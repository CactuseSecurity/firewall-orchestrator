using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Result of performing a "While set" action for a ticket.
    /// </summary>
    public enum WhileSetActionResult
    {
        /// <summary>The action was performed and the ticket is still in the state.</summary>
        Stayed,
        /// <summary>The action was performed and has moved the ticket into another state.</summary>
        Moved,
        /// <summary>The ticket is gone, has left the state in the meantime or is currently processed by a workflow
        /// action request, so nothing was done.</summary>
        Skipped,
        /// <summary>The action could not be performed.</summary>
        Failed
    }

    /// <summary>
    /// Performs a "While set" action for a ticket.
    /// </summary>
    public interface IWhileSetActionExecutor
    {
        /// <summary>
        /// Performs the action for the ticket, if the ticket is still in the state.
        /// </summary>
        /// <param name="ticketId">Id of the ticket.</param>
        /// <param name="stateId">State the ticket is expected in.</param>
        /// <param name="action">Action to perform.</param>
        /// <returns>The result.</returns>
        Task<WhileSetActionResult> Execute(long ticketId, int stateId, WfStateAction action);
    }

    /// <summary>
    /// Performs "While set" actions with the workflow handler in system context, so that state changes caused by
    /// an action perform the state actions of the target state as well. The workflow environment of a phase is
    /// only loaded when an action is performed in it, and reused for all tickets of a run.
    /// A ticket whose lock is held by a workflow action request is skipped without waiting; the next run takes it up.
    /// </summary>
    public sealed class WorkflowWhileSetActionExecutor : IWhileSetActionExecutor, IDisposable
    {
        private const string kLogTitle = "While Set Actions";

        /// <summary>
        /// Owner the ticket locks of the "While set" actions are counted for.
        /// </summary>
        public const string kTicketLockOwner = "middleware:while-set-actions";

        /// <summary>
        /// Phase used when an action has no phase and the state belongs to no active phase.
        /// </summary>
        public const WorkflowPhases kDefaultPhase = WorkflowPhases.review;

        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private readonly WorkflowTicketLocks ticketLocks;
        private readonly Dictionary<WorkflowPhases, WfHandler> workflowHandlers = [];
        private Dictionary<WorkflowPhases, StateMatrix>? phaseMatrices;
        private UserConfig? userConfig;
        private List<Ldap>? connectedLdaps;
        private List<UserGroup>? ownerGroups;
        private bool disposed = false;

        /// <summary>
        /// Creates the executor.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection of the middleware server.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="ticketLocks">Per-ticket locks shared with the workflow action endpoint.</param>
        public WorkflowWhileSetActionExecutor(ApiConnection apiConnection, GlobalConfig globalConfig, WorkflowTicketLocks ticketLocks)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            this.ticketLocks = ticketLocks;
        }

        /// <inheritdoc/>
        public async Task<WhileSetActionResult> Execute(long ticketId, int stateId, WfStateAction action)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            using IDisposable? ticketLock = ticketLocks.TryAcquireImmediately(ticketId, kTicketLockOwner);
            if (ticketLock == null)
            {
                Log.WriteInfo(kLogTitle, $"Ticket {ticketId} is currently processed by a workflow action request; action '{action.Name}' is performed in the next run.");
                return WhileSetActionResult.Skipped;
            }
            return await ExecuteLocked(ticketId, stateId, action);
        }

        /// <summary>
        /// Performs the action while the lock of the ticket is held, so the ticket state is read and changed exclusively.
        /// </summary>
        private async Task<WhileSetActionResult> ExecuteLocked(long ticketId, int stateId, WfStateAction action)
        {
            WorkflowPhases phase = await GetPhase(action, stateId);
            WfHandler? wfHandler = await GetWorkflowHandler(phase);
            if (wfHandler?.ActionHandler == null)
            {
                return WhileSetActionResult.Failed;
            }

            WfTicket? ticket = await wfHandler.ResolveTicket(ticketId);
            if (ticket == null || ticket.StateId != stateId)
            {
                Log.WriteInfo(kLogTitle, $"Ticket {ticketId} has left state {stateId} in the meantime; action '{action.Name}' is not performed.");
                return WhileSetActionResult.Skipped;
            }

            string? requesterDn = !string.IsNullOrWhiteSpace(ticket.Requester?.Dn) ? ticket.Requester.Dn : ticket.RequesterDn;
            await wfHandler.ActionHandler.PerformAction(action, ticket, WfObjectScopes.Ticket, null, ticket.Id, requesterDn);
            return ticket.StateId == stateId ? WhileSetActionResult.Stayed : WhileSetActionResult.Moved;
        }

        /// <summary>
        /// Returns the phase of the action, or, for an action valid in all phases, the last active phase whose
        /// input states start at or below the state.
        /// </summary>
        /// <param name="phaseMatrices">Master state matrices per phase.</param>
        /// <param name="stateId">State of the ticket.</param>
        /// <returns>The phase the state belongs to, or <see cref="kDefaultPhase"/> if none is found.</returns>
        public static WorkflowPhases FindPhaseOfState(Dictionary<WorkflowPhases, StateMatrix> phaseMatrices, int stateId)
        {
            WorkflowPhases? foundPhase = null;
            foreach (KeyValuePair<WorkflowPhases, StateMatrix> phaseMatrix in phaseMatrices.OrderBy(entry => entry.Key))
            {
                if (phaseMatrix.Value.Active && phaseMatrix.Value.LowestInputState <= stateId)
                {
                    foundPhase = phaseMatrix.Key;
                }
            }
            return foundPhase ?? kDefaultPhase;
        }

        private async Task<WorkflowPhases> GetPhase(WfStateAction action, int stateId)
        {
            if (Enum.TryParse(action.Phase, out WorkflowPhases configuredPhase))
            {
                return configuredPhase;
            }

            if (phaseMatrices == null)
            {
                GlobalStateMatrix globalStateMatrix = GlobalStateMatrix.Create();
                await globalStateMatrix.Init(apiConnection, WfTaskType.master);
                phaseMatrices = globalStateMatrix.GlobalMatrix;
            }
            return FindPhaseOfState(phaseMatrices, stateId);
        }

        private async Task<WfHandler?> GetWorkflowHandler(WorkflowPhases phase)
        {
            if (workflowHandlers.TryGetValue(phase, out WfHandler? existingHandler))
            {
                return existingHandler;
            }

            userConfig ??= UserConfig.ForGlobalSettings(globalConfig, apiConnection, globalConfig.DefaultLanguage);
            connectedLdaps ??= await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
            ownerGroups ??= await MiddlewareServerServices.GetInternalGroups(connectedLdaps);
            WfHandler wfHandler = new(userConfig, apiConnection, phase, ownerGroups,
                new ComplianceRequestedRulePolicyChecker(userConfig, apiConnection), null,
                new WorkflowRecipientResolver(apiConnection, connectedLdaps))
            { SystemContext = true };
            if (!await wfHandler.Init())
            {
                Log.WriteError(kLogTitle, $"Workflow environment for phase {phase} could not be initialized.");
                return null;
            }
            workflowHandlers[phase] = wfHandler;
            return wfHandler;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            userConfig?.Dispose();
            disposed = true;
        }
    }
}
