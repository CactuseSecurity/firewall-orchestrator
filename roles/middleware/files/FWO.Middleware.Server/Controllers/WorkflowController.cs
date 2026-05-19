using FWO.Api.Client;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Controller class for workflow actions.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowController : ControllerBase
    {
        private readonly GlobalConfig globalConfig;
        private readonly List<Ldap> ldaps;
        private readonly JwtWriter jwtWriter;
        private readonly TokenLifetimeProvider tokenLifetimeProvider;
        private static readonly ConcurrentDictionary<long, SemaphoreSlim> TicketActionLocks = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        public WorkflowController(GlobalConfig globalConfig, List<Ldap> ldaps, JwtWriter jwtWriter, TokenLifetimeProvider tokenLifetimeProvider)
        {
            this.globalConfig = globalConfig;
            this.ldaps = ldaps;
            this.jwtWriter = jwtWriter;
            this.tokenLifetimeProvider = tokenLifetimeProvider;
        }

        /// <summary>
        /// Execute workflow actions in middleware-server context.
        /// </summary>
        /// <param name="parameters">Workflow action parameters</param>
        /// <returns>true if the workflow action execution was handled</returns>
        [HttpPost("Actions")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.FwAdmin}, {Roles.WorkflowRolesList}")]
        public async Task<WorkflowActionResult> ExecuteActions([FromBody] WorkflowActionParameters parameters)
        {
            WorkflowActionResult result = new();
            try
            {
                LogActionRequest(parameters);
                if (!TryParseScope(parameters, result, out WfObjectScopes scope))
                {
                    return result;
                }

                if (!TryParsePhase(parameters, result, out WorkflowPhases phase))
                {
                    return result;
                }

                if (!CallerCanExecutePhase(User, phase))
                {
                    SetWarning(result, $"User is not authorized to execute workflow actions in phase '{phase}'.");
                    return result;
                }

                long lockTicketId = GetTicketId(parameters, scope);
                return await ExecuteActionsWithTicketLock(parameters, scope, phase, lockTicketId, result);
            }
            catch (Exception exc)
            {
                Log.WriteError("Workflow Actions", "Could not execute workflow actions in middleware.", exc);
                result.ErrorMessage = exc.Message;
                return result;
            }
        }

        private static void LogActionRequest(WorkflowActionParameters parameters)
        {
            Log.WriteDebug("Workflow Actions", $"Received action execution request. Scope: {parameters.Scope}, ActionId: {parameters.ActionId}, ObjectId: {parameters.ObjectId}, TicketId: {parameters.TicketId}, State: {parameters.OldStateId}->{parameters.NewStateId}, Phase: {parameters.Phase}.");
        }

        private static bool TryParseScope(WorkflowActionParameters parameters, WorkflowActionResult result, out WfObjectScopes scope)
        {
            if (Enum.TryParse(parameters.Scope, out scope) && scope != WfObjectScopes.None)
            {
                return true;
            }

            SetWarning(result, $"Invalid scope '{parameters.Scope}'.");
            return false;
        }

        private static bool TryParsePhase(WorkflowActionParameters parameters, WorkflowActionResult result, out WorkflowPhases phase)
        {
            if (Enum.TryParse(parameters.Phase, out phase))
            {
                return true;
            }

            SetWarning(result, $"Invalid workflow phase '{parameters.Phase}'.");
            return false;
        }

        private static bool CallerCanExecutePhase(ClaimsPrincipal user, WorkflowPhases phase)
        {
            if (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.FwAdmin))
            {
                return true;
            }

            return phase switch
            {
                WorkflowPhases.request => user.IsInRole(Roles.Requester),
                WorkflowPhases.approval => user.IsInRole(Roles.Approver),
                WorkflowPhases.planning => user.IsInRole(Roles.Planner),
                WorkflowPhases.implementation => user.IsInRole(Roles.Implementer),
                WorkflowPhases.review => user.IsInRole(Roles.Reviewer),
                _ => false
            };
        }

        private async Task<WorkflowActionResult> ExecuteActionsWithTicketLock(WorkflowActionParameters parameters, WfObjectScopes scope,
            WorkflowPhases phase, long lockTicketId, WorkflowActionResult result)
        {
            SemaphoreSlim ticketActionLock = TicketActionLocks.GetOrAdd(lockTicketId, _ => new SemaphoreSlim(1, 1));
            await ticketActionLock.WaitAsync();
            try
            {
                return await ExecuteActionsWithApi(parameters, scope, phase, lockTicketId, result);
            }
            finally
            {
                ticketActionLock.Release();
            }
        }

        private async Task<WorkflowActionResult> ExecuteActionsWithApi(WorkflowActionParameters parameters, WfObjectScopes scope,
            WorkflowPhases phase, long lockTicketId, WorkflowActionResult result)
        {
            using ApiConnection actionApiConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url."),
                jwtWriter.CreateJWTMiddlewareServer(tokenLifetimeProvider.GetInternalServiceTokenLifetime()));
            return await actionApiConnection.RunWithRole(Roles.MiddlewareServer,
                async () => await ExecuteActionsInMiddlewareContext(actionApiConnection, parameters, scope, phase, lockTicketId, result));
        }

        private async Task<WorkflowActionResult> ExecuteActionsInMiddlewareContext(ApiConnection actionApiConnection, WorkflowActionParameters parameters,
            WfObjectScopes scope, WorkflowPhases phase, long lockTicketId, WorkflowActionResult result)
        {
            using UserConfig userConfig = new(globalConfig, actionApiConnection, new UiUser { DbId = 0, Language = globalConfig.DefaultLanguage }, false);
            WfHandler wfHandler = CreateWorkflowHandler(actionApiConnection, userConfig, phase, result);
            if (!await InitWorkflowHandler(wfHandler, result))
            {
                return result;
            }

            WfTicket? ticket = await ResolveWorkflowTicket(wfHandler, lockTicketId, result);
            if (ticket == null)
            {
                return result;
            }

            if (!CallerCanAccessTicket(User, userConfig, ticket))
            {
                SetWarning(result, $"User is not authorized to execute workflow actions for ticket {ticket.Id}.");
                return result;
            }

            (WfStatefulObject? statefulObject, FwoOwner? owner, long? actionTicketId, string? userGrpDn) =
                ResolveActionContext(wfHandler, ticket, parameters, scope);
            if (statefulObject == null)
            {
                SetWarning(result, $"Stateful object could not be resolved. Scope: {scope}, ObjectId: {parameters.ObjectId}, TicketId: {ticket.Id}.");
                return result;
            }

            if (!ValidateExecutionRequest(wfHandler, parameters, scope, phase, statefulObject, result))
            {
                return result;
            }

            result.Success = await ExecuteResolvedAction(wfHandler, parameters, scope, statefulObject, owner, actionTicketId, userGrpDn);
            return result;
        }

        private WfHandler CreateWorkflowHandler(ApiConnection actionApiConnection, UserConfig userConfig, WorkflowPhases phase, WorkflowActionResult result)
        {
            return new WfHandler(userConfig, actionApiConnection, phase, null, new ComplianceRequestedRulePolicyChecker(userConfig, actionApiConnection),
                (exception, title, message, errorFlag) => AddWorkflowMessage(result, exception, title, message, errorFlag),
                new WorkflowRecipientResolver(actionApiConnection, ldaps));
        }

        private static void AddWorkflowMessage(WorkflowActionResult result, Exception? exception, string title, string message, bool errorFlag)
        {
            string resultMessage = string.IsNullOrWhiteSpace(message) ? exception?.Message ?? "" : message;
            result.Messages.Add(new() { Title = title, Message = resultMessage, ErrorFlag = errorFlag });
            if (exception != null)
            {
                Log.WriteError(title, resultMessage, exception);
            }
        }

        private static bool ValidateExecutionRequest(WfHandler wfHandler, WorkflowActionParameters parameters, WfObjectScopes scope,
            WorkflowPhases phase, WfStatefulObject statefulObject, WorkflowActionResult result)
        {
            return parameters.ActionId > 0
                ? ValidateOfferedAction(wfHandler, parameters, scope, phase, statefulObject, result)
                : ValidatePersistedStateTransition(wfHandler, parameters, statefulObject, result);
        }

        private static bool ValidateOfferedAction(WfHandler wfHandler, WorkflowActionParameters parameters, WfObjectScopes scope,
            WorkflowPhases phase, WfStatefulObject statefulObject, WorkflowActionResult result)
        {
            int expectedState = parameters.OldStateId > 0 ? parameters.OldStateId : parameters.NewStateId;
            if (expectedState > 0 && statefulObject.StateId != expectedState)
            {
                SetWarning(result, $"Action execution rejected because the object state is {statefulObject.StateId}, not {expectedState}.");
                return false;
            }

            bool actionOffered = wfHandler.ActionHandler?.GetOfferedActions(statefulObject, scope, phase).Any(action => action.Id == parameters.ActionId) == true;
            if (!actionOffered)
            {
                SetWarning(result, $"Action {parameters.ActionId} is not offered for scope '{scope}' in phase '{phase}' and current state {statefulObject.StateId}.");
                return false;
            }

            return true;
        }

        private static bool ValidatePersistedStateTransition(WfHandler wfHandler, WorkflowActionParameters parameters,
            WfStatefulObject statefulObject, WorkflowActionResult result)
        {
            if (parameters.OldStateId == parameters.NewStateId)
            {
                SetWarning(result, $"State-change action execution rejected because no state change was requested.");
                return false;
            }

            if (statefulObject.StateId != parameters.NewStateId)
            {
                SetWarning(result, $"State-change action execution rejected because the persisted object state is {statefulObject.StateId}, not {parameters.NewStateId}.");
                return false;
            }

            if (!wfHandler.ActStateMatrix.getAllowedTransitions(parameters.OldStateId, true).Contains(parameters.NewStateId))
            {
                SetWarning(result, $"State-change action execution rejected because transition {parameters.OldStateId}->{parameters.NewStateId} is not allowed.");
                return false;
            }

            return true;
        }

        private static async Task<bool> InitWorkflowHandler(WfHandler wfHandler, WorkflowActionResult result)
        {
            if (await wfHandler.InitForActionExecution())
            {
                return true;
            }

            string initDetails = result.Messages.LastOrDefault(message => message.ErrorFlag)?.Message ?? "";
            string errorMessage = string.IsNullOrWhiteSpace(initDetails)
                ? "Workflow handler initialization failed."
                : $"Workflow handler initialization failed. {initDetails}";
            SetWarning(result, errorMessage);
            return false;
        }

        private static async Task<WfTicket?> ResolveWorkflowTicket(WfHandler wfHandler, long lockTicketId, WorkflowActionResult result)
        {
            WfTicket? ticket = await wfHandler.ResolveTicket(lockTicketId);
            if (ticket == null)
            {
                SetWarning(result, $"Ticket {lockTicketId} could not be resolved.");
                return null;
            }

            wfHandler.TicketList = [ticket];
            return ticket;
        }

        private static async Task<bool> ExecuteResolvedAction(WfHandler wfHandler, WorkflowActionParameters parameters, WfObjectScopes scope,
            WfStatefulObject statefulObject, FwoOwner? owner, long? actionTicketId, string? userGrpDn)
        {
            if (parameters.ActionId > 0)
            {
                return await wfHandler.ActionHandler!.PerformActionById(parameters.ActionId, statefulObject, scope, owner, actionTicketId, userGrpDn);
            }

            MarkStateChanged(statefulObject, parameters.OldStateId, parameters.NewStateId);
            await wfHandler.ActionHandler!.DoStateChangeActions(statefulObject, scope, owner, actionTicketId, userGrpDn);
            return true;
        }

        private static void SetWarning(WorkflowActionResult result, string errorMessage)
        {
            Log.WriteWarning("Workflow Actions", errorMessage);
            result.ErrorMessage = errorMessage;
        }

        private static long GetTicketId(WorkflowActionParameters parameters, WfObjectScopes scope)
        {
            return parameters.TicketId > 0 || scope != WfObjectScopes.Ticket ? parameters.TicketId : parameters.ObjectId;
        }

        private static bool CallerCanAccessTicket(ClaimsPrincipal user, UserConfig userConfig, WfTicket ticket)
        {
            if (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.FwAdmin) || !userConfig.ReqOwnerBased)
            {
                return true;
            }

            if (CallerIsRequester(user, ticket))
            {
                return true;
            }

            HashSet<int> editableOwnerIds = GetClaimIds(user, "x-hasura-editable-owners");
            return editableOwnerIds.Count > 0 && ticket.Tasks.Any(task => CallerOwnsTask(task, editableOwnerIds));
        }

        private static bool CallerIsRequester(ClaimsPrincipal user, WfTicket ticket)
        {
            int? userId = GetClaimInt(user, "x-hasura-user-id");
            string? userDn = GetClaimValue(user, "x-hasura-uuid");
            return (userId != null && ticket.Requester?.DbId == userId)
                || (!string.IsNullOrWhiteSpace(userDn) && string.Equals(GetRequesterDn(ticket), userDn, StringComparison.OrdinalIgnoreCase));
        }

        private static bool CallerOwnsTask(WfReqTask task, HashSet<int> editableOwnerIds)
        {
            int requestingOwnerId = task.GetAddInfoIntValueOrZero(AdditionalInfoKeys.ReqOwner);
            return task.Owners.Any(owner => editableOwnerIds.Contains(owner.Owner.Id))
                || (requestingOwnerId > 0 && editableOwnerIds.Contains(requestingOwnerId));
        }

        private static HashSet<int> GetClaimIds(ClaimsPrincipal user, string claimName)
        {
            string claimValue = GetClaimValue(user, claimName) ?? "";
            return claimValue
                .Trim('{', '}', ' ')
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => int.TryParse(value, out _))
                .Select(int.Parse)
                .ToHashSet();
        }

        private static int? GetClaimInt(ClaimsPrincipal user, string claimName)
        {
            return int.TryParse(GetClaimValue(user, claimName), out int value) ? value : null;
        }

        private static string? GetClaimValue(ClaimsPrincipal user, string claimName)
        {
            return user.Claims.FirstOrDefault(claim => claim.Type.Equals(claimName, StringComparison.OrdinalIgnoreCase)
                || claim.Type.EndsWith("/" + claimName, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private static (WfStatefulObject?, FwoOwner?, long?, string?) ResolveActionContext(WfHandler wfHandler, WfTicket ticket,
            WorkflowActionParameters parameters, WfObjectScopes scope)
        {
            return scope switch
            {
                WfObjectScopes.Ticket => (ticket, null, ticket.Id, GetRequesterDn(ticket)),
                WfObjectScopes.RequestTask => ResolveRequestTask(wfHandler, ticket, parameters.ObjectId),
                WfObjectScopes.ImplementationTask => ResolveImplementationTask(wfHandler, ticket, parameters.ObjectId),
                WfObjectScopes.Approval => ResolveApproval(wfHandler, ticket, parameters.ObjectId),
                _ => (null, null, null, null)
            };
        }

        private static string? GetRequesterDn(WfTicket ticket)
        {
            return !string.IsNullOrWhiteSpace(ticket.Requester?.Dn) ? ticket.Requester.Dn : ticket.RequesterDn;
        }

        private static (WfStatefulObject?, FwoOwner?, long?, string?) ResolveRequestTask(WfHandler wfHandler, WfTicket ticket, long objectId)
        {
            WfReqTask? reqTask = ticket.Tasks.FirstOrDefault(task => task.Id == objectId);
            if (reqTask == null)
            {
                return (null, null, null, null);
            }

            wfHandler.TrySetReqTaskEnv(reqTask);
            return (reqTask, reqTask.Owners.FirstOrDefault()?.Owner, reqTask.TicketId, null);
        }

        private static (WfStatefulObject?, FwoOwner?, long?, string?) ResolveImplementationTask(WfHandler wfHandler, WfTicket ticket, long objectId)
        {
            foreach (WfReqTask reqTask in ticket.Tasks)
            {
                WfImplTask? implTask = reqTask.ImplementationTasks.FirstOrDefault(task => task.Id == objectId);
                if (implTask != null)
                {
                    implTask.TicketId = implTask.TicketId > 0 ? implTask.TicketId : reqTask.TicketId;
                    wfHandler.TrySetImplTaskEnv(implTask);
                    return (implTask, reqTask.Owners.FirstOrDefault()?.Owner, implTask.TicketId, null);
                }
            }
            return (null, null, null, null);
        }

        private static (WfStatefulObject?, FwoOwner?, long?, string?) ResolveApproval(WfHandler wfHandler, WfTicket ticket, long objectId)
        {
            foreach (WfReqTask reqTask in ticket.Tasks)
            {
                WfApproval? approval = reqTask.Approvals.FirstOrDefault(approval => approval.Id == objectId);
                if (approval != null)
                {
                    wfHandler.TrySetReqTaskEnv(reqTask);
                    return (approval, reqTask.Owners.FirstOrDefault()?.Owner, reqTask.TicketId, null);
                }
            }
            return (null, null, null, null);
        }

        private static void MarkStateChanged(WfStatefulObject statefulObject, int oldStateId, int newStateId)
        {
            statefulObject.StateId = oldStateId;
            statefulObject.ResetStateChanged();
            statefulObject.StateId = newStateId;
        }
    }
}
