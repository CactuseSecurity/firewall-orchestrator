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
using FWO.ExternalSystems.CheckPoint;
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
        private bool actInternalWork = false;

        private enum CreateNextRequestResult
        {
            Continue,
            ReturnTrue,
            ReturnHandledTask
        }

        private sealed class CreateNextRequestState
        {
            public WfTicket Ticket { get; set; } = new();
            public ExternalRequest? OldRequest { get; set; }
            public int LastTaskNumber { get; set; }
            public bool HandledTask { get; set; }
            public bool HandledInternalWork { get; set; }
        }

        /// <summary>
        /// constructor for object with all data necessary for request handling
        /// </summary>
        public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection)
        {
            ApiConnection = apiConnection;
            UserConfig = userConfig;
            extStateHandler = new(apiConnection);
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
            wfHandler = new(userConfig, apiConnection, WorkflowPhases.request, userGroups,
                new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection))
            { SystemContext = true };
        }

        /// <summary>
        /// send the first request from ticket (called by UI via middleware client)
        /// may also be a higher task number in case of a reinit
        /// </summary>
        public async Task<bool> SendFirstRequest(long ticketId)
        {
            try
            {
                WfTicket? intTicket = await InitAndResolve(ticketId);
                if (intTicket == null || intTicket.Tasks.Count == 0)
                {
                    return false;
                }

                int lastFinishedTask = 0;
                List<WfReqTask> orderedTasks = [.. intTicket.Tasks.OrderBy(t => t.TaskNumber)];

                int taskIndex = 0;
                while (taskIndex < orderedTasks.Count)
                {
                    WfReqTask task = orderedTasks[taskIndex];

                    if (IsInternalWorkTask(task))
                    {
                        List<WfReqTask> batch = GetInternalWorkBatch(intTicket, task);
                        if (batch.Count == 0)
                        {
                            break;
                        }

                        if (!await InternalWorkBatchIsCompleted(batch))
                        {
                            Log.WriteInfo("SendFirstRequest",
                                $"Ticket {ticketId}: internal work batch starting at task {batch.Min(t => t.TaskNumber)} is not completed yet. Reinit stops here.");
                            return true;
                        }

                        lastFinishedTask = batch.Max(t => t.TaskNumber);
                        taskIndex += batch.Count;
                        continue;
                    }

                    if (task.StateId > wfHandler.StateMatrix(task.TaskType).LowestEndState)
                    {
                        lastFinishedTask = task.TaskNumber;
                        taskIndex++;
                        continue;
                    }

                    break;
                }

                return await CreateNextRequest(intTicket, lastFinishedTask);
            }
            catch (Exception exception)
            {
                Log.WriteError("External Request Creation", $"Runs into exception: ", exception);
                return false;
            }
        }

        /// <summary>
        /// send the next request from ticket if last is done and not rejected
        /// (called by scheduler after state change)
        /// </summary>
        public async Task HandleStateChange(ExternalRequest externalRequest)
        {
            WfTicket? intTicket = await InitAndResolve(externalRequest.TicketId);
            if (intTicket == null)
            {
                Log.WriteError("External Request Update", $"Ticket not found.");
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
                        await RejectFollowingTasks(intTicket, externalRequest.TaskNumber);
                        Log.WriteInfo($"External Request {externalRequest.Id} rejected", $"Reject Following Tasks for internal ticket {intTicket.Id}");
                    }
                    else
                    {
                        await CreateNextRequest(intTicket, externalRequest.TaskNumber, externalRequest);
                    }
                }
            }
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
        /// get number of last processed request task (public only for unit testing)
        /// </summary>
        /// <param name="extQueryVars"></param>
        /// <param name="oldTaskNumber"></param>
        /// <returns></returns>
        public static int GetLastTaskNumber(string extQueryVars, int oldTaskNumber)
        {
            List<int>? taskNumbers = null;
            Dictionary<string, List<int>>? extQueryVarDict = JsonSerializer.Deserialize<Dictionary<string, List<int>>?>(extQueryVars);
            extQueryVarDict?.TryGetValue(ExternalVarKeys.BundledTasks, out taskNumbers);
            if (taskNumbers != null && taskNumbers.Count > 0)
            {
                return taskNumbers[^1];
            }
            else
            {
                return oldTaskNumber;
            }
        }

        /// <summary>
        /// create next external request from internal ticket task list (public only for unit testing)
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="oldTaskNumber"></param>
        /// <param name="oldRequest"></param>
        /// <returns></returns>

        public async Task<bool> CreateNextRequest(WfTicket ticket, int oldTaskNumber, ExternalRequest? oldRequest = null)
        {
            int lastTaskNumber = UserConfig.ModRolloutBundleTasks && oldRequest != null && oldRequest.ExtQueryVariables != "" ?
                GetLastTaskNumber(oldRequest.ExtQueryVariables, oldTaskNumber) : oldTaskNumber;
            WorkflowEmailBundleCollector emailBundleCollector = new();
            CreateNextRequestState state = new()
            {
                Ticket = ticket,
                OldRequest = oldRequest,
                LastTaskNumber = lastTaskNumber
            };

            try
            {
                while (true)
                {
                    CreateNextRequestResult result = await ProcessNextRequestTask(state, emailBundleCollector);
                    if (result == CreateNextRequestResult.Continue)
                    {
                        continue;
                    }
                    return result switch
                    {
                        CreateNextRequestResult.ReturnHandledTask => state.HandledTask,
                        _ => true
                    };
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("CreateNextRequest",
                    $"Create next request failed for ticket {state.Ticket.Id}. Trying to flush pending internal work emails before rethrowing.",
                    exception);
                try
                {
                    await RunInternalWorkStateChangeActionsSafe(state.Ticket.Id, emailBundleCollector);
                }
                catch (Exception flushException)
                {
                    Log.WriteError("CreateNextRequest",
                        $"Flush of pending internal work emails also failed for ticket {state.Ticket.Id}.",
                        flushException);
                }

                throw;
            }
        }

        private async Task<CreateNextRequestResult> ProcessNextRequestTask(CreateNextRequestState state, WorkflowEmailBundleCollector emailBundleCollector)
        {
            WfReqTask? nextTask = state.Ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == state.LastTaskNumber + 1);
            if (nextTask is null)
            {
                Log.WriteDebug("CreateNextRequest", "No more task found.");
                await RunInternalWorkStateChangeActionsSafe(state.Ticket.Id, emailBundleCollector);
                return CreateNextRequestResult.ReturnHandledTask;
            }

            if (state.HandledInternalWork && !IsInternalWorkConfiguredForTask(nextTask))
            {
                Log.WriteInfo("CreateNextRequest", $"Internal work batch for ticket {state.Ticket.Id} created. Waiting for completion before task {nextTask.TaskNumber}.");
                await RunInternalWorkStateChangeActionsSafe(state.Ticket.Id, emailBundleCollector);
                return CreateNextRequestResult.ReturnTrue;
            }

            List<ManagementFwConfigChangeState> managementSettings = JsonSerializer.Deserialize<List<ManagementFwConfigChangeState>>(UserConfig.FwConfigChangeMgmSettings) ?? new();
            List<ExternalTicketSystem> extTicketSystems = JsonSerializer.Deserialize<List<ExternalTicketSystem>>(UserConfig.ExtTicketSystems) ?? new();
            GetExtSystemFromTask(nextTask, managementSettings, extTicketSystems);

            if (actInternalWork)
            {
                WorkflowPhases internalWorkPhase = await PromoteInternalWorkTaskToApproval(state.Ticket, nextTask, emailBundleCollector);
                Log.WriteInfo("CreateNextRequest", $"Promoted internal work task {nextTask.TaskNumber} for ticket {state.Ticket.Id} to {internalWorkPhase}.");
                state.HandledTask = true;
                state.HandledInternalWork = true;
                state.LastTaskNumber = nextTask.TaskNumber;
                state.OldRequest = null;
                state.Ticket = await wfHandler.ResolveTicket(state.Ticket.Id) ?? state.Ticket;
                return CreateNextRequestResult.Continue;
            }

            int waitCycles = GetWaitCycles(nextTask.TaskType, state.OldRequest);
            if (nextTask.TaskType == WfTaskType.access.ToString() || nextTask.TaskType == WfTaskType.rule_modify.ToString() || nextTask.TaskType == WfTaskType.rule_delete.ToString())
            {
                List<WfReqTask> bundledTasks = [];
                List<WfReqTask> handledTasks = [nextTask];
                BundleTasks(state.Ticket, state.LastTaskNumber, nextTask, bundledTasks, handledTasks, managementSettings, extTicketSystems);
                await CreateExtRequest(state.Ticket, bundledTasks, handledTasks, waitCycles);
            }
            else
            {
                await CreateExtRequest(state.Ticket, [nextTask], [nextTask], waitCycles);
            }

            Log.WriteInfo("CreateNextRequest", $"Created Request for ticket {state.Ticket.Id}.");
            await RunInternalWorkStateChangeActionsSafe(state.Ticket.Id, emailBundleCollector);
            return CreateNextRequestResult.ReturnTrue;
        }


        private void BundleTasks(WfTicket ticket, int lastTaskNumber, WfReqTask nextTask, List<WfReqTask> bundledTasks, List<WfReqTask> handledTasks, List<ManagementFwConfigChangeState> managementSettings, List<ExternalTicketSystem> extTicketSystems)
        {
            int actTaskNumber = lastTaskNumber + 2;
            bool taskFound = true;
            WfReqTask actBundledTask = nextTask;

            int startSystemId = actSystem.Id;

            while (taskFound && bundledTasks.Count < actSystem.MaxBundledTasks())
            {
                WfReqTask? furtherTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == actTaskNumber);
                if (furtherTask != null && furtherTask.TaskType == nextTask.TaskType && CanBundleWithStartTask(furtherTask, nextTask, startSystemId, managementSettings, extTicketSystems))
                {
                    taskFound = HandleFurtherTask(furtherTask, nextTask.TaskType, ref actBundledTask, bundledTasks, handledTasks);

                    actTaskNumber++;
                }
                else
                {
                    bundledTasks.Add(actBundledTask);
                    taskFound = false;
                }
            }
        }


        private static bool CanBundleWithStartTask(WfReqTask furtherTask, WfReqTask startTask, int startSystemId, List<ManagementFwConfigChangeState> managementSettings, List<ExternalTicketSystem> extTicketSystems)
        {
            if (GetChangeCategory(furtherTask) != GetChangeCategory(startTask))
            {
                return false;
            }

            return TryResolveExtSystemForTask(furtherTask, managementSettings, extTicketSystems, out ExternalTicketSystem? furtherSystem)
                   && furtherSystem != null
                   && furtherSystem.Id == startSystemId;
        }

        private static bool TryResolveExtSystemForTask(WfReqTask task, List<ManagementFwConfigChangeState> managementSettings, List<ExternalTicketSystem> extTicketSystems, out ExternalTicketSystem? system)
        {
            system = null;

            try
            {
                system = ResolveExtSystemForTask(task, managementSettings, extTicketSystems);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static ExternalTicketSystem ResolveExtSystemForTask(WfReqTask task, List<ManagementFwConfigChangeState> managementSettings, List<ExternalTicketSystem> extTicketSystems)
        {
            ArgumentNullException.ThrowIfNull(task);

            ManagementFwConfigChangeState managementSetting =
                managementSettings.FirstOrDefault(m => m.Id == task.ManagementId)
                ?? throw new InvalidOperationException($"No matching config item found for management {task.ManagementId}.");

            if (!managementSetting.Enabled)
            {
                throw new InvalidOperationException($"External workflow is disabled for management {task.ManagementId}.");
            }

            string changeCategory = GetChangeCategory(task);

            if (!managementSetting.SelectedChanges.TryGetValue(changeCategory, out string? selectedSystemValue) || string.IsNullOrWhiteSpace(selectedSystemValue) || selectedSystemValue == ManagementFwConfigChangeTargets.Disabled)
            {
                throw new InvalidOperationException(
                    $"No external ticket system configured for management {task.ManagementId} and category '{changeCategory}'.");
            }

            if (!int.TryParse(selectedSystemValue, out int externalTicketSystemId))
            {
                throw new InvalidOperationException(
                    $"Configured external ticket system id '{selectedSystemValue}' for management {task.ManagementId} and category '{changeCategory}' is invalid.");
            }

            return extTicketSystems.FirstOrDefault(s => s.Id == externalTicketSystemId)
                ?? throw new InvalidOperationException($"No matching external ticket system found for id {externalTicketSystemId}.");
        }

        private bool HandleFurtherTask(WfReqTask furtherTask, string actTaskType, ref WfReqTask actBundledTask, List<WfReqTask> bundledTasks, List<WfReqTask> handledTasks)
        {
            if (actSystem.BundleGateways() && actSystem.TaskTypesToBundleGateways().Contains(actTaskType) && IsSameRuleOnDiffGw(actBundledTask, furtherTask))
            {
                actBundledTask.Elements.AddRange(furtherTask.GetRuleElements().ConvertAll(e => e.ToReqElement()));
                handledTasks.Add(furtherTask);
            }
            else
            {
                bundledTasks.Add(actBundledTask);
                if (UserConfig.ModRolloutBundleTasks)
                {
                    actBundledTask = new(furtherTask);
                    handledTasks.Add(furtherTask);
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// qad heuristic for Tufin SC (public only for unit testing)
        /// </summary>
        /// <param name="taskType"></param>
        /// <param name="oldRequest"></param>
        /// <returns></returns>
        public int GetWaitCycles(string taskType, ExternalRequest? oldRequest)
        {
            // TODO: to be refined
            if (oldRequest != null && UserConfig.ExternalRequestWaitCycles > 0 &&
                // last request handled group
                (oldRequest.ExtRequestType == "(NetworkObjectModify, CREATE)" || oldRequest.ExtRequestType == "(NetworkObjectModify, UPDATE)") &&
                    // now access request
                    (taskType == WfTaskType.access.ToString() ||
                    // or last request created new objects in group
                    ContainsNewObj(oldRequest.ExtRequestContent)))
            {
                return UserConfig.ExternalRequestWaitCycles;
            }
            return 0;
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


        private async Task CreateExtRequest(WfTicket ticket, List<WfReqTask> tasks, List<WfReqTask> handledTasks, int waitCycles)
        {
            string taskContent = await ConstructContent(tasks, ticket.Requester);
            Dictionary<string, List<int>> handledTaskNumbers = BuildExtQueryVariables(tasks, handledTasks);
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
                waitCycles = waitCycles
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

        private async Task RejectFollowingTasks(WfTicket ticket, int lastTaskNumber)
        {
            int actTaskNumber = lastTaskNumber + 1;
            bool taskFound = true;
            while (taskFound)
            {
                WfReqTask? furtherTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == actTaskNumber);
                if (furtherTask != null)
                {
                    await UpdateTaskState(ticket, furtherTask, ExtStates.ExtReqRejected.ToString());
                    actTaskNumber++;
                }
                else
                {
                    taskFound = false;
                }
            }
        }

        private void GetExtSystemFromTask(WfReqTask task, List<ManagementFwConfigChangeState> managementSettings, List<ExternalTicketSystem> extTicketSystems)
        {
            actInternalWork = false;

            ArgumentNullException.ThrowIfNull(task);

            ManagementFwConfigChangeState managementSetting = managementSettings.FirstOrDefault(m => m.Id == task.ManagementId)
                ?? throw new InvalidOperationException($"No matching config item found for management {task.ManagementId}.");

            if (!managementSetting.Enabled)
            {
                throw new InvalidOperationException($"External workflow is disabled for management {task.ManagementId}.");
            }

            string changeCategory = GetChangeCategory(task);

            if (!managementSetting.SelectedChanges.TryGetValue(changeCategory, out string? selectedSystemValue)
                || string.IsNullOrWhiteSpace(selectedSystemValue)
                || selectedSystemValue == ManagementFwConfigChangeTargets.Disabled)
            {
                throw new InvalidOperationException(
                    $"No external ticket system configured for management {task.ManagementId} and category '{changeCategory}'.");
            }

            if (selectedSystemValue == ManagementFwConfigChangeTargets.InternalWork)
            {
                if (changeCategory != ManagementFwConfigChangeCategories.RuleChanges)
                {
                    throw new InvalidOperationException("Internal work is only supported for rule changes.");
                }

                actInternalWork = true;
                actTaskType = "";
                actSystem = new ExternalTicketSystem
                {
                    Name = "Internal work",
                    TypeId = BuiltInExternalTicketSystemTypes.GenericId
                };
                return;
            }

            if (!int.TryParse(selectedSystemValue, out int externalTicketSystemId))
            {
                throw new InvalidOperationException(
                    $"Configured external ticket system id '{selectedSystemValue}' for management {task.ManagementId} and category '{changeCategory}' is invalid.");
            }

            ExternalTicketSystem system = extTicketSystems.FirstOrDefault(s => s.Id == externalTicketSystemId)
                ?? throw new InvalidOperationException($"No matching external ticket system found for id {externalTicketSystemId}.");

            actSystem = system;
        }

        private async Task<string> ConstructContent(List<WfReqTask> reqTasks, UiUser? requester)
        {
            ExternalTicket ticket = ExternalTicketFactory.Create(actSystem);
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

        private async Task UpdateTaskState(WfTicket ticket, WfReqTask reqTask, string extReqState)
        {
            int? internalStateId = extStateHandler?.GetInternalStateId(extReqState);

            if (internalStateId == null)
            {
                if (extReqState == ExtStates.ExtReqRequested.ToString())
                {
                    return;
                }

                throw new ArgumentException($"No translation defined for external state {extReqState}.");
            }

            if (reqTask.StateId != internalStateId)
            {
                wfHandler.SetTicketEnv(ticket);
                wfHandler.SetReqTaskEnv(reqTask);
                reqTask.StateId = internalStateId.Value;
                await wfHandler.PromoteReqTask(reqTask);
            }
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

        private static string GetChangeCategory(WfReqTask task)
        {
            return task.TaskType switch
            {
                nameof(WfTaskType.group_create) => ManagementFwConfigChangeCategories.ObjectChanges,
                nameof(WfTaskType.group_modify) => ManagementFwConfigChangeCategories.ObjectChanges,
                nameof(WfTaskType.group_delete) => ManagementFwConfigChangeCategories.ObjectChanges,

                nameof(WfTaskType.access) => ManagementFwConfigChangeCategories.RuleChanges,
                nameof(WfTaskType.rule_modify) => ManagementFwConfigChangeCategories.RuleChanges,
                nameof(WfTaskType.rule_delete) => ManagementFwConfigChangeCategories.RuleChanges,

                _ => throw new InvalidOperationException($"Unsupported workflow task type '{task.TaskType}'.")
            };
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


        private static void LogMessage(Exception? exception = null, string title = "", string message = "", bool ErrorFlag = false)
        {
            if (exception == null)
            {
                if (ErrorFlag)
                {
                    Log.WriteWarning(title, message);
                }
                else
                {
                    Log.WriteInfo(title, message);
                }
            }
            else
            {
                Log.WriteError(title, message, exception);
            }
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
