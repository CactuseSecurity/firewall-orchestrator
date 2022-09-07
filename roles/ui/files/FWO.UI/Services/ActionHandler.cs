using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ActionHandler
    {
        private List<RequestState> states = new List<RequestState>();
        private List<RequestStateAction> stateActions = new List<RequestStateAction>();
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

        private void RefreshStateActions(StatefulObject statefulObject)
        {
            stateActions = new List<RequestStateAction>();
            foreach(var actionHlp in states.FirstOrDefault(x => x.Id == statefulObject.StateId)?.Actions ?? throw new Exception("Unknown stateId."))
            {
                stateActions.Add(actionHlp.Action);
            }
        }

        public List<RequestStateAction> GetOfferedActions(StatefulObject statefulObject, ActionScopes scope)
        {
            List<RequestStateAction> offeredActions = new List<RequestStateAction>();
            RefreshStateActions(statefulObject);
            foreach(var action in stateActions.Where(x => (x.Scope == scope.ToString() && x.Event == ActionEvents.OfferButton.ToString())))
            {
                offeredActions.Add(action);
            }
            return offeredActions;
        }

        public async Task DoOnSetActions(StatefulObject statefulObject, ActionScopes scope)
        {
            if (statefulObject.StateChanged())
            {
                RefreshStateActions(statefulObject);
                foreach(var action in stateActions.Where(x => (x.Scope == scope.ToString() && x.Event == ActionEvents.OnSet.ToString())))
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
                    await requestHandler.AutoPromote(statefulObject, scope);
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
