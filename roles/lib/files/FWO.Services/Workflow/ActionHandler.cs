using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Logging;
using RestSharp;
using System.Text.Json;


namespace FWO.Services.Workflow
{
    public partial class ActionHandler
    {
        private List<WfState> states = [];
        private readonly ApiConnection apiConnection;
        private readonly WfHandler wfHandler;
        private readonly bool useInMwServer = false;
        private readonly IRequestedRulePolicyChecker? requestedRulePolicyChecker;
        private readonly IWorkflowRecipientResolver? workflowRecipientResolver;
        private string? ScopedUserTo { get; set; } = "";
        private string? ScopedUserCc { get; set; } = "";
        private string? ScopedUserBcc { get; set; } = "";
        private string? ScopedUserEmailTo { get; set; } = "";
        private string? ScopedUserEmailCc { get; set; } = "";
        private string? ScopedUserEmailBcc { get; set; } = "";
        private readonly List<UserGroup>? UserGroups = [];
        private readonly string NoAuthUser = "No Auth User";
        private static readonly object MiddlewareDelegationLock = new();
        private static readonly Dictionary<string, DateTime> MiddlewareDelegations = [];
        private static readonly TimeSpan MiddlewareDelegationDeduplicationWindow = TimeSpan.FromSeconds(5);


        public ActionHandler(ApiConnection apiConnection, WfHandler wfHandler, List<UserGroup>? userGroups = null, bool useInMwServer = false,
            IRequestedRulePolicyChecker? requestedRulePolicyChecker = null, IWorkflowRecipientResolver? workflowRecipientResolver = null)
        {
            this.apiConnection = apiConnection;
            this.wfHandler = wfHandler;
            this.useInMwServer = useInMwServer;
            UserGroups = userGroups;
            this.requestedRulePolicyChecker = requestedRulePolicyChecker ?? wfHandler.RequestedRulePolicyChecker;
            this.workflowRecipientResolver = workflowRecipientResolver ?? wfHandler.WorkflowRecipientResolver;
        }

        public async Task Init()
        {
            states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
        }

        public List<WfStateAction> GetOfferedActions(WfStatefulObject statefulObject, WfObjectScopes scope, WorkflowPhases phase)
        {
            List<WfStateAction> offeredActions = [];
            List<WfStateAction> stateActions = GetRelevantActions(statefulObject, scope);
            foreach (var action in stateActions.Where(x => x.Event == StateActionEvents.OfferButton.ToString()))
            {
                if (action.Phase == "" || action.Phase == phase.ToString())
                {
                    offeredActions.Add(action);
                }
            }
            return offeredActions;
        }

        public async Task DoStateChangeActions(WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner = null, long? ticketId = null, string? userGrpDn = null)
        {
            if (!statefulObject.StateChanged())
            {
                return;
            }

            Log.WriteDebug("DoStateChangeActions", $"State changed for {scope} from {statefulObject.ChangedFrom()} to {statefulObject.StateId}.");
            if (!useInMwServer && wfHandler.MiddlewareClient != null)
            {
                try
                {
                    await ExecuteInMiddleware(BuildWorkflowActionParameters(statefulObject, scope, ticketId));
                }
                finally
                {
                    statefulObject.ResetStateChanged();
                }
                return;
            }

            List<WfStateAction> onSetActions = StateActionsForEvent(statefulObject, scope, StateActionEvents.OnSet, true);
            List<WfStateAction> onLeaveActions = StateActionsForEvent(statefulObject, scope, StateActionEvents.OnLeave, false);
            statefulObject.ResetStateChanged();

            await PerformStateActions(onSetActions, StateActionEvents.OnSet, statefulObject, scope, owner, ticketId, userGrpDn);
            await PerformStateActions(onLeaveActions, StateActionEvents.OnLeave, statefulObject, scope, owner, ticketId, userGrpDn);
        }

        private List<WfStateAction> StateActionsForEvent(WfStatefulObject statefulObject, WfObjectScopes scope, StateActionEvents actionEvent, bool currentState)
        {
            return [.. GetRelevantActions(statefulObject, scope, currentState).Where(action => action.Event == actionEvent.ToString())];
        }

