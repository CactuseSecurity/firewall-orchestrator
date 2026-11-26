using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Logging;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using System.Text.Json;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class to execute handling of external requests
    /// </summary>
    public partial class ExternalRequestHandler : IDisposable
    {
        private readonly ApiConnection ApiConnection;
        private readonly ExtStateHandler? extStateHandler;
        private readonly WfHandler wfHandler;
        private readonly UserConfig UserConfig;
        private bool disposed = false;
        private ExternalTicketSystem actSystem = new();
        private string actTaskType = "";
        private List<IpProtocol> ipProtos = [];
        private List<UserGroup>? ownerGroups = [];
        private SCTemplateLookup scTemplates = new(kNoSCTemplates);

        /// <summary>
        /// Source of the SecureChange ticket templates, which are device specific settings (settable for unit testing)
        /// </summary>
        public ISCTemplateSource SCTemplateSource { get; init; }

        /// <summary>
        /// SecureChange templates before they are resolved for a ticket
        /// </summary>
        private static readonly List<ExternalTicketTemplate> kNoSCTemplates = [];

        /// <summary>
        /// States closing an external request without further processing; reaching them sets the finish date,
        /// which marks the request as no longer active (see unique index uidx_ext_request_one_active_per_ticket)
        /// </summary>
        private static readonly List<string> kFinalRequestStates =
        [
            ExtStates.ExtReqAckRejected.ToString(),
            ExtStates.ExtReqAcknowledged.ToString(),
            ExtStates.ExtReqDiscarded.ToString()
        ];

        /// <summary>
        /// External states that may stay without a mapping to an internal state: the request task then keeps its state
        /// </summary>
        private static readonly IReadOnlyCollection<string> kOptionallyMappedStates =
        [
            ExtStates.ExtReqRequested.ToString(),
            ExtStates.ExtReqInProgress.ToString()
        ];

        /// <summary>
        /// constructor for object with all data necessary for request handling
        /// </summary>
        public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection)
        {
            ApiConnection = apiConnection;
            UserConfig = userConfig;
            extStateHandler = new(apiConnection);
            SCTemplateSource = new ApiSCTemplateSource(apiConnection);
            Task.Run(GetInternalGroups).Wait();
            wfHandler = new(userConfig, apiConnection, WorkflowPhases.request, ownerGroups,
                new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection))
            { SystemContext = true };
        }

        /// <summary>
        /// constructor only for unit testing
        /// </summary>
        public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection, List<UserGroup>? userGroups)
        {
            ApiConnection = apiConnection;
            UserConfig = userConfig;
            extStateHandler = new(apiConnection);
            SCTemplateSource = new ApiSCTemplateSource(apiConnection);
            wfHandler = new(userConfig, apiConnection, WorkflowPhases.request, userGroups,
                new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection))
            { SystemContext = true };
        }

        /// <summary>
        /// Takes over the state of a request into the ticket. A finished request is acknowledged; a rejected one rejects
        /// the following request tasks, otherwise the provisioning continues with the next entry of the work list
        /// (called by scheduler after state change).
        /// </summary>
        public async Task HandleStateChange(ExternalRequest externalRequest)
        {
            WfTicket? intTicket = await InitAndResolve(externalRequest.TicketId);
            if (intTicket == null)
            {
                Log.WriteError("External Request Update", $"Ticket not found.");
            }
            else if (externalRequest.ImplTaskId != null)
            {
                wfHandler.SetTicketEnv(intTicket);
                await HandleImplTaskStateChange(intTicket, externalRequest);
            }
            else
            {
                wfHandler.SetTicketEnv(intTicket);
                await UpdateTicket(intTicket, externalRequest);
                if (extStateHandler != null && extStateHandler.GetInternalStateId(externalRequest.ExtRequestState) >= wfHandler.ActStateMatrix.LowestEndState)
                {
                    await Acknowledge(externalRequest);
                    if (externalRequest.ExtRequestState == ExtStates.ExtReqRejected.ToString())
                    {
                        await RejectFollowingTasks(intTicket, externalRequest.TaskNumber, actSystem.Name);
                        Log.WriteInfo($"External Request {externalRequest.Id} rejected", $"Reject Following Tasks for internal ticket {intTicket.Id}");
                    }
                    else
                    {
                        await StartProvisioning(intTicket.Id, null, externalRequest);
                    }
                }
            }
        }

        /// <summary>
        /// Takes over a state of a request that does not finish it, e.g. "in progress" reported by the external ticket
        /// system, into its request tasks. Unlike HandleStateChange the request is neither acknowledged nor is the
        /// provisioning continued. Requests of implementation tasks (firewall write) are not handled here
        /// (called by scheduler after state change).
        /// </summary>
        public async Task HandleProgress(ExternalRequest externalRequest)
        {
            if (externalRequest.ImplTaskId != null)
            {
                return;
            }
            WfTicket? intTicket = await InitAndResolve(externalRequest.TicketId);
            if (intTicket == null)
            {
                Log.WriteError("External Request Update", $"Ticket {externalRequest.TicketId} not found.");
                return;
            }
            wfHandler.SetTicketEnv(intTicket);
            await UpdateTicket(intTicket, externalRequest);
        }

        /// <summary>
        /// patch the external request state (called by admin in UI via middleware client)
        /// </summary>
        public async Task<bool> PatchState(ExternalRequest externalRequest)
        {
            try
            {
                await UpdateRequestState(externalRequest);
                if (externalRequest.ExtRequestState == ExtStates.ExtReqRejected.ToString() ||
                    externalRequest.ExtRequestState == ExtStates.ExtReqDone.ToString())
                {
                    await HandleStateChange(externalRequest);
                }
                return true;
            }
            catch (Exception exception)
            {
                Log.WriteError("Patch External Request State", $"Runs into exception: ", exception);
                return false;
            }
        }

        private async Task UpdateRequestState(ExternalRequest request)
        {
            try
            {
                if (kFinalRequestStates.Contains(request.ExtRequestState))
                {
                    var FinalVariables = new
                    {
                        id = request.Id,
                        extRequestState = request.ExtRequestState,
                        finishDate = DateTime.Now
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestFinal, FinalVariables);
                    return;
                }
                var Variables = new
                {
                    id = request.Id,
                    extRequestState = request.ExtRequestState
                };
                await ApiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestProcess, Variables);
            }
            catch (Exception exception)
            {
                Log.WriteError("External Request Handler", $"State update failed: ", exception);
            }
        }

        private async Task<WfTicket?> InitAndResolve(long ticketId)
        {
            ipProtos = await ApiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols);
            return await wfHandler.Init() ? await wfHandler.ResolveTicket(ticketId) : null;
        }

        private async Task GetInternalGroups()
        {
            ownerGroups = await MiddlewareServerServices.GetInternalGroups(ApiConnection);
        }

        /// <summary>
        /// Gives a SecureChange system the ticket templates in effect for the management of the task. The templates of all
        /// managements of the ticket are resolved at once, so bundling can compare them.
        /// </summary>
        private async Task ApplySecureChangeTemplates(WfTicket ticket, WfReqTask task)
        {
            if (!actSystem.IsTufinSecureChange())
            {
                return;
            }
            List<int> managementIds = [.. ticket.Tasks.Select(t => t.ManagementId).OfType<int>().Distinct()];
            scTemplates = await SCTemplateSource.Load(managementIds);
            actSystem.Templates = scTemplates.For(task.ManagementId);
        }

        /// <summary>
        /// qad heuristic: wait after a network object group change in the external system before sending
        /// the next request (public only for unit testing).
        /// Waits before an access request, before a rule modification or deletion implemented by Tufin SC,
        /// or before any request if the old request created new objects in the group.
        /// The new object detection only recognizes the Tufin SC request format.
        /// </summary>
        /// <param name="taskType">task type of the next request task</param>
        /// <param name="oldRequest">previously sent external request, if any</param>
        /// <param name="nextSystem">external system handling the next request task</param>
        /// <returns>number of wait cycles, 0 if no waiting is needed</returns>
        public int GetWaitCycles(string taskType, ExternalRequest? oldRequest, ExternalTicketSystem nextSystem)
        {
            // TODO: to be refined
            if (oldRequest != null && UserConfig.ExternalRequestWaitCycles > 0 &&
                // last request handled group
                IsNetworkObjectGroupChange(oldRequest.ExtRequestType) &&
                    // now access request
                    (taskType == WfTaskType.access.ToString() ||
                    // or rule change handled by Tufin SC (independent of the system which handled the group)
                    (nextSystem.IsTufinSecureChange() && WfTaskBase.IsRuleChangeOrDelete(taskType)) ||
                    // or last request created new objects in group
                    ContainsNewObj(oldRequest.ExtRequestContent)))
            {
                return UserConfig.ExternalRequestWaitCycles;
            }
            return 0;
        }

        /// <summary>
        /// Checks if an external request type is a network object group creation or modification,
        /// either in Tufin SC format (e.g. "(NetworkObjectModify, CREATE)") or as workflow task type
        /// (e.g. "group_create", used by CheckPoint). Comparison is case-insensitive.
        /// </summary>
        /// <param name="externalRequestType">type of the external request</param>
        /// <returns>true if the request type is a group creation or modification</returns>
        private static bool IsNetworkObjectGroupChange(string externalRequestType)
        {
            return externalRequestType.Equals("(NetworkObjectModify, CREATE)", StringComparison.OrdinalIgnoreCase)
            || externalRequestType.Equals("(NetworkObjectModify, UPDATE)", StringComparison.OrdinalIgnoreCase)
            || externalRequestType.Equals(WfTaskType.group_create.ToString(), StringComparison.OrdinalIgnoreCase)
            || externalRequestType.Equals(WfTaskType.group_modify.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameRuleOnDiffGw(WfReqTask? task1, WfReqTask? task2)
        {
            return task1 != null && task2 != null && task1.ManagementId == task2.ManagementId &&
                task1.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) == task2.GetAddInfoIntValue(AdditionalInfoKeys.ConnId);
        }

        private static bool ContainsNewObj(string contentString)
        {
            return contentString.Contains("\"object_updated_status\": \"NEW\"") || contentString.Contains("object_updated_status\\u0022: \\u0022NEW\\u0022") ||
                contentString.Contains("\"object_updated_status\":\"NEW\"") || contentString.Contains("object_updated_status\\u0022:\\u0022NEW\\u0022");
        }


        /// <summary>Stores an external request for the given request tasks with its content, variables and wait cycles.</summary>
        private async Task CreateExtRequest(WfTicket ticket, List<WfReqTask> tasks, List<WfReqTask> handledTasks, int waitCycles,
            ProvisioningRelease? release = null)
        {
            // the copy stored with the request only references the credential, the header is built for each call
            actSystem.Authorization = null;
            if (actSystem.LookupRequesterId)
            {
                // the requester lookup already calls the ticket system while the content is constructed
                await ExternalTicketAuthorization.ResolveHeader(actSystem, ApiConnection);
            }
            string taskContent = await ConstructContent(tasks, ticket.Requester);
            Dictionary<string, List<int>> handledTaskNumbers = BuildExtQueryVariables(tasks, handledTasks);
            AddRelease(handledTaskNumbers, release);
            string extQueryVars = handledTaskNumbers.Count > 0
                ? JsonSerializer.Serialize(handledTaskNumbers)
                : "";

            var Variables = new
            {
                ownerId = ticket.Tasks.FirstOrDefault()?.Owners.FirstOrDefault()?.Owner.Id,
                ticketId = ticket.Id,
                taskNumber = tasks.FirstOrDefault()?.TaskNumber ?? 0,
                extTicketSystem = JsonSerializer.Serialize(actSystem),
                extTaskType = actTaskType,
                extTaskContent = taskContent,
                extQueryVariables = extQueryVars,
                extRequestState = ExtStates.ExtReqInitialized.ToString(),
                waitCycles
            };

            await ApiConnection.SendQueryAsync<ReturnIdWrapper>(ExtRequestQueries.addExtRequest, Variables);
            await LogRequestTasks(handledTasks, ticket.Requester?.Name, ModellingTypes.ChangeType.Request);
        }

        private static Dictionary<string, List<int>> BuildExtQueryVariables(List<WfReqTask> tasks, List<WfReqTask> handledTasks)
        {
            Dictionary<string, List<int>> extQueryVariables = [];

            int? managementId = tasks.FirstOrDefault()?.OnManagement?.Id ?? tasks.FirstOrDefault()?.ManagementId;
            if (managementId != null)
            {
                extQueryVariables[ExternalVarKeys.ManagementId] = [managementId.Value];
            }

            if (handledTasks.Count > 1)
            {
                extQueryVariables[ExternalVarKeys.BundledTasks] = handledTasks.ConvertAll(t => t.TaskNumber);
            }

            return extQueryVariables;
        }

        /// <summary>
        /// Rejects the request tasks following a rejected one, as the ticket is not processed further, and names the
        /// rejected request task in the history of each task whose state changes.
        /// </summary>
        /// <param name="ticket">Ticket of the tasks.</param>
        /// <param name="lastTaskNumber">Number of the rejected request task.</param>
        /// <param name="requester">Requester written to the history entries.</param>
        private async Task RejectFollowingTasks(WfTicket ticket, int lastTaskNumber, string? requester)
        {
            int actTaskNumber = lastTaskNumber + 1;
            bool taskFound = true;
            while (taskFound)
            {
                WfReqTask? furtherTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == actTaskNumber);
                if (furtherTask != null)
                {
                    if (await UpdateTaskState(ticket, furtherTask, ExtStates.ExtReqRejected.ToString()))
                    {
                        List<WfReqTask> rejectedTasks = [furtherTask];
                        await LogRequestTasks(rejectedTasks, requester, ModellingTypes.ChangeType.Reject,
                            $"not processed, request task {lastTaskNumber} was rejected");
                    }
                    actTaskNumber++;
                }
                else
                {
                    taskFound = false;
                }
            }
        }

        private async Task<string> ConstructContent(List<WfReqTask> reqTasks, UiUser? requester)
        {
            ExternalTicket ticket = ExternalTicketFactory.Create(actSystem, UserConfig.GetExternalCertificateChecks());
            ticket.Subject = ConstructSubject(reqTasks.Count > 0 ? reqTasks[0] : throw new ArgumentException("No Task given"));
            ticket.Priority = SCTicketPriority.Low.ToString();
            ticket.Requester = requester?.Name ?? "";
            ModellingNamingConvention namingConvention = ModellingNamingConvention.FromJson(UserConfig.ModNamingConvention);
            await ticket.CreateRequestString(reqTasks, ipProtos, namingConvention);
            actTaskType = ticket.GetTaskTypeAsString(reqTasks[0]);
            return ticket.TicketText;
        }

        private string ConstructSubject(WfReqTask reqTask)
        {
            string appId = reqTask.Owners.Count > 0 ? (reqTask.Owners.FirstOrDefault()?.Owner.ExtAppId ?? "") : "";
            string onMgt = UserConfig.GetText("on") + reqTask.OnManagement?.Name + "(" + reqTask.OnManagement?.Id + ")";
            string grpName = " " + reqTask.GetAddInfoValue(AdditionalInfoKeys.GrpName);
            return (appId != "" ? appId + ": " : "") + reqTask.TaskType switch
            {
                nameof(WfTaskType.access) => UserConfig.GetText("create_rule") + onMgt,
                nameof(WfTaskType.rule_modify) => UserConfig.GetText("modify_rule") + onMgt,
                nameof(WfTaskType.rule_delete) => UserConfig.GetText("remove_rule") + onMgt,
                nameof(WfTaskType.group_create) => UserConfig.GetText("create_group") + grpName + onMgt,
                nameof(WfTaskType.group_modify) => UserConfig.GetText("modify_group") + grpName + onMgt,
                nameof(WfTaskType.group_delete) => UserConfig.GetText("delete_group") + grpName + onMgt,
                _ => "Request something"
            };
        }

        private async Task UpdateTicket(WfTicket ticket, ExternalRequest extReq)
        {
            List<int>? taskNumbers = null;
            if (!string.IsNullOrEmpty(extReq.ExtQueryVariables))
            {
                Dictionary<string, List<int>>? extQueryVars = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(extReq.ExtQueryVariables);
                extQueryVars?.TryGetValue(ExternalVarKeys.BundledTasks, out taskNumbers);
            }
            taskNumbers ??= [extReq.TaskNumber];
            foreach (var taskNumber in taskNumbers)
            {
                WfReqTask? updatedTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == taskNumber);
                if (updatedTask != null)
                {
                    string? extTicketIdInTask = updatedTask.GetAddInfoValue(AdditionalInfoKeys.ExtIcketId);
                    if (extReq.ExtTicketId != null && extReq.ExtTicketId != extTicketIdInTask)
                    {
                        await wfHandler.SetAddInfoInReqTask(updatedTask, AdditionalInfoKeys.ExtIcketId, extReq.ExtTicketId);
                    }
                    await UpdateTaskState(ticket, updatedTask, extReq.ExtRequestState);

                    if (extReq.ExtRequestState == ExtStates.ExtReqDone.ToString())
                    {
                        await LogRequestTasks([updatedTask], actSystem.Name, ModellingTypes.ChangeType.Implement);
                    }
                    else if (extReq.ExtRequestState == ExtStates.ExtReqRejected.ToString())
                    {
                        await LogRequestTasks([updatedTask], actSystem.Name, ModellingTypes.ChangeType.Reject, extReq.LastProcessingResponse ?? extReq.LastCreationResponse ?? "");
                    }
                }
                else
                {
                    Log.WriteError("UpdateTicket", $"Task not found in Ticket {ticket.Id}: {taskNumber}");
                }
            }
        }

        /// <summary>
        /// Sets a request task to the internal state of an external request state. The state only moves forward: a
        /// request task already beyond the mapped state, e.g. promoted to "To Implement" before it was handed to the
        /// external ticket system, keeps its state. A rejection is always set.
        /// </summary>
        /// <returns>true if the state of the task changed.</returns>
        private async Task<bool> UpdateTaskState(WfTicket ticket, WfReqTask reqTask, string extReqState)
        {
            int? internalStateId = extStateHandler?.GetInternalStateId(extReqState);

            if (internalStateId == null)
            {
                if (kOptionallyMappedStates.Contains(extReqState))
                {
                    return false;
                }

                throw new ArgumentException($"No translation defined for external state {extReqState}.");
            }

            if (reqTask.StateId == internalStateId)
            {
                return false;
            }
            if (internalStateId < reqTask.StateId && extReqState != ExtStates.ExtReqRejected.ToString())
            {
                Log.WriteDebug("External Request Update", $"Request task {reqTask.TaskNumber} of ticket {ticket.Id} keeps state {reqTask.StateId}; " +
                    $"external state {extReqState} maps to the lower state {internalStateId}.");
                return false;
            }
            wfHandler.SetTicketEnv(ticket);
            wfHandler.SetReqTaskEnv(reqTask);
            reqTask.StateId = internalStateId.Value;
            await wfHandler.PromoteReqTask(reqTask);
            return true;
        }

        private async Task Acknowledge(ExternalRequest extRequest)
        {
            try
            {
                var Variables = new
                {
                    id = extRequest.Id,
                    extRequestState = extRequest.ExtRequestState == ExtStates.ExtReqRejected.ToString() ?
                        ExtStates.ExtReqAckRejected.ToString() :
                        ExtStates.ExtReqAcknowledged.ToString(),
                    finishDate = DateTime.Now
                };
                await ApiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestFinal, Variables);
            }
            catch (Exception exception)
            {
                Log.WriteError("Acknowledge External Request", $"Runs into exception: ", exception);
            }
        }

        private async Task LogRequestTasks(List<WfReqTask> tasks, string? requester, ModellingTypes.ChangeType changeType, string? comment = null)
        {
            foreach (WfReqTask task in tasks)
            {
                (long objId, ModellingTypes.ModObjectType objType) = GetObject(task);
                await ModellingHandlerBase.LogChange(new LogChangeRequest
                {
                    ChangeType = changeType,
                    ObjectType = objType,
                    ObjectId = objId,
                    Text = $"{ConstructLogMessageText(changeType)} {task.Title} on {task.OnManagement?.Name}{(comment != null ? ", " + comment : "")}",
                    ApiConnection = ApiConnection,
                    UserConfig = UserConfig,
                    ApplicationId = task.Owners.FirstOrDefault()?.Owner.Id,
                    DisplayMessageInUi = DefaultInit.DoNothing,
                    Requester = requester
                });
            }
        }

        private static (long, ModellingTypes.ModObjectType) GetObject(WfReqTask task)
        {
            if (task.GetAddInfoLongValue(AdditionalInfoKeys.ConnId) != null)
            {
                return (task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) ?? 0, ModellingTypes.ModObjectType.Connection);
            }
            else if (task.GetAddInfoLongValue(AdditionalInfoKeys.AppRoleId) != null)
            {
                return (task.GetAddInfoIntValue(AdditionalInfoKeys.AppRoleId) ?? 0, ModellingTypes.ModObjectType.AppRole);
            }
            else if (task.GetAddInfoIntValue(AdditionalInfoKeys.SvcGrpId) != null)
            {
                return (task.GetAddInfoIntValue(AdditionalInfoKeys.SvcGrpId) ?? 0, ModellingTypes.ModObjectType.ServiceGroup);
            }
            return (0, ModellingTypes.ModObjectType.Connection);
        }

        private static string ConstructLogMessageText(ModellingTypes.ChangeType changeType)
        {
            return changeType switch
            {
                ModellingTypes.ChangeType.Request => "Requested",
                ModellingTypes.ChangeType.Implement => "Implemented",
                ModellingTypes.ChangeType.Reject => "Rejected",
                _ => "",
            };
        }


        /// <summary>
        /// Dispose method to clean up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // UserConfig is caller-owned and can be reused across multiple request handling steps.
                    // Disposing it here breaks subsequent handler instances that receive the same config.
                }
                disposed = true;
            }
        }
    }
}
