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


        private List<RequestStateAction> getRelevantActions(StatefulObject statefulObject, ActionScopes scope, bool toState=true)
        {
            List<RequestStateAction> stateActions = new List<RequestStateAction>();
            int searchedStateId = (toState ? statefulObject.StateId : statefulObject.ChangedFrom());
            foreach(var actionHlp in states.FirstOrDefault(x => x.Id == searchedStateId)?.Actions ?? throw new Exception("Unknown stateId."))
            {
                if(actionHlp.Action.Scope == scope.ToString() 
                    && (!(actionHlp.Action.Scope == ActionScopes.RequestTask.ToString() || actionHlp.Action.Scope == ActionScopes.ImplementationTask.ToString())
                     || actionHlp.Action.TaskType == "" || actionHlp.Action.TaskType == ((TaskBase)statefulObject).TaskType))
                {
                    stateActions.Add(actionHlp.Action);
                }
            }
            return stateActions;
        }

        public List<RequestStateAction> GetOfferedActions(StatefulObject statefulObject, ActionScopes scope, WorkflowPhases phase)
        {
            List<RequestStateAction> offeredActions = new List<RequestStateAction>();
            List<RequestStateAction> stateActions = getRelevantActions(statefulObject, scope);
            foreach(var action in stateActions.Where(x => (x.Event == ActionEvents.OfferButton.ToString())))
            {
                if(action.Phase == "" || action.Phase == phase.ToString())
                {
                    offeredActions.Add(action);
                }
            }
            return offeredActions;
        }

        public async Task DoStateChangeActions(StatefulObject statefulObject, ActionScopes scope)
        {
            if (statefulObject.StateChanged())
            {
                List<RequestStateAction> stateActions = getRelevantActions(statefulObject, scope);
                foreach(var action in stateActions.Where(x => (x.Event == ActionEvents.OnSet.ToString())))
                {
                    await performAction(action, statefulObject, scope);
                }
                List<RequestStateAction> fromStateActions = getRelevantActions(statefulObject, scope, false);
                foreach(var action in fromStateActions.Where(x => (x.Event == ActionEvents.OnLeave.ToString())))
                {
                    await performAction(action, statefulObject, scope);
                }
                statefulObject.ResetStateChanged();
            }
        }

        public async Task performAction(RequestStateAction action, StatefulObject statefulObject, ActionScopes scope)
        {
            switch(action.ActionType)
            {
                case nameof(ActionTypes.AutoPromote):
                    int? toState = (action.ExternalParams != "" ? Convert.ToInt32(action.ExternalParams) : null);
                    if(toState == null || states.FirstOrDefault(x => x.Id == toState) != null)
                    {
                        await requestHandler.AutoPromote(statefulObject, scope, toState);
                    }
                    break;
                case nameof(ActionTypes.SetAlert):
                    await setAlert(action.ExternalParams);
                    break;
                case nameof(ActionTypes.AddApproval):
                    await requestHandler.AddApproval(action.ExternalParams);
                    break;
                case nameof(ActionTypes.ExternalCall):
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