        private async Task PerformStateActions(List<WfStateAction> actions, StateActionEvents actionEvent, WfStatefulObject statefulObject,
            WfObjectScopes scope, FwoOwner? owner, long? ticketId, string? userGrpDn)
        {
            foreach (var action in actions.Where(IsActionInCurrentPhase))
            {
                string stateText = actionEvent == StateActionEvents.OnLeave ? statefulObject.ChangedFrom().ToString() : statefulObject.StateId.ToString();
                Log.WriteDebug("DoStateChangeActions", $"Perform {actionEvent} action '{action.Name}' ({action.ActionType}) for {scope} state {stateText}.");
                await PerformAction(action, statefulObject, scope, owner, ticketId, userGrpDn);
            }
        }

        private bool IsActionInCurrentPhase(WfStateAction action)
        {
            return action.Phase == "" || action.Phase == wfHandler.Phase.ToString();
        }

        public async Task DoOwnerChangeActions(WfStatefulObject statefulObject, FwoOwner? owner, long ticketId)
        {
            List<WfStateAction> ownerChangeActions = GetRelevantActions(statefulObject, WfObjectScopes.None);
            foreach (var action in ownerChangeActions.Where(x => x.Event == StateActionEvents.OwnerChange.ToString()))
            {
                await PerformAction(action, statefulObject, WfObjectScopes.None, owner, ticketId);
            }
        }

        public async Task DoOnAssignmentActions(WfStatefulObject statefulObject, WfObjectScopes scope, string? userGrpDn)
        {
            List<WfStateAction> assignmentActions = GetAssignmentActions(statefulObject, scope);
            foreach (var action in assignmentActions.Where(x => x.Event == StateActionEvents.OnAssignment.ToString()))
            {
                await PerformAction(action, statefulObject, scope, null, null, userGrpDn);
            }
        }

        private List<WfStateAction> GetAssignmentActions(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            List<WfStateAction> actions = GetRelevantActions(statefulObject, scope);
            actions.AddRange(GetRelevantActions(statefulObject, WfObjectScopes.None).Where(action => actions.All(existing => !IsSamePersistedAction(existing, action))));
            return actions;
        }

        private static bool IsSamePersistedAction(WfStateAction firstAction, WfStateAction secondAction)
        {
            return firstAction.Id > 0 && firstAction.Id == secondAction.Id;
        }

        public async Task PerformAction(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope,
            FwoOwner? owner = null, long? ticketId = null, string? userGrpDn = null)
        {
            if (scope != WfObjectScopes.None && !useInMwServer && wfHandler.MiddlewareClient != null && !WfStateAction.IsReadonlyType(action.ActionType))
            {
                Log.WriteDebug("PerformAction", $"Delegating action '{action.Name}' ({action.ActionType}) to middleware.");
                await ExecuteInMiddleware(BuildWorkflowActionParameters(statefulObject, scope, ticketId, action.Id));
                return;
            }

            switch (action.ActionType)
            {
                case nameof(StateActionTypes.AutoPromote):
                    int? toState = await GetAutoPromoteTargetState(action.ExternalParams, statefulObject, scope);
                    if (toState == null || states.FirstOrDefault(x => x.Id == toState) != null)
                    {
                        await wfHandler.AutoPromote(statefulObject, scope, toState);
                    }
                    break;
                case nameof(StateActionTypes.AddApproval):
                    await SetScope(statefulObject, scope);
                    await wfHandler.AddApproval(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.SetAlert):
                    await SetAlert(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.TrafficPathAnalysis):
                    await SetScope(statefulObject, scope);
                    await wfHandler.HandlePathAnalysisAction(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.ExternalCall):
                    await CallExternal(action);
                    break;
                case nameof(StateActionTypes.SendEmail):
                    await SendEmail(action, statefulObject, scope, owner, userGrpDn);
                    break;
                case nameof(StateActionTypes.CreateFlow):
                    await CreateFlow(action, statefulObject, scope, owner, ticketId);
                    break;
                case nameof(StateActionTypes.BundleTasks):
                    await BundleTasks(action, statefulObject, scope, owner, ticketId);
                    break;
                case nameof(StateActionTypes.UpdateConnectionOwner):
                    await UpdateConnectionOwner(owner, ticketId);
                    break;
                case nameof(StateActionTypes.UpdateConnectionRelease):
                    await UpdateConnectionPublish(owner, ticketId);
                    break;
                case nameof(StateActionTypes.UpdateConnectionReject):
                    await UpdateConnectionReject(owner, ticketId);
                    break;
                case nameof(StateActionTypes.DisplayConnection):
                    await DisplayConnection(statefulObject, scope);
                    break;
                case nameof(StateActionTypes.UpdateModelling):
                    await SetScope(statefulObject, scope);
                    UpdateModellingActionParams updateModellingParams = UpdateModellingActionParams.FromExternalParams(action.ExternalParams);
                    int updatedModellingObjects = await UpdateModelling(updateModellingParams.ModellingState, statefulObject, scope, ticketId);
                    if (updateModellingParams.ConfirmUiMessage)
                    {
                        wfHandler.DisplayMessage(null, wfHandler.userConfig.GetText("UpdateModelling"), $"{updatedModellingObjects}{wfHandler.userConfig.GetText("modelling_objects_updated")}", false);
                    }
                    break;
                default:
                    break;
            }
        }

