using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.Modelling;
using System.Text.Json;


namespace FWO.Services.Workflow
{
    public class ActionHandler
    {
        private List<WfState> states = [];
        private readonly ApiConnection apiConnection;
        private readonly WfHandler wfHandler;
        private readonly bool useInMwServer = false;
        private readonly IRequestedRulePolicyChecker? requestedRulePolicyChecker;
        private string? ScopedUserTo { get; set; } = "";
        private string? ScopedUserCc { get; set; } = "";
        public bool DisplayConnectionMode = false;
        public ModellingConnectionHandler? ConnHandler { get; set; }
        private readonly List<UserGroup>? UserGroups = [];
        private readonly string NoAuthUser = "No Auth User";


        public ActionHandler(ApiConnection apiConnection, WfHandler wfHandler, List<UserGroup>? userGroups = null, bool useInMwServer = false,
            IRequestedRulePolicyChecker? requestedRulePolicyChecker = null)
        {
            this.apiConnection = apiConnection;
            this.wfHandler = wfHandler;
            this.useInMwServer = useInMwServer;
            UserGroups = userGroups;
            this.requestedRulePolicyChecker = requestedRulePolicyChecker ?? wfHandler.RequestedRulePolicyChecker;
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
                List<WfStateAction> stateActions = GetRelevantActions(statefulObject, scope);
                foreach (var action in stateActions.Where(x => x.Event == StateActionEvents.OnSet.ToString()))
                {
                    if (action.Phase == "" || action.Phase == wfHandler.Phase.ToString())
                    {
                        await PerformAction(action, statefulObject, scope, owner, ticketId, userGrpDn);
                    }
                }
                List<WfStateAction> fromStateActions = GetRelevantActions(statefulObject, scope, false);
                foreach (var action in fromStateActions.Where(x => x.Event == StateActionEvents.OnLeave.ToString()))
                {
                    if (action.Phase == "" || action.Phase == wfHandler.Phase.ToString())
                    {
                        await PerformAction(action, statefulObject, scope, owner, ticketId, userGrpDn);
                    }
                }
                statefulObject.ResetStateChanged();
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
                default:
                    break;
            }
        }

        public async Task CallExternal(WfStateAction action)
        {
            // call external APIs with ExternalParams, e.g. for Compliance Check
        }

        public async Task SendEmail(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner, string? userGrpDn = null)
        {
            Log.WriteDebug("SendEmail", "Perform Action");
            try
            {
                EmailActionParams emailActionParams = System.Text.Json.JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams) ?? throw new JsonException("Extparams could not be parsed.");
                await SetScope(statefulObject, scope, emailActionParams);
                EmailHelper emailHelper = new(apiConnection, wfHandler.MiddlewareClient, wfHandler.userConfig, DefaultInit.DoNothing, UserGroups, useInMwServer);
                await emailHelper.Init(ScopedUserTo, ScopedUserCc);
                if (owner != null)
                {
                    await emailHelper.SendOwnerEmailFromAction(emailActionParams, statefulObject, owner);
                }
                else if (userGrpDn != null)
                {
                    await emailHelper.SendUserEmailFromAction(emailActionParams, statefulObject, userGrpDn);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Send Email", $"Could not send email: ", exc);
            }
        }

        public async Task UpdateConnectionOwner(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionOwner", "Perform Action");
            try
            {
                if (owner != null && ticketId != null) // todo: role check
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                        foreach (var conn in Connections)
                        {
                            if (conn.IsRequested)
                            {
                                var Variables = new
                                {
                                    id = conn.Id,
                                    propAppId = owner.Id
                                };
                                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateProposedConnectionOwner, Variables);
                                await ModellingHandlerBase.LogChange(new LogChangeRequest
                                {
                                    ChangeType = ModellingTypes.ChangeType.Update,
                                    ObjectType = ModellingTypes.ModObjectType.Connection,
                                    ObjectId = conn.Id,
                                    Text = $"Updated {(conn.IsInterface ? "Interface" : "Connection")}: {conn.Name}",
                                    ApiConnection = apiConnection,
                                    UserConfig = wfHandler.userConfig,
                                    ApplicationId = owner.Id,
                                    DisplayMessageInUi = DefaultInit.DoNothing
                                });
                            }
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Update Connection Owner", $"Could not change owner: ", exc);
            }
        }

        public async Task UpdateConnectionPublish(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionPublish", "Perform Action");
            try
            {
                if (owner != null && ticketId != null) // todo: role check
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                        foreach (var conn in Connections)
                        {
                            if (conn.IsRequested && !conn.IsPublished)
                            {
                                await PublishInterface(conn, owner);
                            }
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Update Connection Publish", $"Could not publish connection: ", exc);
            }
        }

        private async Task PublishInterface(ModellingConnection conn, FwoOwner owner)
        {
            if (conn.AppId == null && conn.ProposedAppId != null)
            {
                conn.AppId = conn.ProposedAppId;
                conn.ProposedAppId = null;
            }
            var Variables = new
            {
                id = conn.Id,
                isRequested = false,
                isPublished = true,
                appId = conn.AppId,
                proposedAppId = conn.ProposedAppId
            };
            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionPublish, Variables);
            await ModellingHandlerBase.LogChange(new LogChangeRequest
            {
                ChangeType = ModellingTypes.ChangeType.Publish,
                ObjectType = ModellingTypes.ModObjectType.Connection,
                ObjectId = conn.Id,
                Text = $"Published {(conn.IsInterface ? "Interface" : "Connection")}: {conn.Name}",
                ApiConnection = apiConnection,
                UserConfig = wfHandler.userConfig,
                ApplicationId = owner.Id,
                DisplayMessageInUi = DefaultInit.DoNothing
            });
        }

