using FWO.Api.Client;
using FWO.Api.Client.Queries;
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
    public class ExternalRequestHandler : IDisposable
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

        /// <summary>
        /// constructor for object with all data necessary for request handling
        /// </summary>
        public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection)
        {
            ApiConnection = apiConnection;
            UserConfig = userConfig;
            extStateHandler = new(apiConnection);
            Task.Run(GetInternalGroups).Wait();
            wfHandler = new(userConfig, apiConnection, WorkflowPhases.request, ownerGroups, new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection));
        }

        /// <summary>
        /// constructor only for unit testing
        /// </summary>
        public ExternalRequestHandler(UserConfig userConfig, ApiConnection apiConnection, List<UserGroup>? userGroups)
        {
            ApiConnection = apiConnection;
            UserConfig = userConfig;
            extStateHandler = new(apiConnection);
            wfHandler = new(userConfig, apiConnection, WorkflowPhases.request, userGroups, new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection));
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

            bool handledTask = false;
            bool handledInternalWork = false;

            while (true)
            {
                WfReqTask? nextTask = ticket.Tasks.FirstOrDefault(ta => ta.TaskNumber == lastTaskNumber + 1);
                if (nextTask is null)
                {
                    Log.WriteDebug("CreateNextRequest", "No more task found.");
                    return handledTask;
                }

                if (handledInternalWork && !IsInternalWorkConfiguredForTask(nextTask))
                {
                    Log.WriteInfo("CreateNextRequest", $"Internal work batch for ticket {ticket.Id} created. Waiting for completion before task {nextTask.TaskNumber}.");
                    return true;
                }

                List<ManagementFwConfigChangeState> managementSettings = JsonSerializer.Deserialize<List<ManagementFwConfigChangeState>>(UserConfig.FwConfigChangeMgmSettings) ?? new();

                List<ExternalTicketSystem> extTicketSystems = JsonSerializer.Deserialize<List<ExternalTicketSystem>>(UserConfig.ExtTicketSystems) ?? new();
                GetExtSystemFromTask(nextTask, managementSettings, extTicketSystems);

                if (actInternalWork)
                {
                    await PromoteInternalWorkTaskToPlanning(ticket, nextTask);
                    Log.WriteInfo("CreateNextRequest", $"Promoted internal work task {nextTask.TaskNumber} for ticket {ticket.Id} to planning.");

                    handledTask = true;
                    handledInternalWork = true;
                    lastTaskNumber = nextTask.TaskNumber;
                    oldRequest = null;
                    ticket = await wfHandler.ResolveTicket(ticket.Id) ?? ticket;
                    continue;
                }

                int waitCycles = GetWaitCycles(nextTask.TaskType, oldRequest);
                if (nextTask.TaskType == WfTaskType.access.ToString() || nextTask.TaskType == WfTaskType.rule_modify.ToString() || nextTask.TaskType == WfTaskType.rule_delete.ToString())
                {
                    List<WfReqTask> bundledTasks = [];
                    List<WfReqTask> handledTasks = [nextTask];
                    BundleTasks(ticket, lastTaskNumber, nextTask, bundledTasks, handledTasks, managementSettings, extTicketSystems);
                    await CreateExtRequest(ticket, bundledTasks, handledTasks, waitCycles);
                }
                else
                {
                    await CreateExtRequest(ticket, [nextTask], [nextTask], waitCycles);
                }

                Log.WriteInfo("CreateNextRequest", $"Created Request for ticket {ticket.Id}.");
                return true;
            }
        }

        private bool IsInternalWorkConfiguredForTask(WfReqTask task)
        {
            try
            {
                string changeCategory = GetChangeCategory(task);

                var managementSettings = JsonSerializer.Deserialize<List<ManagementFwConfigChangeState>>(UserConfig.FwConfigChangeMgmSettings) ?? [];
                ManagementFwConfigChangeState? managementSetting = managementSettings.FirstOrDefault(m => m.Id == task.ManagementId);

                return managementSetting?.Enabled == true
                    && managementSetting.SelectedChanges.TryGetValue(changeCategory, out string? selectedSystemValue)
                    && selectedSystemValue == ManagementFwConfigChangeTargets.InternalWork;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsInternalWorkTask(WfReqTask task)
        {
            return task.GetAddInfoValue(AdditionalInfoKeys.FwConfigChangeTarget) == ManagementFwConfigChangeTargets.InternalWork;
        }


        /// <summary>
        /// Continues the external request chain after an internal work request task has completed.
        /// </summary>
        /// <param name="ticketId">The ID of the workflow ticket that contains the internal work task.</param>
        /// <param name="reqTaskId">The ID of the completed internal work request task.</param>
        /// <returns>
        /// <c>true</c> if the request chain was continued or a following task was started; otherwise <c>false</c>.
        /// </returns>
        public async Task<bool> ContinueAfterInternalWorkCompletion(long ticketId, long reqTaskId)
        {
            WfTicket? ticket = await InitAndResolve(ticketId);
            if (ticket == null)
            {
                return false;
            }

            WfReqTask? changedTask = ticket.Tasks.FirstOrDefault(task => task.Id == reqTaskId);
            if (changedTask == null || !IsInternalWorkTask(changedTask))
            {
                return false;
            }

            List<WfReqTask> batch = GetInternalWorkBatch(ticket, changedTask);
            if (batch.Count == 0 || !await InternalWorkBatchIsCompleted(batch))
            {
                return false;
            }

            int lastInternalTaskNumber = batch.Max(task => task.TaskNumber);
            Log.WriteInfo("Internal Work", $"Internal work batch for ticket {ticket.Id} completed through task {lastInternalTaskNumber}. Continuing external request chain.");

            return await CreateNextRequest(ticket, lastInternalTaskNumber, null);
        }

        private static List<WfReqTask> GetInternalWorkBatch(WfTicket ticket, WfReqTask task)
        {
            List<WfReqTask> orderedTasks = [.. ticket.Tasks.OrderBy(task => task.TaskNumber)];
            int taskIndex = orderedTasks.FindIndex(candidate => candidate.Id == task.Id);
            if (taskIndex < 0 || !IsInternalWorkTask(orderedTasks[taskIndex]))
            {
                return [];
            }

            int firstIndex = taskIndex;
            while (firstIndex > 0 && IsInternalWorkTask(orderedTasks[firstIndex - 1]))
            {
                firstIndex--;
            }

            int lastIndex = taskIndex;
            while (lastIndex + 1 < orderedTasks.Count && IsInternalWorkTask(orderedTasks[lastIndex + 1]))
            {
                lastIndex++;
            }

            return orderedTasks.GetRange(firstIndex, lastIndex - firstIndex + 1);
        }

        private async Task<bool> InternalWorkBatchIsCompleted(List<WfReqTask> batch)
        {
            WfHandler implementationHandler = new(UserConfig, ApiConnection, WorkflowPhases.implementation, ownerGroups,
                new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection));

            if (!await implementationHandler.Init())
            {
                throw new InvalidOperationException("Could not initialize implementation workflow handler.");
            }

            foreach (WfReqTask task in batch)
            {
                if (IsFailedInternalWorkState(task.StateId))
                {
                    Log.WriteWarning("Internal Work", $"Internal work task {task.Id} in ticket {task.TicketId} reached failure state {task.StateId}. Request chain will not continue.");
                    return false;
                }

                if (task.StateId < implementationHandler.StateMatrix(task.TaskType).LowestEndState)
                {
                    return false;
                }
            }

            return true;
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

        private async Task PromoteInternalWorkTaskToPlanning(WfTicket ticket, WfReqTask task)
        {
            WfHandler planningHandler = new(UserConfig, ApiConnection, WorkflowPhases.planning, ownerGroups, new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection));

            if (!await planningHandler.Init())
            {
                throw new InvalidOperationException("Could not initialize planning workflow handler.");
            }

            WfTicket planningTicket = await planningHandler.ResolveTicket(ticket.Id) ?? throw new InvalidOperationException($"Ticket {ticket.Id} not found.");

            WfReqTask planningTask = planningTicket.Tasks.FirstOrDefault(ta => ta.TaskNumber == task.TaskNumber) ?? throw new InvalidOperationException($"Task {task.TaskNumber} not found in ticket {ticket.Id}.");

            StateMatrix planningMatrix = planningHandler.StateMatrix(planningTask.TaskType);
            planningTask.StateId = planningMatrix.LowestInputState;

            planningHandler.SetTicketEnv(planningTicket);
            planningHandler.SetReqTaskEnv(planningTask);

            await planningHandler.SetAddInfoInReqTask(planningTask, AdditionalInfoKeys.FwConfigChangeTarget, ManagementFwConfigChangeTargets.InternalWork);

            if (!IsInternalWorkTask(planningTask))
            {
                throw new InvalidOperationException($"Internal work marker could not be set for task {task.TaskNumber} in ticket {ticket.Id}.");
            }

            await planningHandler.PromoteReqTask(planningTask);

            await LogRequestTasks([planningTask], ticket.Requester?.Name, ModellingTypes.ChangeType.Request);
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
                    await UpdateTaskState(furtherTask, ExtStates.ExtReqRejected.ToString());
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
            ModellingNamingConvention? namingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(UserConfig.ModNamingConvention);
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
                    await UpdateTaskState(updatedTask, extReq.ExtRequestState);

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

        private async Task UpdateTaskState(WfReqTask reqTask, string extReqState)
        {
            if (extStateHandler != null && reqTask.StateId != extStateHandler.GetInternalStateId(extReqState))
            {
                wfHandler.SetReqTaskEnv(reqTask);
                reqTask.StateId = extStateHandler.GetInternalStateId(extReqState) ?? throw new ArgumentException("No translation defined for external state.");
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

        private bool IsFailedInternalWorkState(int stateId)
        {
            List<int?> failedStates =
            [
                extStateHandler?.GetInternalStateId(ExtStates.Rejected),
                extStateHandler?.GetInternalStateId(ExtStates.ExtReqRejected),
                extStateHandler?.GetInternalStateId(ExtStates.ExtReqAckRejected),
                extStateHandler?.GetInternalStateId(ExtStates.ExtReqDiscarded)
            ];

            return failedStates.Any(failedState => failedState == stateId);
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