        public async Task<bool> PerformActionById(int actionId, WfStatefulObject statefulObject, WfObjectScopes scope,
            FwoOwner? owner = null, long? ticketId = null, string? userGrpDn = null)
        {
            WfStateAction? action = GetOfferedActions(statefulObject, scope, wfHandler.Phase).FirstOrDefault(action => action.Id == actionId);
            if (action == null)
            {
                Log.WriteError("Workflow Actions", $"Action id {actionId} is not offered for {scope} in state {statefulObject.StateId} and phase {wfHandler.Phase}.");
                return false;
            }
            await PerformAction(action, statefulObject, scope, owner, ticketId, userGrpDn);
            return true;
        }

        private WorkflowActionParameters BuildWorkflowActionParameters(WfStatefulObject statefulObject, WfObjectScopes scope, long? ticketId, int actionId = 0)
        {
            return new()
            {
                Scope = scope.ToString(),
                ActionId = actionId,
                ObjectId = GetStatefulObjectId(statefulObject, scope),
                TicketId = GetTicketId(statefulObject, scope, ticketId),
                OldStateId = statefulObject.StateChanged() ? statefulObject.ChangedFrom() : statefulObject.StateId,
                NewStateId = statefulObject.StateId,
                StateChangedByCreation = statefulObject.StateChangedByCreation(),
                Phase = wfHandler.Phase.ToString()
            };
        }

        private async Task ExecuteInMiddleware(WorkflowActionParameters parameters)
        {
            string delegationKey = BuildMiddlewareDelegationKey(parameters);
            if (!TryRegisterMiddlewareDelegation(delegationKey))
            {
                Log.WriteDebug("Workflow Actions", $"Skipping duplicate middleware action execution. Scope: {parameters.Scope}, ActionId: {parameters.ActionId}, ObjectId: {parameters.ObjectId}, TicketId: {parameters.TicketId}, State: {parameters.OldStateId}->{parameters.NewStateId}, Phase: {parameters.Phase}.");
                return;
            }

            Log.WriteDebug("Workflow Actions", $"Delegating action execution to middleware. Scope: {parameters.Scope}, ActionId: {parameters.ActionId}, ObjectId: {parameters.ObjectId}, TicketId: {parameters.TicketId}, State: {parameters.OldStateId}->{parameters.NewStateId}, Phase: {parameters.Phase}.");
            try
            {
                RestResponse<WorkflowActionResult> response = await wfHandler.MiddlewareClient!.ExecuteWorkflowActions(parameters);
                DisplayWorkflowActionMessages(response.Data?.Messages);
                if (!response.IsSuccessful || response.Data?.Success != true)
                {
                    string details = response.Data?.ErrorMessage ?? response.ErrorMessage ?? response.Content ?? "";
                    string message = $"Middleware execution failed. Status: {(int)response.StatusCode} {response.StatusDescription}. {details}";
                    Log.WriteError("Workflow Actions", message);
                    throw new InvalidOperationException(message);
                }
            }
            finally
            {
                MarkMiddlewareDelegationDone(delegationKey);
            }
        }