        public async Task UpdateConnectionReject(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionReject", "Perform Action");
            try
            {
                if (owner != null && ticketId != null)
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                        foreach (var conn in Connections)
                        {
                            if (conn.IsRequested)
                            {
                                conn.AddProperty(ConState.Rejected.ToString());
                                var Variables = new
                                {
                                    id = conn.Id,
                                    connProp = conn.Properties
                                };
                                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, Variables);
                                await ModellingHandlerBase.LogChange(new LogChangeRequest
                                {
                                    ChangeType = ModellingTypes.ChangeType.Reject,
                                    ObjectType = ModellingTypes.ModObjectType.Connection,
                                    ObjectId = conn.Id,
                                    Text = $"Rejected {(conn.IsInterface ? "Interface" : "Connection")}: {conn.Name}",
                                    ApiConnection = apiConnection,
                                    UserConfig = wfHandler.userConfig,
                                    ApplicationId = owner.Id,
                                    DisplayMessageInUi = DefaultInit.DoNothing
                                });
                            }
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Reject Connection", $"Could not change state: ", exc);
            }
        }

        public async Task DisplayConnection(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            try
            {
                Log.WriteDebug("DisplayConnection", "Perform Action");
                await SetScope(statefulObject, scope);
                WfReqTask? reqTask = wfHandler.ActTicket.Tasks.FirstOrDefault(x => x.TaskType == WfTaskType.new_interface.ToString());
                if (reqTask != null)
                {
                    wfHandler.SetReqTaskEnv(reqTask);
                }
                FwoOwner? owner = wfHandler.ActReqTask.Owners?.FirstOrDefault()?.Owner;
                if (owner != null && wfHandler.ActReqTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) != null)
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin, Roles.Auditor], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnections, new { appId = owner.Id });
                        ModellingConnection? conn = Connections.FirstOrDefault(c => c.Id == wfHandler.ActReqTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId));
                        if (conn != null)
                        {
                            ConnHandler = new ModellingConnectionHandler(apiConnection, wfHandler.userConfig, owner, Connections, conn, false, true, DefaultInit.DoNothing, DefaultInit.DoNothing, false);
                            await ConnHandler.Init();
                            DisplayConnectionMode = true;
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Display Connection", $"Could not display: ", exc);
            }
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

        private async Task SetScope(WfStatefulObject statefulObject, WfObjectScopes scope, EmailActionParams? emailActionParams = null)
        {
            switch (scope)
            {
                case WfObjectScopes.Ticket:
                    wfHandler.SetTicketEnv((WfTicket)statefulObject);
                    SetCommenter(emailActionParams, wfHandler.ActTicket.Comments);
                    if (emailActionParams?.RecipientTo == EmailRecipientOption.Requester)
                    {
                        ScopedUserTo = wfHandler.ActTicket.Requester?.Dn;
                    }
                    if (emailActionParams?.RecipientCC == EmailRecipientOption.Requester)
                    {
                        ScopedUserCc = wfHandler.ActTicket.Requester?.Dn;
                    }
                    break;
                case WfObjectScopes.RequestTask:
                    wfHandler.SetReqTaskEnv((WfReqTask)statefulObject);
                    SetCommenter(emailActionParams, wfHandler.ActReqTask.Comments);
                    break;
                case WfObjectScopes.ImplementationTask:
                    wfHandler.SetImplTaskEnv((WfImplTask)statefulObject);
                    SetCommenter(emailActionParams, wfHandler.ActImplTask.Comments);
                    break;
                case WfObjectScopes.Approval:
                    if (wfHandler.SetReqTaskEnv(((WfApproval)statefulObject).TaskId))
                    {
                        await wfHandler.SetApprovalEnv(null, false);
                        SetCommenter(emailActionParams, wfHandler.ActApproval.Comments);
                        if (emailActionParams?.RecipientTo == EmailRecipientOption.Approver)
                        {
                            ScopedUserTo = wfHandler.ActApproval.ApproverDn;
                        }
                        if (emailActionParams?.RecipientCC == EmailRecipientOption.Approver)
                        {
                            ScopedUserCc = wfHandler.ActApproval.ApproverDn;
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private void SetCommenter(EmailActionParams? emailActionParams, List<WfCommentDataHelper> comments)
        {
            ScopedUserTo = emailActionParams?.RecipientTo == EmailRecipientOption.LastCommenter ? comments.Last().Comment.Creator.Dn : null;
            ScopedUserCc = emailActionParams?.RecipientCC == EmailRecipientOption.LastCommenter ? comments.Last().Comment.Creator.Dn : null;
        }
    }
}
