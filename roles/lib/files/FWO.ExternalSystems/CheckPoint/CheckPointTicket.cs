using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Logging;
using Microsoft.AspNetCore.Http.HttpResults;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Utilities.Net;
using RestSharp;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.CheckPoint
{
    internal sealed record RenderedTask(string TaskType, JsonNode Body);

    internal sealed class CheckPointExecutionPlan
    {
        public List<RenderedTask> Steps { get; set; } = [];
    }

    /// <summary>
    /// CheckPoint implementation using template driven requests similar to SecureChange.
    /// </summary>
    public class CheckPointTicket : ExternalTicket
    {
        private const string Content = "Content: ";

        private readonly CheckPointClient checkPointClient;

        private WfReqTask? rootTask;

        /// <summary>
        /// Fully rendered executable requests.
        /// </summary>
        private readonly List<RenderedTask> renderedTasks = [];

        public CheckPointTicket(ExternalTicketSystem checkPointSystem, CheckPointClient? checkPointClient = null)
        {
            TicketSystem = checkPointSystem;
            this.checkPointClient = checkPointClient ?? new CheckPointClient(checkPointSystem);
        }

        #region Request Creation

        public override async Task CreateRequestString(List<WfReqTask> tasks, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention)
        {
            rootTask = tasks.FirstOrDefault();

            renderedTasks.Clear();
            TicketTasks.Clear();

            BuildRequestTasks(tasks, ipProtos, namingConvention);

            if (TicketText.Contains(GlobalConst.kPlaceholderMarker))
            {
                throw new ConfigException("Template error. Unhandled placeholder found.");
            }

            await Task.CompletedTask;
        }

        private void BuildRequestTasks(List<WfReqTask> tasks, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention)
        {
            foreach (WfReqTask task in tasks)
            {
                string taskType = GetCheckPointTaskType(task);
                Log.WriteInfo("DEBUG", $"TaskNumber: {task.TaskNumber}");
                Log.WriteInfo("DEBUG", $"Generated AR: AR{task.TaskNumber}");
                ExternalTicketTemplate template = ResolveTemplate(taskType, task);

                CheckPointTicketTask ticketTask = new(task, ipProtos, namingConvention);

                ticketTask.FillTaskText(template);  // Placeholers setzen? TaskText

                if (task.TaskType == nameof(WfTaskType.group_create) || task.TaskType == nameof(WfTaskType.group_modify))
                {
                    AddMemberObjectSteps(ticketTask);       // Members Tasks
                }

                TicketTasks.Add(ticketTask.TaskText);

                string renderedBody = RenderTemplate(template.TicketTemplate, task);
                JsonNode body = JsonNode.Parse(renderedBody) ?? throw new ConfigException("Rendered CheckPoint body could not be parsed into valid JSON.");

                renderedTasks.Add(new RenderedTask(taskType, body));
            }

            AddPublishTask();       // Last Task Publish, not publish again? 

            TicketText = SerializeExecutionPlan();
        }

        private void AddMemberObjectSteps(CheckPointTicketTask ticketTask)
        {
            foreach (CheckPointObjectRequest request in ticketTask.GetRequiredMemberObjects())
            {
                renderedTasks.Add(new RenderedTask(GetTaskType(request), RenderObjectBody(request)));
                AddPublishTask();
            }
        }
        private static string GetTaskType(CheckPointObjectRequest request)
        {
            return request.NetworkObjectType switch
            {
                ObjectType.Host => CheckPointTaskTypes.HostCreate,
                ObjectType.Network => CheckPointTaskTypes.NetworkCreate,
                ObjectType.IPRange => CheckPointTaskTypes.Adress_RangeCreate, // AddressRangeCreate,
                _ => throw new ConfigException("Unsupported CheckPoint object type.")
            };
        }

        private static JsonNode RenderObjectBody(CheckPointObjectRequest request)
        {
            return request.NetworkObjectType switch
            {
                ObjectType.Host => new JsonObject
                {
                    ["name"] = request.Name,
                    ["ip-address"] = request.IpAddress
                },

                ObjectType.Network => new JsonObject
                {
                    ["name"] = request.Name,
                    ["subnet4"] = request.Subnet,
                    ["mask-length4"] = request.MaskLength
                },

                ObjectType.IPRange => new JsonObject
                {
                    ["name"] = request.Name,
                    ["ip-address-first"] = request.StartIp,
                    ["ip-address-last"] = request.EndIp
                },

                _ => throw new ConfigException("Unsupported CheckPoint object type.")
            };
        }




        private static string GetEndpoint(string taskType)
        {
            return taskType switch
            {
                CheckPointTaskTypes.GroupCreate => "add-group",
                CheckPointTaskTypes.GroupModify => "set-group",
                CheckPointTaskTypes.GroupDelete => "delete-group",
                CheckPointTaskTypes.HostCreate => "add-host",
                CheckPointTaskTypes.NetworkCreate => "add-network",
                CheckPointTaskTypes.Adress_RangeCreate => "add-address-range",

                CheckPointTaskTypes.Publish => "publish",
                _ => throw new ConfigException($"No endpoint mapping for taskType {taskType}")
            };
        }

        private void AddPublishTask()
        {
            ExternalTicketTemplate? template = GetTemplate(CheckPointTaskTypes.Publish);

            string json = template != null ? RenderTemplate(template.TicketTemplate, rootTask) : "{}";

            JsonNode body = JsonNode.Parse(json) ?? new JsonObject();

            renderedTasks.Add(new RenderedTask(CheckPointTaskTypes.Publish, body));
        }

        private void AddInstallPolicyTasks()
        {
            List<CheckPointInstallPolicyTarget> targets = GetInstallPolicyTargets();

            if (targets.Count == 0)
            {
                return;
            }

            ExternalTicketTemplate template = GetTemplate(CheckPointTaskTypes.InstallPolicy) ?? throw new ConfigException("Missing install policy template.");

            foreach (CheckPointInstallPolicyTarget target in targets)
            {
                //string body = template.TicketTemplate
                //    .Replace(
                //        Placeholder.POLICY_PACKAGE,
                //        target.PolicyPackage)
                //    .Replace(
                //        Placeholder.TARGETS,
                //        JsonSerializer.Serialize(target.Targets));

                //renderedTasks.Add(new RenderedTask(
                //    CheckPointTaskTypes.InstallPolicy,
                //    template.Endpoint
                //    ));
            }
        }

        #endregion

        #region External Processing

        public override async Task<RestResponse<int>> CreateExternalTicket()
        {
            try
            {
                EnsureExecutionPlanLoaded();

                RestResponse<int>? lastResponse = null;

                foreach (RenderedTask task in renderedTasks)
                {
                    Log.WriteInfo("CheckPoint", $"Executing task: {task.TaskType}");

                    lastResponse = await ExecuteTask(task);



                    if (!string.IsNullOrWhiteSpace(lastResponse.Content))   // Host already exists? Multiple Objects with same IP-Adress
                    {
                        Log.WriteInfo("CheckPoint RESPONSE BODY", lastResponse.Content);
                    }
                    else
                    {
                        Log.WriteWarning("CheckPoint RESPONSE", "Empty response body");
                    }

                    if (!IsSuccess(lastResponse))
                    {
                        return lastResponse;
                    }





                }

                return lastResponse ?? throw new ProcessingFailedException("No response received from CheckPoint.");
            }
            finally
            {
                await checkPointClient.Logout();
            }
        }

        private string SerializeExecutionPlan()
        {
            return JsonSerializer.Serialize(new CheckPointExecutionPlan
            {
                Steps = [.. renderedTasks]
            });
        }

        private void EnsureExecutionPlanLoaded()
        {
            if (renderedTasks.Count > 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(TicketText))
            {
                throw new ProcessingFailedException("CheckPoint request content missing.");
            }

            try
            {
                CheckPointExecutionPlan? plan = JsonSerializer.Deserialize<CheckPointExecutionPlan>(TicketText);    // Prüfen wie Plan funktioniert, 

                if (plan?.Steps == null || plan.Steps.Count == 0)
                {
                    throw new ProcessingFailedException("CheckPoint request content has no executable steps.");
                }

                renderedTasks.AddRange(plan.Steps);
            }
            catch (JsonException exception)
            {
                throw new ProcessingFailedException("Invalid CheckPoint request content format.", exception);
            }
        }

        private async Task<RestResponse<int>> ExecuteTask(RenderedTask task)
        {
            //bool exists = await CheckForGroupExists(task);      // reagieren auf Fehler, also alles senden

            string endpoint = GetEndpoint(task.TaskType);

            // CREATE
            //if (task.TaskType == CheckPointTaskTypes.GroupCreate)
            //{
            //    Log.WriteInfo("CheckPoint", "Group already exists → skipping create");
            //    return new RestResponse<int>(new RestRequest())
            //    {
            //        StatusCode = HttpStatusCode.Conflict,
            //        Data = 0,
            //        Content = "Group already exists"
            //    };

            //}

            // DELETE 
            //if (task.TaskType == CheckPointTaskTypes.GroupDelete && !exists)
            //{
            //    Log.WriteInfo("CheckPoint", "Group does not exist → skipping delete");
            //    return new RestResponse<int>(new RestRequest())
            //    {
            //        StatusCode = HttpStatusCode.OK,
            //        Data = 1,
            //        Content = "Already deleted"
            //    };
            //}


            // Normal
            RestRequest request = new(endpoint, Method.Post);

            request.AddStringBody(task.Body.ToJsonString(), ContentType.Json);

            Log.WriteInfo("CheckPoint REQUEST", endpoint);
            Log.WriteInfo("CheckPoint BODY", task.Body.ToJsonString());

            RestResponse response = await checkPointClient.RestCall(request, endpoint);


            if (task.TaskType == CheckPointTaskTypes.HostCreate && IsMultipleIpAddressError(response))
            {
                string? ipAddress = task.Body?["ip-address"]?.GetValue<string>() ?? "";

                if(!string.IsNullOrEmpty(ipAddress))
                {
                    RestResponse lookupResponse = await FindExistingHostByIpAsync(ipAddress);

                }
            }

            return new RestResponse<int>(request)
            {
                StatusCode = response.StatusCode,
                ResponseStatus = response.ResponseStatus,
                Content = response.Content,
                ErrorMessage = response.ErrorMessage,
                ErrorException = response.ErrorException,
                Data = response.IsSuccessful ? 1 : 0
            };



        }

        private async Task WaitForTaskCompletion(string taskId)
        {
            TicketId = taskId;

            const int maxRetries = 60;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                RestResponse<int> response = await PollExternalTicket();

                if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    throw new ProcessingFailedException("Polling failed.");
                }

                string state = GetInternalState(response.Content);

                if (state == ExtStates.ExtReqDone.ToString())
                {
                    return;
                }

                if (state == ExtStates.ExtReqRejected.ToString())
                {
                    throw new ProcessingFailedException(response.Content);
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            throw new TimeoutException(
                $"CheckPoint task {taskId} timed out.");
        }

        #endregion


        private async Task<bool> CheckForGroupExists(RenderedTask task)
        {
            RestRequest request = new("show-group", Method.Post);

            string? name = task.Body?["name"]?.GetValue<string>();

            request.AddJsonBody(new { name });

            RestResponse response = await checkPointClient.RestCall(request, "show-group");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (response.IsSuccessful)
            {
                return true;
            }

            // optional: Logging für echte Fehler
            Log.WriteWarning("CheckGroupExists", response.Content ?? "no content");

            return false;
        }

        private async Task<RestResponse> FindExistingHostByIpAsync(string ipAddress)
        {
            RestRequest request = new("show-objects", Method.Post);

            request.AddJsonBody(new
            {
                type = "host",
                filter = ipAddress
            });

            return await checkPointClient.RestCall(request, "show-objects");
        }



        #region Polling

        public override async Task<(string, string?)> GetNewState(string oldState)
        {
            if (string.IsNullOrWhiteSpace(TicketId))
            {
                return (
                    ExtStates.ExtReqDone.ToString(),
                    "No asynchronous CheckPoint task id returned.");
            }

            RestResponse<int> response =
                await PollExternalTicket();

            if (response.StatusCode == HttpStatusCode.OK &&
                response.Content != null)
            {
                return (
                    GetInternalState(response.Content),
                    response.Content);
            }

            Log.WriteError(
                $"Poll status failed for CheckPoint task {TicketId}.",
                Content + response.Content);

            throw new ProcessingFailedException(
                response.ErrorMessage ?? "");
        }

        protected override async Task<RestResponse<int>> PollExternalTicket()
        {
            RestRequest request = new("show-task", Method.Post);

            request.AddJsonBody(new
            {
                taskId = TicketId
            });

            RestResponse response = await checkPointClient.RestCall(request, "show-task");

            return new RestResponse<int>(request)
            {
                StatusCode = response.StatusCode,
                Content = response.Content,
                Data = response.IsSuccessful ? 1 : 0,
                ResponseStatus = response.ResponseStatus,
                ErrorMessage = response.ErrorMessage,
                ErrorException = response.ErrorException
            };

            //return await checkPointClient.RestCall(request, "show-task");
        }

        #endregion

        #region Template Handling

        private ExternalTicketTemplate ResolveTemplate(string taskType, WfReqTask task)
        {
            ExternalTicketTemplate? template = GetTemplate(taskType);

            if (template != null)
            {
                return template;
            }

            string fallback = GetSecureChangeTemplateTaskType(task);

            template = GetTemplate(fallback);

            if (template != null)
            {
                return template;
            }

            throw new ConfigException(
                $"No template found for task type {taskType}.");
        }

        private ExternalTicketTemplate? GetTemplate(string taskType)
        {
            return TicketSystem.Templates.FirstOrDefault(template => template.TaskType == taskType); //group create, template Networkmodify / immer fallback nutzen
        }

        private string RenderTemplate(string template, WfReqTask? reqTask)
        {
            string appId = reqTask != null && reqTask.Owners.Count > 0 ? reqTask.Owners[0]?.Owner.ExtAppId ?? "" : "";

            ExtMgtData extMgt = GetExtMgtData(reqTask);

            string rendered =
                template
                    .Replace(
                        Placeholder.TICKET_SUBJECT,
                        Subject)
                    .Replace(
                        Placeholder.PRIORITY,
                        Priority)
                    .Replace(
                        Placeholder.ONBEHALF,
                        Requester)
                    .Replace(
                        Placeholder.REQUESTER,
                        Requester)
                    .Replace(
                        Placeholder.REASON,
                        reqTask?.Reason ?? "")
                    .Replace(
                        Placeholder.APPID,
                        appId)
                    .Replace(
                        Placeholder.GROUPNAME,
                        reqTask?.GetAddInfoValue(AdditionalInfoKeys.GrpName) ?? "")
                    .Replace(
                        Placeholder.MANAGEMENT_ID,
                        extMgt.ExtId ?? reqTask?.ManagementId?.ToString() ?? "0")
                    .Replace(
                        Placeholder.MANAGEMENT_NAME,
                        extMgt.ExtName ?? reqTask?.OnManagement?.Name ?? "")
                    .Replace(
                        Placeholder.TASKS,
                        string.Join(",", TicketTasks));

            bool shortened = false;

            rendered = rendered.SanitizeEolMand(ref shortened);

            CheckForProperJson(rendered);

            return rendered;
        }

        #endregion

        #region State Mapping

        private static string GetInternalState(
            string responseContent)
        {
            string externalState =
                TryGetJsonValue(responseContent, "status")
                    ?.ToLowerInvariant() ?? "";

            if (externalState.Contains("failed") ||
                externalState.Contains("canceled") ||
                externalState.Contains("cancelled"))
            {
                return ExtStates.ExtReqRejected.ToString();
            }

            if (externalState.Contains("succeeded") ||
                externalState.Contains("success"))
            {
                return ExtStates.ExtReqDone.ToString();
            }

            return ExtStates.ExtReqInProgress.ToString();
        }

        #endregion

        #region Helper

        public override string GetTaskTypeAsString(WfReqTask task)
        {
            return GetCheckPointTaskType(task);
        }

        private static string NormalizeStateAfterCreation(
            string? configuredState)
        {
            if (string.IsNullOrWhiteSpace(configuredState))
            {
                return ExtStates.ExtReqDone.ToString();
            }

            return configuredState.Trim() switch
            {
                nameof(ExtStates.ExtReqInitialized)
                    => ExtStates.ExtReqInitialized.ToString(),

                nameof(ExtStates.ExtReqFailed)
                    => ExtStates.ExtReqFailed.ToString(),

                nameof(ExtStates.ExtReqRequested)
                    => ExtStates.ExtReqRequested.ToString(),

                nameof(ExtStates.ExtReqInProgress)
                    => ExtStates.ExtReqInProgress.ToString(),

                nameof(ExtStates.ExtReqDone)
                    => ExtStates.ExtReqDone.ToString(),

                nameof(ExtStates.ExtReqRejected)
                    => ExtStates.ExtReqRejected.ToString(),

                _ => ExtStates.ExtReqDone.ToString()
            };
        }

        private static bool IsSuccess(RestResponse<int> response)
        {
            return response.StatusCode == HttpStatusCode.OK ||
                   response.StatusCode == HttpStatusCode.Created;
        }

        private List<CheckPointInstallPolicyTarget> GetInstallPolicyTargets()
        {
            if (string.IsNullOrWhiteSpace(
                ExtQueryVariables))
            {
                return [];
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(ExtQueryVariables);

                if (document.RootElement.TryGetProperty(ExternalVarKeys.CheckPointInstallPolicyTargets, out JsonElement targets))
                {
                    return JsonSerializer.Deserialize<List<CheckPointInstallPolicyTarget>>(targets.GetRawText()) ?? [];
                }
            }
            catch (JsonException)
            {
                return [];
            }

            return [];
        }

        private static string GetCheckPointTaskType(WfReqTask task)
        {
            return task.TaskType switch
            {
                nameof(WfTaskType.group_create)
                    => IsServiceFlavor(task)
                        ? SCTaskType.NetworkServiceCreate.ToString()
                        : task.TaskType,

                nameof(WfTaskType.group_modify)
                    => IsServiceFlavor(task)
                        ? SCTaskType.NetworkServiceUpdate.ToString()
                        : task.TaskType,

                _ => task.TaskType
            };
        }

        private static string GetSecureChangeTemplateTaskType(WfReqTask task)
        {
            return task.TaskType switch
            {
                nameof(WfTaskType.access)
                    => SCTaskType.AccessRequest.ToString(),

                nameof(WfTaskType.rule_modify)
                    => SCTaskType.AccessRequest.ToString(),

                nameof(WfTaskType.rule_delete)
                    => SCTaskType.AccessRequest.ToString(),

                nameof(WfTaskType.group_create)
                    => SCTaskType.NetworkObjectModify.ToString(),

                nameof(WfTaskType.group_modify)
                    => SCTaskType.NetworkObjectModify.ToString(),

                _ => task.TaskType
            };
        }

        private static bool IsServiceFlavor(WfReqTask task)
        {
            return
                task.Elements.Any(
                    e => e.Field ==
                    ElemFieldType.service.ToString()) &&
                task.Elements.All(
                    e => e.Field ==
                    ElemFieldType.service.ToString());
        }

        private static string? TryGetJsonValue(string? content, params string[] propertyNames)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(content);

                return FindJsonValue(document.RootElement, propertyNames);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? FindJsonValue(JsonElement element, params string[] propertyNames)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property
                    in element.EnumerateObject())
                {
                    if (propertyNames.Contains(
                        property.Name,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        return property.Value.ToString();
                    }

                    string? childValue =
                        FindJsonValue(
                            property.Value,
                            propertyNames);

                    if (!string.IsNullOrWhiteSpace(childValue))
                    {
                        return childValue;
                    }
                }
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child
                    in element.EnumerateArray())
                {
                    string? childValue =
                        FindJsonValue(
                            child,
                            propertyNames);

                    if (!string.IsNullOrWhiteSpace(childValue))
                    {
                        return childValue;
                    }
                }
            }

            return null;
        }

        private static ExtMgtData GetExtMgtData(WfReqTask? reqTask)
        {
            var raw = reqTask?.OnManagement?.ExtMgtData;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new ExtMgtData();
            }

            try
            {
                return JsonSerializer.Deserialize<ExtMgtData>(raw);
            }
            catch (JsonException)
            {
                return new ExtMgtData();
            }
        }

        private static bool IsMultipleIpAddressError(RestResponse response)
        {
            if (response.StatusCode != HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(response.Content))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(response.Content);

                if (document.RootElement.TryGetProperty("message", out JsonElement messageElement))
                {
                    string? message = messageElement.GetString();

                    if (!string.IsNullOrWhiteSpace(message) && message.Contains("multiple IP", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (document.RootElement.TryGetProperty("code", out JsonElement codeElement))
                {
                    string? code = codeElement.GetString();

                    if (string.Equals(code, "generic_err_multiple_ip_addresses", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
                return response.Content.Contains("multiple IP", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }


        #endregion
    }
}