        private void DisplayWorkflowActionMessages(List<WorkflowActionMessage>? messages)
        {
            foreach (WorkflowActionMessage message in messages ?? [])
            {
                wfHandler.DisplayMessage(null, message.Title, message.Message, message.ErrorFlag);
            }
        }

        private string BuildMiddlewareDelegationKey(WorkflowActionParameters parameters)
        {
            return $"{GetMiddlewareDelegationUserKey()}|{parameters.Scope}|{parameters.ActionId}|{parameters.ObjectId}|{parameters.TicketId}|{parameters.OldStateId}|{parameters.NewStateId}|{parameters.Phase}";
        }

        private string GetMiddlewareDelegationUserKey()
        {
            return wfHandler.AuthUser?.FindFirst("x-hasura-uuid")?.Value
                ?? wfHandler.AuthUser?.Identity?.Name
                ?? NoAuthUser;
        }

        private static bool TryRegisterMiddlewareDelegation(string delegationKey)
        {
            lock (MiddlewareDelegationLock)
            {
                DateTime now = DateTime.UtcNow;
                foreach (string expiredKey in MiddlewareDelegations
                    .Where(entry => now - entry.Value > MiddlewareDelegationDeduplicationWindow)
                    .Select(entry => entry.Key)
                    .ToList())
                {
                    MiddlewareDelegations.Remove(expiredKey);
                }

                if (MiddlewareDelegations.ContainsKey(delegationKey))
                {
                    return false;
                }

                MiddlewareDelegations[delegationKey] = now;
                return true;
            }
        }

        private static void MarkMiddlewareDelegationDone(string delegationKey)
        {
            lock (MiddlewareDelegationLock)
            {
                MiddlewareDelegations[delegationKey] = DateTime.UtcNow;
            }
        }

        private long GetStatefulObjectId(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => ticket.Id,
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => reqTask.Id,
                WfObjectScopes.ImplementationTask when statefulObject is WfImplTask implTask => implTask.Id,
                WfObjectScopes.Approval when statefulObject is WfApproval approval => approval.Id,
                _ => 0
            };
        }

