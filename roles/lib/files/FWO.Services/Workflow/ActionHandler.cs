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
            if (statefulObject.StateChanged())
            {
                Log.WriteDebug("DoStateChangeActions", $"State changed for {scope} from {statefulObject.ChangedFrom()} to {statefulObject.StateId}.");
                if (!useInMwServer && wfHandler.MiddlewareClient != null)
                {
                    await ExecuteInMiddleware(BuildWorkflowActionParameters(statefulObject, scope, ticketId));
                    statefulObject.ResetStateChanged();
                    return;
                }

                List<WfStateAction> onSetActions = [.. GetRelevantActions(statefulObject, scope).Where(x => x.Event == StateActionEvents.OnSet.ToString())];
                List<WfStateAction> onLeaveActions = [.. GetRelevantActions(statefulObject, scope, false).Where(x => x.Event == StateActionEvents.OnLeave.ToString())];
                statefulObject.ResetStateChanged();

                foreach (var action in onSetActions)
                {
                    if (action.Phase == "" || action.Phase == wfHandler.Phase.ToString())
                    {
                        Log.WriteDebug("DoStateChangeActions", $"Perform OnSet action '{action.Name}' ({action.ActionType}) for {scope} state {statefulObject.StateId}.");
                        await PerformAction(action, statefulObject, scope, owner, ticketId, userGrpDn);
                    }
                }
                foreach (var action in onLeaveActions)
                {
                    if (action.Phase == "" || action.Phase == wfHandler.Phase.ToString())
                    {
                        Log.WriteDebug("DoStateChangeActions", $"Perform OnLeave action '{action.Name}' ({action.ActionType}) for {scope} state {statefulObject.ChangedFrom()}.");
                        await PerformAction(action, statefulObject, scope, owner, ticketId, userGrpDn);
                    }
                }
            }
        }

        public async Task DoOwnerChangeActions(WfStatefulObject statefulObject, FwoOwner? owner, long ticketId)
        {
            List<WfStateAction> ownerChangeActions = GetRelevantActions(statefulObject, WfObjectScopes.None);
            foreach (var action in ownerChangeActions.Where(x => x.Event == StateActionEvents.OwnerChange.ToString()))
            {
                await PerformAction(action, statefulObject, WfObjectScopes.None, owner, ticketId);
            }
        }

        public async Task DoOnAssignmentActions(WfStatefulObject statefulObject, string? userGrpDn)
        {
            List<WfStateAction> assignmentActions = GetRelevantActions(statefulObject, WfObjectScopes.None);
            foreach (var action in assignmentActions.Where(x => x.Event == StateActionEvents.OnAssignment.ToString()))
            {
                await PerformAction(action, statefulObject, WfObjectScopes.None, null, null, userGrpDn);
            }
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
                    if (updateModellingParams.ConfirmUiMessage && updatedModellingObjects > 0)
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
            WfStateAction? action = states.SelectMany(state => state.Actions.Select(actionHelper => actionHelper.Action)).FirstOrDefault(action => action.Id == actionId);
            if (action == null)
            {
                Log.WriteError("Workflow Actions", $"Action id {actionId} not found.");
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
                if (!response.IsSuccessful || response.Data?.Success != true)
                {
                    string details = response.Data?.ErrorMessage ?? response.ErrorMessage ?? response.Content ?? "";
                    string message = $"Middleware execution failed. Status: {(int)response.StatusCode} {response.StatusDescription}. {details}";
                    Log.WriteError("Workflow Actions", message);
                    throw new InvalidOperationException(message);
                }

                foreach (WorkflowActionMessage message in response.Data.Messages)
                {
                    wfHandler.DisplayMessage(null, message.Title, message.Message, message.ErrorFlag);
                }
            }
            finally
            {
                MarkMiddlewareDelegationDone(delegationKey);
            }
        }

        private static string BuildMiddlewareDelegationKey(WorkflowActionParameters parameters)
        {
            return $"{parameters.Scope}|{parameters.ActionId}|{parameters.ObjectId}|{parameters.TicketId}|{parameters.OldStateId}|{parameters.NewStateId}|{parameters.Phase}";
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

        public async Task CallExternal(WfStateAction action)
        {
            // call external APIs with ExternalParams, e.g. for Compliance Check
        }

        public async Task SendEmail(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner, string? userGrpDn = null)
        {
            Log.WriteDebug("SendEmail", "Perform Action");
            EmailActionParams? emailActionParams = null;
            try
            {
                emailActionParams = System.Text.Json.JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams) ?? throw new JsonException("Extparams could not be parsed.");
                List<FwoNotification> actionNotifications = await ResolveActionNotifications(emailActionParams);
                int sentEmailCount = 0;
                List<int> sentNotificationIds = [];
                foreach (FwoNotification actionNotification in actionNotifications)
                {
                    await SetScope(statefulObject, scope, actionNotification);
                    WorkflowEmailContent? workflowContent = await CreateWorkflowEmailContent(emailActionParams.AttachedContent, statefulObject, scope);
                    EmailHelper emailHelper = new(apiConnection, wfHandler.MiddlewareClient, wfHandler.userConfig, wfHandler.DisplayMessage, UserGroups, useInMwServer, workflowRecipientResolver);
                    await emailHelper.Init(ScopedUserTo, ScopedUserCc, ScopedUserBcc, ScopedUserEmailTo, ScopedUserEmailCc, ScopedUserEmailBcc);
                    WfStatefulObject placeholderObject = WorkflowPlaceholderObject(statefulObject);
                    if (await emailHelper.SendWorkflowActionEmail(actionNotification, statefulObject, owner, workflowContent: workflowContent, placeholderObject: placeholderObject))
                    {
                        ++sentEmailCount;
                        if (actionNotification.Id > 0)
                        {
                            sentNotificationIds.Add(actionNotification.Id);
                        }
                    }
                }
                await UpdateSentNotificationTimestamps(sentNotificationIds);
                Log.WriteInfo("SendEmail", $"Sent {sentEmailCount} workflow action email(s).");
                if (emailActionParams.ConfirmSentMail)
                {
                    wfHandler.DisplayMessage(null, wfHandler.userConfig.GetText("send_email"), $"{sentEmailCount}{wfHandler.userConfig.GetText("emails_sent")}", sentEmailCount == 0);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Send Email", $"Could not send email: ", exc);
                if (emailActionParams?.ConfirmSentMail ?? false)
                {
                    wfHandler.DisplayMessage(exc, wfHandler.userConfig.GetText("send_email"), "", true);
                }
            }
        }

        private async Task UpdateSentNotificationTimestamps(List<int> notificationIds)
        {
            List<int> distinctNotificationIds = [.. notificationIds.Where(id => id > 0).Distinct()];
            if (distinctNotificationIds.Count == 0)
            {
                return;
            }

            try
            {
                int affectedRows = (await apiConnection.SendQueryAsync<ReturnId>(NotificationQueries.updateNotificationsLastSent,
                    new { ids = distinctNotificationIds, lastSent = DateTime.Now })).AffectedRows;
                if (affectedRows != distinctNotificationIds.Count)
                {
                    Log.WriteWarning("SendEmail", $"Updated last_sent for {affectedRows} of {distinctNotificationIds.Count} workflow action notification(s).");
                }
            }
            catch (Exception exc)
            {
                Log.WriteWarning("SendEmail", $"Could not update last_sent for workflow action notification(s): {exc.Message}");
            }
        }

        private async Task<List<FwoNotification>> ResolveActionNotifications(EmailActionParams emailActionParams)
        {
            List<int> notificationIds = [.. emailActionParams.NotificationIds.Where(id => id > 0).Distinct()];
            if (notificationIds.Count > 0)
            {
                List<FwoNotification> notifications = await apiConnection.SendQueryAsync<List<FwoNotification>>(NotificationQueries.getNotifications,
                    new { client = NotificationClient.WfAction.ToString() });
                List<FwoNotification> actionNotifications = [.. notifications.Where(n => notificationIds.Contains(n.Id))];
                List<int> missingNotificationIds = [.. notificationIds.Except(actionNotifications.Select(n => n.Id))];
                if (missingNotificationIds.Count > 0)
                {
                    throw new JsonException($"Referenced notification(s) '{string.Join(", ", missingNotificationIds)}' were not found.");
                }
                return actionNotifications;
            }

            return [emailActionParams.ToNotification()];
        }

        private async Task<WorkflowEmailContent?> CreateWorkflowEmailContent(EmailAttachedContent attachedContent, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (attachedContent != EmailAttachedContent.RequestedConnections)
            {
                return null;
            }

            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => WorkflowEmailContent.FromRequestTasks((await GetTicketForEmailContent(ticket)).Tasks, wfHandler.userConfig),
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => WorkflowEmailContent.FromRequestTasks([reqTask], wfHandler.userConfig),
                WfObjectScopes.ImplementationTask when statefulObject is WfImplTask implTask => WorkflowEmailContent.FromImplementationTasks([implTask], wfHandler.userConfig),
                WfObjectScopes.Approval when wfHandler.ActReqTask.Id > 0 => WorkflowEmailContent.FromRequestTasks([wfHandler.ActReqTask], wfHandler.userConfig),
                _ => null
            };
        }

        private async Task<WfTicket> GetTicketForEmailContent(WfTicket ticket)
        {
            if (ticket.Id <= 0)
            {
                return ticket;
            }

            try
            {
                WfTicket fullTicket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticket.Id });
                fullTicket.UpdateCidrsInTaskElements();
                return fullTicket.Id > 0 ? fullTicket : ticket;
            }
            catch (Exception exc)
            {
                Log.WriteWarning("SendEmail", $"Could not load full ticket {ticket.Id} for workflow email content. Falling back to current ticket data. {exc.Message}");
                return ticket;
            }
        }

        private WfStatefulObject WorkflowPlaceholderObject(WfStatefulObject statefulObject)
        {
            return wfHandler.ActTicket.Id > 0 ? wfHandler.ActTicket : statefulObject;
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

        private async Task<int?> GetAutoPromoteTargetState(string externalParams, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (!WfStateAction.TryParseAutoPromoteParams(externalParams, out int? toState, out ConditionalAutoPromoteParams? conditionalParams))
            {
                throw new JsonException("Extparams could not be parsed.");
            }

            if (conditionalParams == null)
            {
                return toState;
            }

            return await EvaluateConditionalAutoPromote(conditionalParams, statefulObject, scope) ? conditionalParams.IfCompliantState : conditionalParams.IfNotCompliantState;
        }

        private Task<bool> EvaluateConditionalAutoPromote(ConditionalAutoPromoteParams conditionalParams, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            return conditionalParams.ToBeCalled switch
            {
                ToBeCalled.PolicyCheck => ExecutePolicyCheck(conditionalParams.PolicyIds, conditionalParams.CheckResultLabel, statefulObject, scope),
                _ => Task.FromResult(false)
            };
        }

        private async Task<bool> ExecutePolicyCheck(IEnumerable<int> selectedPolicyIds, string checkResultLabel, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            try
            {
                List<WfReqTask> requestedRuleTasks = GetRequestedRuleTasksForCallingTicket(statefulObject, scope);
                if (requestedRuleTasks.Count == 0)
                {
                    return false;
                }

                if (requestedRulePolicyChecker == null)
                {
                    return false;
                }

                bool isCompliant = await requestedRulePolicyChecker.AreRequestTasksCompliant(selectedPolicyIds, requestedRuleTasks);
                await AttachPolicyCheckResultLabel(requestedRuleTasks, checkResultLabel, isCompliant);
                return isCompliant;
            }
            catch (Exception exc)
            {
                Log.WriteError("Policy Check", "Conditional compliance evaluation failed.", exc);
                return false;
            }
        }

        private async Task AttachPolicyCheckResultLabel(IEnumerable<WfReqTask> requestTasks, string checkResultLabel, bool isCompliant)
        {
            if (string.IsNullOrWhiteSpace(checkResultLabel))
            {
                return;
            }

            foreach (WfReqTask requestTask in requestTasks)
            {
                await wfHandler.SetAddInfoInReqTask(requestTask, checkResultLabel.Trim(), isCompliant.ToString().ToLowerInvariant());
            }
        }

        private List<WfReqTask> GetRequestedRuleTasksForCallingTicket(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            WfTicket? ticket = GetCallingTicket(statefulObject, scope);
            if (ticket == null)
            {
                return [];
            }

            return ticket.Tasks
                .Where(task => task.ManagementId != null)
                .Where(task => task.GetNwObjectElements(ElemFieldType.source).Count > 0)
                .Where(task => task.GetNwObjectElements(ElemFieldType.destination).Count > 0)
                .Where(task => task.GetServiceElements().Count > 0)
                .ToList();
        }

        private WfTicket? GetCallingTicket(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (scope == WfObjectScopes.Ticket && statefulObject is WfTicket ticket)
            {
                return ticket;
            }

            if (wfHandler.ActTicket.Tasks.Count > 0)
            {
                return wfHandler.ActTicket;
            }

            return scope switch
            {
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => new WfTicket { Tasks = [reqTask] },
                WfObjectScopes.ImplementationTask when wfHandler.ActReqTask.Id > 0 => new WfTicket { Tasks = [wfHandler.ActReqTask] },
                WfObjectScopes.Approval when wfHandler.ActReqTask.Id > 0 => new WfTicket { Tasks = [wfHandler.ActReqTask] },
                _ => null
            };
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
