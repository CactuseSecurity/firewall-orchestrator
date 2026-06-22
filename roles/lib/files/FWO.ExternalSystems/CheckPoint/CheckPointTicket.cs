using FWO.Basics;
using FWO.Basics.Comparer;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Logging;
using NetTools;
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

        private CheckPointClient? checkPointClient;

        private WfReqTask? rootTask;

        /// <summary>
        /// Fully rendered executable requests.
        /// </summary>
        private readonly List<RenderedTask> renderedTasks = [];

        public CheckPointTicket(ExternalTicketSystem checkPointSystem, CheckPointClient? checkPointClient = null)
        {
            TicketSystem = checkPointSystem;
            this.checkPointClient = checkPointClient;
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

                ticketTask.FillTaskText(template);
                TicketTasks.Add(ticketTask.TaskText);

                if (task.TaskType == nameof(WfTaskType.group_create))
                {
                    AddEmptyGroupCreateStep(ticketTask);
                    //AddPublishTask();

                    AddMemberObjectSteps(ticketTask);
                    AddBundledMemberAddStep(ticketTask);
                    continue;
                }

                if (task.TaskType == nameof(WfTaskType.group_modify))
                {
                    AddMemberObjectSteps(ticketTask);
                    AddBundledMemberAddStep(ticketTask);
                    AddBundledMemberRemoveStep(ticketTask);
                    continue;
                }

                renderedTasks.Add(new RenderedTask(taskType, ticketTask.TaskBody));
            }

            AddPublishTask();
            TicketText = SerializeExecutionPlan();
        }

        private void AddMemberObjectSteps(CheckPointTicketTask ticketTask)
        {
            foreach (CheckPointObjectRequest request in ticketTask.GetRequiredMemberObjectSteps())
            {
                renderedTasks.Add(new RenderedTask(GetTaskType(request), RenderObjectBody(request)));
                //AddPublishTask();
            }
        }
        private void AddEmptyGroupCreateStep(CheckPointTicketTask ticketTask)
        {
            renderedTasks.Add(new RenderedTask(CheckPointTaskTypes.GroupCreate, ticketTask.RenderEmptyGroupCreateBody()));
        }
        private void AddBundledMemberAddStep(CheckPointTicketTask ticketTask)
        {
            List<string> membersToAdd = ticketTask.GetMembersToAdd();
            if (membersToAdd.Count == 0)
            {
                return;
            }
            renderedTasks.Add(new RenderedTask(CheckPointTaskTypes.GroupAddMembers, ticketTask.RenderGroupMembersAddBody(membersToAdd)));
            //AddPublishTask();

        }
        private void AddBundledMemberRemoveStep(CheckPointTicketTask ticketTask)
        {
            List<string> membersToRemove = ticketTask.GetMembersToRemove();
            if (membersToRemove.Count == 0)
            {
                return;
            }
            renderedTasks.Add(new RenderedTask(CheckPointTaskTypes.GroupRemoveMembers, ticketTask.RenderGroupMembersRemoveBody(membersToRemove)));
            //AddPublishTask();

        }
        private static string GetTaskType(CheckPointObjectRequest request)
        {
            return request.RequestAction switch
            {
                nameof(RequestAction.create) => request.NetworkObjectType switch
                {
                    ObjectType.Host => CheckPointTaskTypes.HostCreate,
                    ObjectType.Network => CheckPointTaskTypes.NetworkCreate,
                    ObjectType.IPRange => CheckPointTaskTypes.AddressRangeCreate,
                    _ => throw new ConfigException("Unsupported CheckPoint object type.")
                },

                nameof(RequestAction.modify) => request.NetworkObjectType switch
                {
                    ObjectType.Host => CheckPointTaskTypes.HostModify,
                    ObjectType.Network => CheckPointTaskTypes.NetworkModify,
                    ObjectType.IPRange => CheckPointTaskTypes.AddressRangeModify,
                    _ => throw new ConfigException("Unsupported CheckPoint object type.")
                },
                _ => throw new ConfigException($"Unsupported CheckPoint request action '{request.RequestAction}'.")
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
                    ["subnet"] = request.Subnet,
                    ["mask-length"] = request.MaskLength
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
                CheckPointTaskTypes.GroupAddMembers => "set-group",
                CheckPointTaskTypes.GroupRemoveMembers => "set-group",
                CheckPointTaskTypes.GroupDelete => "delete-group",

                CheckPointTaskTypes.HostCreate => "add-host",
                CheckPointTaskTypes.HostModify => "set-host",

                CheckPointTaskTypes.NetworkCreate => "add-network",
                CheckPointTaskTypes.NetworkModify => "set-network",

                CheckPointTaskTypes.AddressRangeCreate => "add-address-range",
                CheckPointTaskTypes.AddressRangeModify => "set-address-range",

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
            checkPointClient ??= new CheckPointClient(TicketSystem, OnManagement ?? throw new ProcessingFailedException("No management context available for Check Point request."));

            try
            {
                EnsureExecutionPlanLoaded();
                RestResponse<int> response = await ExecuteAllSteps();
                TicketId = checkPointClient.CurrentSessionId;
                return response;
            }
            finally
            {
                await checkPointClient.Logout();
            }
        }

        private async Task<RestResponse<int>> ExecuteAllSteps()
        {
            RestResponse<int>? lastResponse = null;

            foreach (RenderedTask task in renderedTasks)
            {
                if (IsRuleChangeTaskType(task.TaskType))
                {
                    return new RestResponse<int>(new RestRequest())
                    {
                        StatusCode = HttpStatusCode.OK,
                        ResponseStatus = ResponseStatus.Completed,
                        Content = "Check Point rule change tasks are not yet supported.",
                        Data = 1
                    };
                    //return new RestResponse<int>(new RestRequest())
                    //{
                    //    StatusCode = HttpStatusCode.BadRequest,
                    //    ResponseStatus = ResponseStatus.Completed,
                    //    Content = "Check Point rule change tasks are not yet supported.",
                    //    Data = 0
                    //};
                }

                Log.WriteInfo("CheckPoint", $"Executing task: {task.TaskType}");

                lastResponse = await ExecuteTask(task);

                if (!string.IsNullOrWhiteSpace(lastResponse.Content))
                {
                    Log.WriteInfo("CheckPoint RESPONSE BODY", lastResponse.Content);
                }
                else
                {
                    Log.WriteWarning("CheckPoint RESPONSE", "Empty response body");
                }

                if (!IsSynchronousSuccess(lastResponse))
                {
                    return lastResponse;
                }
            }

            return lastResponse ?? throw new ProcessingFailedException("No response received from CheckPoint.");
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
                CheckPointExecutionPlan? plan = JsonSerializer.Deserialize<CheckPointExecutionPlan>(TicketText);

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
            string endpoint = GetEndpoint(task.TaskType);
            JsonNode requestBody = task.Body.DeepClone();

            RestRequest request = new(endpoint, Method.Post);
            request.AddStringBody(requestBody.ToJsonString(), ContentType.Json);

            Log.WriteInfo("CheckPoint REQUEST", endpoint);
            Log.WriteInfo("CheckPoint BODY", requestBody.ToJsonString());

            RestResponse response = await checkPointClient!.RestCall(request, endpoint);

            Log.WriteInfo("CheckPoint RESPONSE STATUS", $"{(int)response.StatusCode} {response.StatusCode}");
            Log.WriteInfo("CheckPoint RESPONSE BODY", string.IsNullOrWhiteSpace(response.Content) ? "<empty>" : response.Content);

            CheckPointResponseCategory category = CategorizeResponse(response);

            if (category == CheckPointResponseCategory.Success)
            {
                return ToTypedResponse(request, response);
            }

            if (CanCheckDesiredState(task) && await IsDesiredStateAlreadyPresent(task))
            {
                Log.WriteWarning("CheckPoint SKIP", $"Skipping {task.TaskType}: desired state already present.");
                return BuildAlreadyPresentSuccessResponse(request, response.Content);
            }

            if (category == CheckPointResponseCategory.WarningCandidate && CanRetryWithIgnoreWarnings(task))
            {
                RestResponse retryResponse = await RetryWithIgnoreWarnings(endpoint, task.Body);
                return ToTypedResponse(request, retryResponse);
            }

            return ToTypedResponse(request, response);
        }

        private static CheckPointResponseCategory CategorizeResponse(RestResponse response)
        {
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
            {
                return CheckPointResponseCategory.Success;
            }

            string content = response.Content ?? "";

            if (content.Contains("Check Point rule change tasks are not yet supported.", StringComparison.OrdinalIgnoreCase))
            {
                return CheckPointResponseCategory.HardError;
            }

            if (content.Contains("multiple IP", StringComparison.OrdinalIgnoreCase)    || content.Contains("same IP address", StringComparison.OrdinalIgnoreCase)    
                || content.Contains("same IPv6 address", StringComparison.OrdinalIgnoreCase)    || content.Contains("same IPv4 address", StringComparison.OrdinalIgnoreCase)    
                || content.Contains("More than one object has the same", StringComparison.OrdinalIgnoreCase))
            {
                return CheckPointResponseCategory.WarningCandidate;
            }

            return CheckPointResponseCategory.HardError;
        }

        private async Task<RestResponse> RetryWithIgnoreWarnings(string endpoint, JsonNode originalBody)
        {
            JsonNode retryBody = originalBody.DeepClone();
            retryBody["ignore-warnings"] = true;

            Log.WriteInfo("CheckPoint RETRY", $"Retrying {endpoint} with ignore-warnings=true");
            Log.WriteInfo("CheckPoint RETRY BODY", retryBody.ToJsonString());

            RestRequest retryRequest = new(endpoint, Method.Post);
            retryRequest.AddStringBody(retryBody.ToJsonString(), ContentType.Json);

            RestResponse retryResponse = await checkPointClient!.RestCall(retryRequest, endpoint);

            Log.WriteInfo("CheckPoint RETRY RESPONSE STATUS", $"{(int)retryResponse.StatusCode} {retryResponse.StatusCode}");
            Log.WriteInfo("CheckPoint RETRY RESPONSE BODY", string.IsNullOrWhiteSpace(retryResponse.Content) ? "<empty>" : retryResponse.Content);

            return retryResponse;
        }

        private static RestResponse<int> ToTypedResponse(RestRequest request, RestResponse response)
        {
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
        #region Helper

        public override string GetTaskTypeAsString(WfReqTask task)
        {
            return GetCheckPointTaskType(task);
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

        private static bool HasMatchingMessage(JsonElement element, string expectedMessageFragment)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("message"))
                    {
                        string? message = property.Value.GetString();

                        if (!string.IsNullOrWhiteSpace(message)
                            && message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    if (HasMatchingMessage(property.Value, expectedMessageFragment))
                    {
                        return true;
                    }
                }
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in element.EnumerateArray())
                {
                    if (HasMatchingMessage(child, expectedMessageFragment))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsRuleChangeTaskType(string? taskType)
        {
            return taskType == nameof(WfTaskType.access)
                || taskType == nameof(WfTaskType.rule_modify)
                || taskType == nameof(WfTaskType.rule_delete);
        }

        private static bool IsSynchronousSuccess(RestResponse<int> response)
        {
            return response.StatusCode == HttpStatusCode.OK ||
                   response.StatusCode == HttpStatusCode.Created;
        }
        private enum CheckPointResponseCategory
        {
            Success,
            WarningCandidate,
            HardError
        }

        private static bool CanCheckDesiredState(RenderedTask task)
        {
            return task.TaskType == CheckPointTaskTypes.GroupCreate ||
                   task.TaskType == CheckPointTaskTypes.HostCreate ||
                   task.TaskType == CheckPointTaskTypes.NetworkCreate ||
                   task.TaskType == CheckPointTaskTypes.AddressRangeCreate;
        }
        private async Task<bool> IsDesiredStateAlreadyPresent(RenderedTask task)
        {
            return task.TaskType switch
            {
                CheckPointTaskTypes.GroupCreate => await GroupExists(task),
                CheckPointTaskTypes.HostCreate => await ExistingHostMatches(task),
                CheckPointTaskTypes.NetworkCreate => await ExistingNetworkMatches(task),
                CheckPointTaskTypes.AddressRangeCreate => await ExistingAddressRangeMatches(task),
                _ => false
            };
        }

        private static bool CanRetryWithIgnoreWarnings(RenderedTask task)
        {
            return task.TaskType == CheckPointTaskTypes.HostCreate ||
                   task.TaskType == CheckPointTaskTypes.NetworkCreate ||
                   task.TaskType == CheckPointTaskTypes.AddressRangeCreate;
        }

        private async Task<JsonElement?> LoadObject(string endpoint, string name)
        {
            RestRequest request = new(endpoint, Method.Post);
            request.AddJsonBody(new { name });

            RestResponse response = await checkPointClient!.RestCall(request, endpoint);

            if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(response.Content);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task<bool> GroupExists(RenderedTask task)
        {
            string? name = task.Body?["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            JsonElement? existing = await LoadObject("show-group", name);
            return existing != null;
        }

        private async Task<bool> ExistingHostMatches(RenderedTask task)
        {
            string? name = task.Body?["name"]?.GetValue<string>();
            string? requestedIp = task.Body?["ip-address"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(requestedIp))
            {
                return false;
            }

            JsonElement? existing = await LoadObject("show-host", name);
            if (existing == null)
            {
                return false;
            }

            string? existingIp = TryGetString(existing.Value, "ipv4-address")
                ?? TryGetString(existing.Value, "ipv6-address");

            if (string.IsNullOrWhiteSpace(existingIp))
            {
                return false;
            }

            if (!IPAddress.TryParse(requestedIp.StripOffNetmask(), out IPAddress? requestedAddress) ||
                !IPAddress.TryParse(existingIp.StripOffNetmask(), out IPAddress? existingAddress))
            {
                return false;
            }

            IPAdressComparer comparer = new();
            return comparer.Compare(existingAddress, requestedAddress) == 0;
        }

        private async Task<bool> ExistingAddressRangeMatches(RenderedTask task)
        {
            string? name = task.Body?["name"]?.GetValue<string>();
            string? requestedFirst = task.Body?["ip-address-first"]?.GetValue<string>();
            string? requestedLast = task.Body?["ip-address-last"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(requestedFirst) ||
                string.IsNullOrWhiteSpace(requestedLast))
            {
                return false;
            }

            JsonElement? existing = await LoadObject("show-address-range", name);
            if (existing == null)
            {
                return false;
            }

            string? existingFirst = TryGetString(existing.Value, "ipv4-address-first")
                ?? TryGetString(existing.Value, "ipv6-address-first")
                ?? TryGetString(existing.Value, "ip-address-first");

            string? existingLast = TryGetString(existing.Value, "ipv4-address-last")
                ?? TryGetString(existing.Value, "ipv6-address-last")
                ?? TryGetString(existing.Value, "ip-address-last");

            if (string.IsNullOrWhiteSpace(existingFirst) || string.IsNullOrWhiteSpace(existingLast))
            {
                return false;
            }

            if (!IPAddress.TryParse(requestedFirst.StripOffNetmask(), out IPAddress? requestedFirstAddress) ||
                !IPAddress.TryParse(requestedLast.StripOffNetmask(), out IPAddress? requestedLastAddress) ||
                !IPAddress.TryParse(existingFirst.StripOffNetmask(), out IPAddress? existingFirstAddress) ||
                !IPAddress.TryParse(existingLast.StripOffNetmask(), out IPAddress? existingLastAddress))
            {
                return false;
            }

            IPAddressRange requestedRange = new(requestedFirstAddress, requestedLastAddress);
            IPAddressRange existingRange = new(existingFirstAddress, existingLastAddress);

            IPAddressRangeComparer comparer = new();
            return comparer.Compare(existingRange, requestedRange) == 0;
        }

        private async Task<bool> ExistingNetworkMatches(RenderedTask task)
        {
            string? name = task.Body?["name"]?.GetValue<string>();
            string? requestedSubnet = task.Body?["subnet"]?.GetValue<string>();
            int? requestedMaskLength = task.Body?["mask-length"]?.GetValue<int?>();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(requestedSubnet) ||
                requestedMaskLength == null)
            {
                return false;
            }

            if (!IPAddress.TryParse(requestedSubnet.StripOffNetmask(), out IPAddress? requestedSubnetAddress))
            {
                return false;
            }

            JsonElement? existing = await LoadObject("show-network", name);
            if (existing == null)
            {
                return false;
            }

            bool isIpv6 = requestedSubnetAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;

            string? existingSubnet = isIpv6
                ? TryGetString(existing.Value, "subnet6")
                : TryGetString(existing.Value, "subnet4") ?? TryGetString(existing.Value, "subnet");

            int? existingMaskLength = isIpv6
                ? TryGetInt(existing.Value, "mask-length6")
                : TryGetInt(existing.Value, "mask-length4") ?? TryGetInt(existing.Value, "mask-length");

            if (!isIpv6 && existingMaskLength == null)
            {
                string? subnetMask = TryGetString(existing.Value, "subnet-mask");
                if (!string.IsNullOrWhiteSpace(subnetMask) && IPAddress.TryParse(subnetMask, out IPAddress? maskIp))
                {
                    existingMaskLength = NetMaskToPrefixLength(maskIp);
                }
            }

            if (string.IsNullOrWhiteSpace(existingSubnet) || existingMaskLength == null)
            {
                return false;
            }

            if (!IPAddress.TryParse(existingSubnet.StripOffNetmask(), out IPAddress? existingSubnetAddress))
            {
                return false;
            }

            IPAdressComparer comparer = new();
            return comparer.Compare(existingSubnetAddress, requestedSubnetAddress) == 0 &&
                   existingMaskLength == requestedMaskLength;
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement property) &&
                   property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static int? TryGetInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int intValue))
            {
                return intValue;
            }

            if (property.ValueKind == JsonValueKind.String &&
                int.TryParse(property.GetString(), out int parsedValue))
            {
                return parsedValue;
            }

            return null;
        }

        private static int NetMaskToPrefixLength(IPAddress subnetMask)
        {
            byte[] bytes = subnetMask.GetAddressBytes();
            int prefixLength = 0;

            foreach (byte currentByte in bytes)
            {
                byte value = currentByte;
                while (value != 0)
                {
                    prefixLength += value & 1;
                    value >>= 1;
                }
            }

            return prefixLength;
        }

        private static RestResponse<int> BuildAlreadyPresentSuccessResponse(RestRequest request, string? originalContent)
        {
            return new RestResponse<int>(request)
            {
                StatusCode = HttpStatusCode.OK,
                ResponseStatus = ResponseStatus.Completed,
                Content = originalContent ?? "Object already present with matching definition.",
                Data = 1
            };
        }
        #endregion
    }
}
