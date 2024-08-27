using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ActionHandler
    {
        private List<WfState> states = [];
        private readonly ApiConnection apiConnection;
        private readonly WfHandler requestHandler = new ();
        private string? ScopedUserTo { get; set; } = "";
        private string? ScopedUserCc { get; set; } = "";
        public bool DisplayConnectionMode = false;
        public ModellingConnectionHandler? ConnHandler { get; set; }


        public ActionHandler(ApiConnection apiConnection, WfHandler requestHandler)
        {
            this.apiConnection = apiConnection;
            this.requestHandler = requestHandler;
        }

        public async Task Init()
        {
            states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
        }

        public List<WfStateAction> GetOfferedActions(WfStatefulObject statefulObject, WfObjectScopes scope, WorkflowPhases phase)
        {
            List<WfStateAction> offeredActions = [];
            List<WfStateAction> stateActions = GetRelevantActions(statefulObject, scope);
            foreach(var action in stateActions.Where(x => x.Event == StateActionEvents.OfferButton.ToString()))
            {
                if(action.Phase == "" || action.Phase == phase.ToString())
                {
                    offeredActions.Add(action);
                }
            }
            return offeredActions;
        }

        public async Task DoStateChangeActions(WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner = null, long? ticketId = null)
        {
            if (statefulObject.StateChanged())
            {
                List<WfStateAction> stateActions = GetRelevantActions(statefulObject, scope);
                foreach(var action in stateActions.Where(x => x.Event == StateActionEvents.OnSet.ToString()))
                {
                    if(action.Phase == "" || action.Phase == requestHandler.Phase.ToString())
                    {
                        await PerformAction(action, statefulObject, scope, owner, ticketId);
                    }
                }
                List<WfStateAction> fromStateActions = GetRelevantActions(statefulObject, scope, false);
                foreach(var action in fromStateActions.Where(x => x.Event == StateActionEvents.OnLeave.ToString()))
                {
                    if(action.Phase == "" || action.Phase == requestHandler.Phase.ToString())
                    {
                        await PerformAction(action, statefulObject, scope, owner, ticketId);
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
            switch(action.ActionType)
            {
                case nameof(StateActionTypes.AutoPromote):
                    int? toState = action.ExternalParams != "" ? Convert.ToInt32(action.ExternalParams) : null;
                    if(toState == null || states.FirstOrDefault(x => x.Id == toState) != null)
                    {
                        await requestHandler.AutoPromote(statefulObject, scope, toState);
                    }
                    break;
                case nameof(StateActionTypes.AddApproval):
                    await SetScope(statefulObject, scope);
                    await requestHandler.AddApproval(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.SetAlert):
                    await SetAlert(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.TrafficPathAnalysis):
                    await SetScope(statefulObject, scope);
                    await requestHandler.HandlePathAnalysisAction(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.ExternalCall):
                    await CallExternal(action);
                    break;
                case nameof(StateActionTypes.SendEmail):
                    await SendEmail(action, statefulObject, scope, owner, userGrpDn);
                    break;
                // case nameof(StateActionTypes.CreateConnection):
                //     await CreateConnection(action, owner);
                //     break;
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
                EmailActionParams emailActionParams = System.Text.Json.JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams) ?? throw new Exception("Extparams could not be parsed.");
                await SetScope(statefulObject, scope, emailActionParams);
                EmailHelper emailHelper = new(apiConnection, requestHandler.MiddlewareClient, requestHandler.userConfig, DefaultInit.DoNothing);
                await emailHelper.Init(ScopedUserTo, ScopedUserCc);
                if(owner != null)
                {
                    await emailHelper.SendOwnerEmailFromAction(emailActionParams, statefulObject, owner);
                }
                else if(userGrpDn != null)
                {
                    await emailHelper.SendUserEmailFromAction(emailActionParams, statefulObject, userGrpDn);
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Send Email", $"Could not send email: ", exc);
            }
        }

        public async Task CreateConnection(WfStateAction action, FwoOwner? owner)
        {
            Log.WriteDebug("CreateConnection", "Perform Action");
            // try
            // {
            //     ModellingConnection proposedInterface = new(){ IsInterface = true, IsRequested = true, TicketId = requestHandler.ActTicket.Id };
            //     ModellingConnectionHandler ConnHandler = new (apiConnection, requestHandler.userConfig, requestHandler.ActReqTask.Owners.First().Owner, new(), proposedInterface, true, false, DefaultInit.DoNothing, false);
            //     apiConnection.SetProperRole(user, [Roles.Modeller, Roles.Admin]);
            //     await ConnHandler.CreateNewRequestedInterface();
            //     apiConnection.SwitchBack());
            // }
            // catch(Exception exc)
            // {
            //     Log.WriteError("Create Connection", $"Could not create connection externally from Workflow: ", exc);
            // }
        }

        public async Task UpdateConnectionOwner(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionOwner", "Perform Action");
            try
            {
                if(owner != null && ticketId != null) // todo: role check
                {
                    apiConnection.SetProperRole(requestHandler.AuthUser, [Roles.Modeller, Roles.Admin]);
                    List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                    foreach(var conn in Connections)
                    {
                        if(conn.IsRequested)
                        {
                            var Variables = new
                            {
                                id = conn.Id,
                                appId = owner.Id
                            };
                            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionOwner, Variables);
                            await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.Connection, conn.Id,
                                $"Updated {(conn.IsInterface? "Interface" : "Connection")}: {conn.Name}", apiConnection, requestHandler.userConfig, owner.Id, DefaultInit.DoNothing);
                        }
                    }
                    apiConnection.SwitchBack();
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Update Connection Owner", $"Could not change owner: ", exc);
            }
        }

        public async Task UpdateConnectionPublish(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionPublish", "Perform Action");
            try
            {
                if(owner != null && ticketId != null) // todo: role check
                {
                    apiConnection.SetProperRole(requestHandler.AuthUser, [Roles.Modeller, Roles.Admin]);
                    List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                    foreach(var conn in Connections)
                    {
                        if(conn.IsRequested && !conn.IsPublished)
                        {
                            ConnHandler = new (apiConnection, requestHandler.userConfig, owner, [], conn, true, false, DefaultInit.DoNothing, DefaultInit.DoNothing, false);
                            await ConnHandler.PartialInit();
                            if(ConnHandler.CheckConn())
                            {
                                var Variables = new
                                {
                                    id = conn.Id,
                                    isRequested = false,
                                    isPublished = true
                                };
                                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionPublish, Variables);
                                await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.Connection, conn.Id,
                                    $"Updated {(conn.IsInterface? "Interface" : "Connection")}: {conn.Name}", apiConnection, requestHandler.userConfig, owner.Id, DefaultInit.DoNothing);
                            }
                        }
                    }
                    apiConnection.SwitchBack();
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Update Connection Publish", $"Could not publish connection: ", exc);
            }
        }

        public async Task UpdateConnectionReject(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionReject", "Perform Action");
            try
            {
                if(owner != null && ticketId != null)
                {
                    apiConnection.SetProperRole(requestHandler.AuthUser, [Roles.Modeller, Roles.Admin]);
                    List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                    foreach(var conn in Connections)
                    {
                        if(conn.IsRequested)
                        {
                            conn.AddProperty(ConState.Rejected.ToString());
                            var Variables = new
                            {
                                id = conn.Id,
                                connProp = conn.Properties
                            };
                            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, Variables);
                            await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.Connection, conn.Id,
                                $"Rejected {(conn.IsInterface? "Interface" : "Connection")}: {conn.Name}", apiConnection, requestHandler.userConfig, owner.Id, DefaultInit.DoNothing);
                        }
                    }
                    apiConnection.SwitchBack();
                }
            }
            catch(Exception exc)
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
                WfReqTask? reqTask = requestHandler.ActTicket.Tasks.FirstOrDefault(x => x.TaskType == TaskType.new_interface.ToString());
                if(reqTask != null)
                {
                    requestHandler.SetReqTaskEnv(reqTask);
                }
                FwoOwner? owner = requestHandler.ActReqTask.Owners?.First()?.Owner;
                if(owner != null && requestHandler.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) != null)
                {
                    apiConnection.SetProperRole(requestHandler.AuthUser, [Roles.Modeller, Roles.Admin, Roles.Auditor]);
                    List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnections, new { appId = owner?.Id });
                    ModellingConnection? conn = Connections.FirstOrDefault(c => c.Id == requestHandler.GetAddInfoIntValue(AdditionalInfoKeys.ConnId));
                    if(conn != null)
                    {
                        ConnHandler = new ModellingConnectionHandler(apiConnection, requestHandler.userConfig, owner ?? new(), Connections, conn, false, true, DefaultInit.DoNothing, DefaultInit.DoNothing, false);
                        await ConnHandler.Init();
                        DisplayConnectionMode = true;
                    }
                    apiConnection.SwitchBack();
                }
            }
            catch(Exception exc)
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
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(MonitorQueries.addAlert, Variables)).ReturnIds;
                Log.WriteAlert ($"source: \"workflow\"", 
                    $"userId: \"0\", title: \"Workflow state alert\", description: \"{description}\", " +
                    $"alertCode: \"{AlertCode.WorkflowAlert}\"");
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for Workflow: ", exc);
            }
        }

        private List<WfStateAction> GetRelevantActions(WfStatefulObject statefulObject, WfObjectScopes scope, bool toState=true)
        {
            List<WfStateAction> stateActions = [];
            try
            {
                int searchedStateId = toState ? statefulObject.StateId : statefulObject.ChangedFrom();
                foreach(var actionHlp in states.FirstOrDefault(x => x.Id == searchedStateId)?.Actions ?? throw new Exception("Unknown stateId:" + searchedStateId))
                {
                    if(actionHlp.Action.Scope == scope.ToString() 
                        && (!(actionHlp.Action.Scope == WfObjectScopes.RequestTask.ToString() || actionHlp.Action.Scope == WfObjectScopes.ImplementationTask.ToString())
                        || actionHlp.Action.TaskType == "" || actionHlp.Action.TaskType == ((WfTaskBase)statefulObject).TaskType))
                    {
                        stateActions.Add(actionHlp.Action);
                    }
                }
            }
            catch(Exception exc)
            {
                // unknown stateId probably by misconfiguration
                Log.WriteError("Get relevant actions", $"Exception thrown and ignored: ", exc);
            }
            return stateActions;
        }

        private async Task SetScope(WfStatefulObject statefulObject, WfObjectScopes scope, EmailActionParams? emailActionParams = null)
        {
            switch(scope)
            {
                case WfObjectScopes.Ticket:
                    requestHandler.SetTicketEnv((WfTicket)statefulObject);
                    SetCommenter(emailActionParams, requestHandler.ActTicket.Comments);
                    if(emailActionParams?.RecipientTo == EmailRecipientOption.Requester)
                    {
                        ScopedUserTo = requestHandler.ActTicket.Requester?.Dn;
                    }
                    if(emailActionParams?.RecipientCC == EmailRecipientOption.Requester)
                    {
                        ScopedUserCc = requestHandler.ActTicket.Requester?.Dn;
                    }
                    break;
                case WfObjectScopes.RequestTask:
                    requestHandler.SetReqTaskEnv((WfReqTask)statefulObject);
                    SetCommenter(emailActionParams, requestHandler.ActReqTask.Comments);
                    break;
                case WfObjectScopes.ImplementationTask:
                    requestHandler.SetImplTaskEnv((WfImplTask)statefulObject);
                    SetCommenter(emailActionParams, requestHandler.ActImplTask.Comments);
                    break;
                case WfObjectScopes.Approval:
                    if(requestHandler.SetReqTaskEnv(((WfApproval)statefulObject).TaskId))
                    {
                        await requestHandler.SetApprovalEnv(null, false);
                        SetCommenter(emailActionParams, requestHandler.ActApproval.Comments);
                        if(emailActionParams?.RecipientTo == EmailRecipientOption.Approver)
                        {
                            ScopedUserTo = requestHandler.ActApproval.ApproverDn;
                        }
                        if(emailActionParams?.RecipientCC == EmailRecipientOption.Approver)
                        {
                            ScopedUserCc = requestHandler.ActApproval.ApproverDn;
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
