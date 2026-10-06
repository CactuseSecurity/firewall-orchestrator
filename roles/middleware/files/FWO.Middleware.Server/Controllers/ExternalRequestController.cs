using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
	/// Controller class for role api
	/// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExternalRequestController : ControllerBase
    {
        private readonly ApiConnection apiConnection;
        private readonly Func<long, Task<bool>> sendFirstRequest;

        /// <summary>
        /// External request states which indicate that the request chain of a ticket is still being processed.
        /// Same set as used by the UI to decide whether a ticket may be reinitialized.
        /// </summary>
        private static readonly List<string> kOpenRequestStates =
        [
            ExtStates.ExtReqInitialized.ToString(),
            ExtStates.ExtReqFailed.ToString(),
            ExtStates.ExtReqRequested.ToString(),
            ExtStates.ExtReqInProgress.ToString(),
            ExtStates.ExtReqRejected.ToString(),
            ExtStates.ExtReqDone.ToString()
        ];

        /// <summary>
		/// Constructor needing jwt writer, ldap list and connection
		/// </summary>
		public ExternalRequestController(ApiConnection apiConnection) : this(apiConnection, null)
        {
        }

        /// <summary>
        /// Constructor allowing to replace the request sender (unit testing only)
        /// </summary>
        internal ExternalRequestController(ApiConnection apiConnection, Func<long, Task<bool>>? sendFirstRequest)
        {
            this.apiConnection = apiConnection;
            this.sendFirstRequest = sendFirstRequest ?? SendFirstRequest;
        }

        /// <summary>
        /// Add new ExternalRequest
        /// </summary>
        /// <remarks>
        /// TicketId (required) &#xA;
        /// Admins may start the external requests of any ticket. Modellers may only start them for tickets
        /// whose request tasks all belong to owners they are allowed to edit. &#xA;
        /// Tickets which are completed or still have an open external request are refused.
        /// </remarks>
        /// <param name="parameters">ExternalRequestAddParameters</param>
        /// <returns>true if external request could be added; 404 if the ticket does not exist or is not accessible
        /// for the caller; 409 if the ticket is completed or its external requests are still being processed</returns>
        [HttpPost]
        [Authorize(Roles = $"{Roles.Modeller}, {Roles.Admin}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<bool>> Post([FromBody] ExternalRequestAddParameters parameters)
        {
            if (parameters.TicketId <= 0)
            {
                return false;
            }

            WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = parameters.TicketId });
            if (ticket == null || !CallerMayRequestTicket(ticket))
            {
                return NotFound();
            }
            if (ticket.CompletionDate != null)
            {
                return Conflict("The ticket is already completed.");
            }
            if (await HasOpenExternalRequest(ticket.Id))
            {
                return Conflict("The external requests of the ticket are still being processed.");
            }
            return await sendFirstRequest(ticket.Id);
        }

        /// <summary>
        /// Patch ExternalRequest state
        /// </summary>
        /// <remarks>
        /// ExtRequestId (required) &#xA;
        /// TicketId (required) &#xA;
        /// TaskNumber (required) &#xA;
        /// ExtQueryVariables (optional) &#xA;
        /// ExtRequestState (required) &#xA;
        /// </remarks>
        /// <param name="parameters">ExternalRequestPatchStateParameters</param>
        /// <returns>true if external request state could be patched</returns>
		[HttpPatch("PatchState")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> Change([FromBody] ExternalRequestPatchStateParameters parameters)
        {
            if (parameters.ExtRequestId > 0)
            {
                using GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                using UserConfig userConfig = UserConfig.ForGlobalSettings(GlobalConfig, apiConnection);
                using ExternalRequestHandler extRequestHandler = new(userConfig, apiConnection);
                ExternalRequest extRequest = new()
                {
                    Id = parameters.ExtRequestId,
                    TicketId = parameters.TicketId,
                    TaskNumber = parameters.TaskNumber,
                    ExtQueryVariables = parameters.ExtQueryVariables,
                    ExtRequestState = parameters.ExtRequestState
                };
                return await extRequestHandler.PatchState(extRequest);
            }
            else
            {
                return false;
            }
        }

        private async Task<bool> SendFirstRequest(long ticketId)
        {
            using GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            using UserConfig userConfig = UserConfig.ForGlobalSettings(GlobalConfig, apiConnection);
            using ExternalRequestHandler extRequestHandler = new(userConfig, apiConnection);
            return await extRequestHandler.SendFirstRequest(ticketId);
        }

        /// <summary>
        /// Admins may handle every ticket, modellers only tickets whose request tasks all belong to editable owners.
        /// Interface request tickets of other applications are not affected, as they are never handled via this endpoint.
        /// </summary>
        private bool CallerMayRequestTicket(WfTicket ticket)
        {
            ClaimsPrincipal caller = ControllerContext.HttpContext.User;
            if (caller.IsInRole(Roles.Admin))
            {
                return true;
            }

            List<int> ticketOwnerIds = [.. ticket.Tasks.SelectMany(task => task.Owners).Select(owner => owner.Owner.Id).Distinct()];
            List<int> editableOwnerIds = JwtClaimParser.ExtractIntClaimValues(caller.Claims, "x-hasura-editable-owners");
            bool mayRequest = caller.IsInRole(Roles.Modeller) && ticketOwnerIds.Count > 0 && ticketOwnerIds.All(editableOwnerIds.Contains);
            if (!mayRequest)
            {
                Log.WriteWarning("External Request", $"Denied external request for ticket {ticket.Id}. " +
                    $"Caller='{caller.Identity?.Name}', ticketOwnerIds='{string.Join(",", ticketOwnerIds)}', editableOwnerIds='{string.Join(",", editableOwnerIds)}'.");
            }
            return mayRequest;
        }

        private async Task<bool> HasOpenExternalRequest(long ticketId)
        {
            List<ExternalRequest> openRequests = await apiConnection.SendQueryAsync<List<ExternalRequest>>(ExtRequestQueries.getOpenRequests, new { states = kOpenRequestStates });
            return openRequests.Any(request => request.TicketId == ticketId);
        }
    }
}
