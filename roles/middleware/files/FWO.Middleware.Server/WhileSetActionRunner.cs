using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Middleware.Server.Services;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Performs the state actions with event "While set": every ticket in a state carrying such actions gets
    /// them performed in their sort order, until one of them moves the ticket out of the state. Only actions
    /// with ticket scope are performed.
    /// </summary>
    public sealed class WhileSetActionRunner
    {
        private const string kLogTitle = "While Set Actions";

        private readonly ApiConnection apiConnection;
        private readonly IWhileSetActionExecutor executor;

        /// <summary>
        /// Creates the runner.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection of the middleware server.</param>
        /// <param name="executor">Executor performing an action for a ticket.</param>
        public WhileSetActionRunner(ApiConnection apiConnection, IWhileSetActionExecutor executor)
        {
            this.apiConnection = apiConnection;
            this.executor = executor;
        }

        /// <summary>
        /// Performs the "While set" actions of all tickets in states carrying such actions.
        /// </summary>
        /// <param name="cancellationToken">Stops before the next ticket is processed.</param>
        /// <returns>Descriptions of the actions that failed.</returns>
        public async Task<List<string>> Run(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
            Dictionary<int, List<WfStateAction>> actionsByState = GetWhileSetTicketActions(states);
            if (actionsByState.Count == 0)
            {
                return [];
            }

            List<WfTicket> tickets = await apiConnection.SendQueryAsync<List<WfTicket>>(RequestQueries.getTicketIdsByStates,
                new { stateIds = actionsByState.Keys.Order().ToList() }) ?? [];
            List<string> failures = [];
            foreach (WfTicket ticket in tickets.Where(ticket => actionsByState.ContainsKey(ticket.StateId)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunTicketActions(ticket, actionsByState[ticket.StateId], failures);
            }
            return failures;
        }

        /// <summary>
        /// Returns the ticket scope actions with event "While set" per state, in their sort order.
        /// </summary>
        /// <param name="states">All states with their actions.</param>
        /// <returns>The actions by state id; states without such actions are left out.</returns>
        public static Dictionary<int, List<WfStateAction>> GetWhileSetTicketActions(List<WfState> states)
        {
            Dictionary<int, List<WfStateAction>> actionsByState = [];
            foreach (WfState state in states)
            {
                List<WfStateAction> actions = [.. state.Actions
                    .OrderBy(stateAction => stateAction.SortOrder)
                    .Select(stateAction => stateAction.Action)
                    .Where(IsWhileSetAction)];
                LogIgnoredScopes(state, actions);
                List<WfStateAction> ticketActions = [.. actions.Where(action => action.Scope == nameof(WfObjectScopes.Ticket))];
                if (ticketActions.Count > 0)
                {
                    actionsByState[state.Id] = ticketActions;
                }
            }
            return actionsByState;
        }

        private static bool IsWhileSetAction(WfStateAction action)
        {
            return action.Event == nameof(StateActionEvents.WhileSet);
        }

        private static void LogIgnoredScopes(WfState state, List<WfStateAction> actions)
        {
            foreach (WfStateAction action in actions.Where(action => action.Scope != nameof(WfObjectScopes.Ticket)))
            {
                Log.WriteDebug(kLogTitle, $"Action '{action.Name}' ({action.Id}) of state {state.Id} has scope {action.Scope}; " +
                    "only actions with ticket scope are performed while set.");
            }
        }

        private async Task RunTicketActions(WfTicket ticket, List<WfStateAction> actions, List<string> failures)
        {
            foreach (WfStateAction action in actions)
            {
                WhileSetActionResult result;
                try
                {
                    result = await executor.Execute(ticket.Id, ticket.StateId, action);
                }
                catch (Exception exception)
                {
                    Log.WriteError(kLogTitle, $"Action '{action.Name}' ({action.Id}) failed for ticket {ticket.Id}.", exception);
                    failures.Add($"Ticket {ticket.Id}, action '{action.Name}': {exception.Message}");
                    return;
                }

                if (result == WhileSetActionResult.Failed)
                {
                    failures.Add($"Ticket {ticket.Id}, action '{action.Name}' could not be performed");
                    return;
                }
                if (result != WhileSetActionResult.Stayed)
                {
                    return;
                }
            }
        }
    }
}