        private long GetTicketId(WfStatefulObject statefulObject, WfObjectScopes scope, long? ticketId)
        {
            if (ticketId != null)
            {
                return (long)ticketId;
            }

            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => ticket.Id,
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => reqTask.TicketId,
                WfObjectScopes.ImplementationTask when statefulObject is WfImplTask implTask => implTask.TicketId,
                WfObjectScopes.Approval => wfHandler.ActTicket.Id,
                _ => 0
            };
        }

        public async Task CreateFlow(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner, long? ticketId)
        {
            if (!wfHandler.userConfig.ReqUseFlowDb)
            {
                Log.WriteInfo("Create Flow", $"Flow creation action '{action.Name}' skipped because Flow DB use is disabled.");
                return;
            }
            if (scope == WfObjectScopes.ImplementationTask)
            {
                Log.WriteInfo("Create Flow", $"Flow creation action '{action.Name}' skipped for implementation task scope.");
                return;
            }

            FlowDbCreator flowDbCreator = new(apiConnection);
            bool? success = await flowDbCreator.CreateFlowInFlowDb(action, statefulObject, scope, owner, ticketId);
            if (success != null)
            {
                ActionResultStateParams? resultStateParams = TryLoadActionResultStateParams(action.ExternalParams);
                if (resultStateParams?.ConfirmUiMessage == true)
                {
                    wfHandler.DisplayMessage(null, wfHandler.userConfig.GetText("CreateFlow"),
                        wfHandler.userConfig.GetText((bool)success ? "flow_creation_succeeded" : "flow_creation_failed"), !(bool)success);
                }
                await PromoteAfterActionResult(action.ExternalParams, (bool)success, statefulObject, scope);
            }
        }

        public async Task BundleTasks(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner, long? ticketId)
        {
            WfTicket? ticket = GetTicketForBundling(statefulObject, scope);
            if (ticket == null)
            {
                Log.WriteWarning("Bundle Tasks", $"Task bundling action '{action.Name}' found no ticket request tasks.");
                return;
            }

            BundleTasksActionParams bundleParams = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Dictionary<long, string> bundleAssignments = new RequestTaskBundler().BuildBundleAssignments(ticket.Tasks, bundleParams.BundleType);
            foreach (WfReqTask reqTask in ticket.Tasks.Where(task => task.Id > 0))
            {
                string currentBundleId = reqTask.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId);
                if (bundleAssignments.TryGetValue(reqTask.Id, out string? newBundleId) && !string.IsNullOrWhiteSpace(newBundleId))
                {
                    if (currentBundleId != newBundleId)
                    {
                        await wfHandler.SetAddInfoInReqTask(reqTask, AdditionalInfoKeys.FlowBundleId, newBundleId);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(currentBundleId))
                {
                    await wfHandler.RemoveAddInfoInReqTask(reqTask, AdditionalInfoKeys.FlowBundleId);
                }
            }
            Log.WriteInfo("Bundle Tasks", $"Bundled {bundleAssignments.Count} request tasks for flow creation.");
        }

        private WfTicket? GetTicketForBundling(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (scope == WfObjectScopes.Ticket && statefulObject is WfTicket ticket)
            {
                return ticket;
            }
            if (scope == WfObjectScopes.RequestTask && wfHandler.ActTicket.Tasks.Count > 0)
            {
                return wfHandler.ActTicket;
            }
            return null;
        }

        private async Task PromoteAfterActionResult(string externalParams, bool success, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (string.IsNullOrWhiteSpace(externalParams))
            {
                return;
            }

            ActionResultStateParams? resultStateParams = TryLoadActionResultStateParams(externalParams);
            if (resultStateParams == null)
            {
                return;
            }
            int? toState = success ? resultStateParams.SuccessState : resultStateParams.ErrorState;
            if (toState == null)
            {
                return;
            }

            if (states.FirstOrDefault(x => x.Id == toState) == null)
            {
                Log.WriteWarning("Action result state", $"Configured target state '{toState}' does not exist.");
                return;
            }

            await wfHandler.AutoPromote(statefulObject, scope, toState);
        }

        private static ActionResultStateParams? TryLoadActionResultStateParams(string externalParams)
        {
            if (string.IsNullOrWhiteSpace(externalParams))
            {
                return new();
            }

            try
            {
                return JsonSerializer.Deserialize<ActionResultStateParams>(externalParams) ?? new();
            }
            catch (JsonException exception)
            {
                Log.WriteWarning("Action result state", $"Configured action result parameters are invalid JSON. Skipping result-state promotion. {exception.Message}");
                return null;
            }
        }

        public async Task CallExternal(WfStateAction action)
        {
            // call external APIs with ExternalParams, e.g. for Compliance Check
        }

        public async Task SetAlert(string? description)
        {
            try
            {
                var Variables = new
                {
                    source = "workflow",
                    userId = 0,
                    title = "Workflow state alert",
                    description = description,
                    alertCode = (int)AlertCode.WorkflowAlert
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addAlert, Variables);
                Log.WriteAlert($"source: \"workflow\"",
                    $"userId: \"0\", title: \"Workflow state alert\", description: \"{description}\", " +
                    $"alertCode: \"{AlertCode.WorkflowAlert}\"");
            }
            catch (Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for Workflow: ", exc);
            }
        }

        private List<WfStateAction> GetRelevantActions(WfStatefulObject statefulObject, WfObjectScopes scope, bool toState = true)
        {
            List<WfStateAction> stateActions = [];
            try
            {
                int searchedStateId = toState ? statefulObject.StateId : statefulObject.ChangedFrom();
                foreach (var action in states.FirstOrDefault(x => x.Id == searchedStateId)?.Actions.Select(a => a.Action) ?? throw new KeyNotFoundException("Unknown stateId:" + searchedStateId))
                {
                    if (action.Scope == scope.ToString()
                        && (!(action.Scope == WfObjectScopes.RequestTask.ToString() || action.Scope == WfObjectScopes.ImplementationTask.ToString())
                        || action.TaskType == "" || action.TaskType == ((WfTaskBase)statefulObject).TaskType))
                    {
                        stateActions.Add(action);
                    }
                }
            }
            catch (Exception exc)
            {
                // unknown stateId probably by misconfiguration
                Log.WriteError("Get relevant actions", $"Exception thrown and ignored: ", exc);
            }
            return stateActions;
        }

        private async Task SetScope(WfStatefulObject statefulObject, WfObjectScopes scope, FwoNotification? notification = null)
        {
            ScopedUserTo = null;
            ScopedUserCc = null;
            ScopedUserBcc = null;
            ScopedUserEmailTo = null;
            ScopedUserEmailCc = null;
            ScopedUserEmailBcc = null;
            switch (scope)
            {
                case WfObjectScopes.Ticket:
                    wfHandler.SetTicketEnv((WfTicket)statefulObject);
                    SetCommenter(notification, wfHandler.ActTicket.Comments);
                    SetScopedRecipients(notification, EmailRecipientOption.Requester, GetRequesterDn(), GetRequesterEmail());
                    break;
                case WfObjectScopes.RequestTask:
                    wfHandler.SetReqTaskEnv((WfReqTask)statefulObject);
                    SetCommenter(notification, wfHandler.ActReqTask.Comments);
                    SetScopedRecipients(notification, EmailRecipientOption.Requester, GetRequesterDn(), GetRequesterEmail());
                    break;
                case WfObjectScopes.ImplementationTask:
                    wfHandler.SetImplTaskEnv((WfImplTask)statefulObject);
                    SetCommenter(notification, wfHandler.ActImplTask.Comments);
                    SetScopedRecipients(notification, EmailRecipientOption.Requester, GetRequesterDn(), GetRequesterEmail());
                    break;
                case WfObjectScopes.Approval:
                    if (wfHandler.SetReqTaskEnv(((WfApproval)statefulObject).TaskId))
                    {
                        await wfHandler.SetApprovalEnv(null, false);
                        SetCommenter(notification, wfHandler.ActApproval.Comments);
                        SetScopedRecipients(notification, EmailRecipientOption.Approver, wfHandler.ActApproval.ApproverDn, null);
                        SetScopedRecipients(notification, EmailRecipientOption.Requester, GetRequesterDn(), GetRequesterEmail());
                    }
                    break;
                default:
                    break;
            }
        }

        private string? GetRequesterDn()
        {
            return !string.IsNullOrWhiteSpace(wfHandler.ActTicket.Requester?.Dn) ? wfHandler.ActTicket.Requester.Dn : wfHandler.ActTicket.RequesterDn;
        }

        private string? GetRequesterEmail()
        {
            return wfHandler.ActTicket.Requester?.Email;
        }

        private void SetScopedRecipients(FwoNotification? notification, EmailRecipientOption recipientOption, string? userDn, string? userEmail)
        {
            if (notification?.RecipientTo == recipientOption)
            {
                ScopedUserTo = userDn;
                ScopedUserEmailTo = userEmail;
            }
            if (notification?.RecipientCc == recipientOption)
            {
                ScopedUserCc = userDn;
                ScopedUserEmailCc = userEmail;
            }
            if (notification?.RecipientBcc == recipientOption)
            {
                ScopedUserBcc = userDn;
                ScopedUserEmailBcc = userEmail;
            }
        }

        private void SetCommenter(FwoNotification? notification, List<WfCommentDataHelper> comments)
        {
            UiUser? lastCommenter = comments.Count > 0 ? comments.Last().Comment.Creator : null;
            string? lastCommenterDn = lastCommenter?.Dn;
            string? lastCommenterEmail = lastCommenter?.Email;
            ScopedUserTo = notification?.RecipientTo == EmailRecipientOption.LastCommenter ? lastCommenterDn : null;
            ScopedUserCc = notification?.RecipientCc == EmailRecipientOption.LastCommenter ? lastCommenterDn : null;
            ScopedUserBcc = notification?.RecipientBcc == EmailRecipientOption.LastCommenter ? lastCommenterDn : null;
            ScopedUserEmailTo = notification?.RecipientTo == EmailRecipientOption.LastCommenter ? lastCommenterEmail : null;
            ScopedUserEmailCc = notification?.RecipientCc == EmailRecipientOption.LastCommenter ? lastCommenterEmail : null;
            ScopedUserEmailBcc = notification?.RecipientBcc == EmailRecipientOption.LastCommenter ? lastCommenterEmail : null;
        }
    }
}
