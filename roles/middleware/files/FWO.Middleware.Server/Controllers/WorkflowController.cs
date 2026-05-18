using FWO.Api.Client;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

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
        private static readonly ConcurrentDictionary<long, SemaphoreSlim> TicketActionLocks = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        public WorkflowController(GlobalConfig globalConfig, List<Ldap> ldaps, JwtWriter jwtWriter)
        {
            this.globalConfig = globalConfig;
            this.ldaps = ldaps;
            this.jwtWriter = jwtWriter;
        }

        /// <summary>
        /// Execute workflow actions in middleware-server context.
        /// </summary>
        /// <param name="parameters">Workflow action parameters</param>
        /// <returns>true if the workflow action execution was handled</returns>
        [HttpPost("Actions")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.FwAdmin}, {Roles.WorkflowRolesList}, {Roles.Modeller}")]
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

                WorkflowPhases phase = ParsePhase(parameters.Phase);
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

        private static WorkflowPhases ParsePhase(string phase)
        {
            return Enum.TryParse(phase, out WorkflowPhases parsedPhase) ? parsedPhase : WorkflowPhases.request;
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
            using ApiConnection actionApiConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url."), jwtWriter.CreateJWTMiddlewareServer());
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

            (WfStatefulObject? statefulObject, FwoOwner? owner, long? actionTicketId, string? userGrpDn) =
                ResolveActionContext(wfHandler, ticket, parameters, scope);
            if (statefulObject == null)
            {
                SetWarning(result, $"Stateful object could not be resolved. Scope: {scope}, ObjectId: {parameters.ObjectId}, TicketId: {ticket.Id}.");
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

        private static (WfStatefulObject?, FwoOwner?, long?, string?) ResolveActionContext(WfHandler wfHandler, WfTicket ticket,
            WorkflowActionParameters parameters, WfObjectScopes scope)
        {
            return scope switch
            {
                WfObjectScopes.Ticket => (ticket, null, ticket.Id, GetRequesterDn(ticket)),
                WfObjectScopes.RequestTask => ResolveRequestTask(ticket, parameters.ObjectId),
                WfObjectScopes.ImplementationTask => ResolveImplementationTask(ticket, parameters.ObjectId),
                WfObjectScopes.Approval => ResolveApproval(wfHandler, ticket, parameters.ObjectId),
                _ => (null, null, null, null)
            };
        }

        private static string? GetRequesterDn(WfTicket ticket)
        {
            return !string.IsNullOrWhiteSpace(ticket.Requester?.Dn) ? ticket.Requester.Dn : ticket.RequesterDn;
        }

        private static (WfStatefulObject?, FwoOwner?, long?, string?) ResolveRequestTask(WfTicket ticket, long objectId)
        {
            WfReqTask? reqTask = ticket.Tasks.FirstOrDefault(task => task.Id == objectId);
            return reqTask == null ? (null, null, null, null) : (reqTask, reqTask.Owners.FirstOrDefault()?.Owner, reqTask.TicketId, null);
        }

        private static (WfStatefulObject?, FwoOwner?, long?, string?) ResolveImplementationTask(WfTicket ticket, long objectId)
        {
            foreach (WfReqTask reqTask in ticket.Tasks)
            {
                WfImplTask? implTask = reqTask.ImplementationTasks.FirstOrDefault(task => task.Id == objectId);
                if (implTask != null)
                {
                    implTask.TicketId = implTask.TicketId > 0 ? implTask.TicketId : reqTask.TicketId;
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
                    wfHandler.SetReqTaskEnv(reqTask);
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
