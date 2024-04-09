using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ActionHandler
    {
        private List<RequestState> states = new ();
        private readonly ApiConnection apiConnection;
        private readonly RequestHandler requestHandler = new ();
        private string? ScopedUserTo { get; set; } = "";
        private string? ScopedUserCc { get; set; } = "";


        public ActionHandler(ApiConnection apiConnection, RequestHandler requestHandler)
        {
            this.apiConnection = apiConnection;
            this.requestHandler = requestHandler;
        }

        public async Task Init()
        {
            states = new List<RequestState>();
            states = await apiConnection.SendQueryAsync<List<RequestState>>(RequestQueries.getStates);
        }

        public List<RequestStateAction> GetOfferedActions(RequestStatefulObject statefulObject, RequestObjectScopes scope, WorkflowPhases phase)
        {
            List<RequestStateAction> offeredActions = new ();
            List<RequestStateAction> stateActions = GetRelevantActions(statefulObject, scope);
            foreach(var action in stateActions.Where(x => x.Event == StateActionEvents.OfferButton.ToString()))
            {
                if(action.Phase == "" || action.Phase == phase.ToString())
                {
                    offeredActions.Add(action);
                }
            }
            return offeredActions;
        }

        public async Task DoStateChangeActions(RequestStatefulObject statefulObject, RequestObjectScopes scope)
        {
            if (statefulObject.StateChanged())
            {
                List<RequestStateAction> stateActions = GetRelevantActions(statefulObject, scope);
                foreach(var action in stateActions.Where(x => x.Event == StateActionEvents.OnSet.ToString()))
                {
                    if(action.Phase == "" || action.Phase == requestHandler.Phase.ToString())
                    {
                        await PerformAction(action, statefulObject, scope);
                    }
                }
                List<RequestStateAction> fromStateActions = GetRelevantActions(statefulObject, scope, false);
                foreach(var action in fromStateActions.Where(x => x.Event == StateActionEvents.OnLeave.ToString()))
                {
                    if(action.Phase == "" || action.Phase == requestHandler.Phase.ToString())
                    {
                        await PerformAction(action, statefulObject, scope);
                    }
                }
                statefulObject.ResetStateChanged();
            }
        }

        public async Task DoOwnerChangeActions(RequestStatefulObject statefulObject, FwoOwner? owner, long ticketId)
        {
            List<RequestStateAction> ownerChangeActions = GetRelevantActions(statefulObject, RequestObjectScopes.None);
            foreach (var action in ownerChangeActions.Where(x => x.Event == StateActionEvents.OwnerChange.ToString()))
            {
                await PerformAction(action, statefulObject, RequestObjectScopes.None, owner, ticketId);
            }
        }

        public async Task PerformAction(RequestStateAction action, RequestStatefulObject statefulObject, RequestObjectScopes scope, FwoOwner? owner = null, long? ticketId = null)
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
                    await SendEmail(action, statefulObject, scope, owner);
                    break;
                case nameof(StateActionTypes.CreateConnection):
                    await CreateConnection(action, owner);
                    break;
                case nameof(StateActionTypes.UpdateConnectionOwner):
                    await UpdateConnectionOwner(owner, ticketId);
                    break;
                case nameof(StateActionTypes.UpdateConnectionRelease):
                    await UpdateConnectionRelease(action, owner);
                    break;
                default:
                    break;
            }
        }

        public async Task CallExternal(RequestStateAction action)
        {
            // call external APIs with ExternalParams, e.g. for Compliance Check
        }

        public async Task SendEmail(RequestStateAction action, RequestStatefulObject statefulObject, RequestObjectScopes scope, FwoOwner? owner)
        {
            Log.WriteDebug("SendEmail", "Perform Action");
            try
            {
                EmailActionParams emailActionParams = System.Text.Json.JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams) ?? throw new Exception("Extparams could not be parsed.");
                await SetScope(statefulObject, scope, emailActionParams);
                EmailHelper emailHelper = new(apiConnection, requestHandler.MiddlewareClient, requestHandler.userConfig, DefaultInit.DoNothing);
                await emailHelper.Init(ScopedUserTo, ScopedUserCc);
                await emailHelper.SendEmailFromAction(emailActionParams, statefulObject, owner);
            }
            catch(Exception exc)
            {
                Log.WriteError("Send Email", $"Could not send email: ", exc);
            }
        }

        public async Task CreateConnection(RequestStateAction action, FwoOwner? owner)
        {
            Log.WriteDebug("CreateConnection", "Perform Action");
            // try
            // {
            //     ModellingConnection proposedInterface = new(){ IsInterface = true, IsRequested = true, TicketId = requestHandler.ActTicket.Id };
            //     ModellingConnectionHandler ConnHandler = new (apiConnection, requestHandler.userConfig, requestHandler.ActReqTask.Owners.First().Owner, new(), proposedInterface, true, false, DefaultInit.DoNothing, false);
            //     await ConnHandler.CreateNewRequestedInterface();
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
                    apiConnection.SetRole(Roles.Modeller);
                    List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                    foreach(var conn in Connections)
                    {
                        if(conn.IsRequested)
                        {
                            var Variables = new
                            {
                                id = conn.Id,
                                name = conn.Name,
                                appId = owner.Id,
                                reason = conn.Reason,
                                isInterface = conn.IsInterface,
                                usedInterfaceId = conn.UsedInterfaceId,
                                isRequested = conn.IsRequested,
                                commonSvc = conn.IsCommonService
                            };
                            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnection, Variables);
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

        public async Task UpdateConnectionRelease(RequestStateAction action, FwoOwner? owner)
        {
            Log.WriteDebug("UpdateConnectionRelease", "Perform Action");
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

        private List<RequestStateAction> GetRelevantActions(RequestStatefulObject statefulObject, RequestObjectScopes scope, bool toState=true)
        {
            List<RequestStateAction> stateActions = new ();
            try
            {
                int searchedStateId = toState ? statefulObject.StateId : statefulObject.ChangedFrom();
                foreach(var actionHlp in states.FirstOrDefault(x => x.Id == searchedStateId)?.Actions ?? throw new Exception("Unknown stateId:" + searchedStateId))
                {
                    if(actionHlp.Action.Scope == scope.ToString() 
                        && (!(actionHlp.Action.Scope == RequestObjectScopes.RequestTask.ToString() || actionHlp.Action.Scope == RequestObjectScopes.ImplementationTask.ToString())
                        || actionHlp.Action.TaskType == "" || actionHlp.Action.TaskType == ((RequestTaskBase)statefulObject).TaskType))
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

        private async Task SetScope(RequestStatefulObject statefulObject, RequestObjectScopes scope, EmailActionParams? emailActionParams = null)
        {
            switch(scope)
            {
                case RequestObjectScopes.Ticket:
                    requestHandler.SetTicketEnv((RequestTicket)statefulObject);
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
                case RequestObjectScopes.RequestTask:
                    requestHandler.SetReqTaskEnv((RequestReqTask)statefulObject);
                    SetCommenter(emailActionParams, requestHandler.ActReqTask.Comments);
                    break;
                case RequestObjectScopes.ImplementationTask:
                    requestHandler.SetImplTaskEnv((RequestImplTask)statefulObject);
                    SetCommenter(emailActionParams, requestHandler.ActImplTask.Comments);
                    break;
                case RequestObjectScopes.Approval:
                    if(requestHandler.SetReqTaskEnv(((RequestApproval)statefulObject).TaskId))
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

        private void SetCommenter(EmailActionParams? emailActionParams, List<RequestCommentDataHelper> comments)
        {
            ScopedUserTo = emailActionParams?.RecipientTo == EmailRecipientOption.LastCommenter ? comments.Last().Comment.Creator.Dn : null;
            ScopedUserCc = emailActionParams?.RecipientCC == EmailRecipientOption.LastCommenter ? comments.Last().Comment.Creator.Dn : null;
        }
    }
}
