using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ActionHandler
    {
        private List<RequestState> states = new List<RequestState>();
        private ApiConnection apiConnection;
        private RequestHandler requestHandler;

        public ActionHandler(ApiConnection apiConnection, RequestHandler requestHandler)
        {
            this.apiConnection = apiConnection;
            this.requestHandler = requestHandler;
        }

        public async Task Init()
        {
            states = new List<RequestState>();
            states = await apiConnection.SendQueryAsync<List<RequestState>>(FWO.Api.Client.Queries.RequestQueries.getStates);
        }


        private List<RequestStateAction> getRelevantActions(RequestStatefulObject statefulObject, RequestObjectScopes scope, bool toState=true)
        {
            List<RequestStateAction> stateActions = new List<RequestStateAction>();
            try
            {
                int searchedStateId = (toState ? statefulObject.StateId : statefulObject.ChangedFrom());
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

        public List<RequestStateAction> GetOfferedActions(RequestStatefulObject statefulObject, RequestObjectScopes scope, WorkflowPhases phase)
        {
            List<RequestStateAction> offeredActions = new List<RequestStateAction>();
            List<RequestStateAction> stateActions = getRelevantActions(statefulObject, scope);
            foreach(var action in stateActions.Where(x => (x.Event == StateActionEvents.OfferButton.ToString())))
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
                List<RequestStateAction> stateActions = getRelevantActions(statefulObject, scope);
                foreach(var action in stateActions.Where(x => (x.Event == StateActionEvents.OnSet.ToString())))
                {
                    if(action.Phase == "" || action.Phase == requestHandler.Phase.ToString())
                    {
                        await performAction(action, statefulObject, scope);
                    }
                }
                List<RequestStateAction> fromStateActions = getRelevantActions(statefulObject, scope, false);
                foreach(var action in fromStateActions.Where(x => (x.Event == StateActionEvents.OnLeave.ToString())))
                {
                    if(action.Phase == "" || action.Phase == requestHandler.Phase.ToString())
                    {
                        await performAction(action, statefulObject, scope);
                    }
                }
                statefulObject.ResetStateChanged();
            }
        }

        public async Task performAction(RequestStateAction action, RequestStatefulObject statefulObject, RequestObjectScopes scope)
        {
            switch(action.ActionType)
            {
                case nameof(StateActionTypes.AutoPromote):
                    int? toState = (action.ExternalParams != "" ? Convert.ToInt32(action.ExternalParams) : null);
                    if(toState == null || states.FirstOrDefault(x => x.Id == toState) != null)
                    {
                        await requestHandler.AutoPromote(statefulObject, scope, toState);
                    }
                    break;
                case nameof(StateActionTypes.AddApproval):
                    switch(scope)
                    {
                        case RequestObjectScopes.Ticket:
                            break;
                        case RequestObjectScopes.RequestTask:
                            requestHandler.SetReqTaskEnv((RequestReqTask)statefulObject);
                            break;
                        case RequestObjectScopes.ImplementationTask:
                            requestHandler.SetImplTaskEnv((RequestImplTask)statefulObject);
                            break;
                        case RequestObjectScopes.Approval:
                            break;
                        default:
                            break;
                    }
                    await requestHandler.AddApproval(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.SetAlert):
                    await setAlert(action.ExternalParams);
                    break;
                case nameof(StateActionTypes.ExternalCall):
                    await callExternal(action);
                    break;
                default:
                    break;
            }
        }

        public async Task callExternal(RequestStateAction action)
        {
            // call external APIs with ExternalParams, e.g. for Compliance Check
        }

        public async Task setAlert(string? description)
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
                    $"alertCode: \"{AlertCode.WorkflowAlert.ToString()}\"");
            }
            catch(Exception exc)
            {
                Log.WriteError("Write Alert", $"Could not write Alert for Workflow: ", exc);
            }
        }
    }
}
