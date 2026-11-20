using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        private readonly Func<long, Task<bool>> startProvisioning;

        /// <summary>
		/// Constructor needing jwt writer, ldap list and connection
		/// </summary>
		public ExternalRequestController(ApiConnection apiConnection) : this(apiConnection, null)
        {
        }

        /// <summary>
        /// Constructor allowing to replace the start of the provisioning (unit testing only)
        /// </summary>
        internal ExternalRequestController(ApiConnection apiConnection, Func<long, Task<bool>>? startProvisioning)
        {
            this.apiConnection = apiConnection;
            this.startProvisioning = startProvisioning ?? StartProvisioning;
        }

        /// <summary>
        /// Add new ExternalRequest
        /// </summary>
        /// <remarks>
        /// TicketId (required) &#xA;
        /// Only admins may start the external requests of a ticket, e.g. by the reinit in the monitoring. Otherwise
        /// the provisioning only starts by the state action "Create external requests", so that it follows the
        /// workflow, e.g. after the approval. &#xA;
        /// Tickets which are completed or still have an open external request are refused. &#xA;
        /// Creates the request for the next open entry of the provisioning work list of the ticket, see the state action
        /// "Create external requests"; finished entries are skipped, so this also continues a stopped ticket.
        /// </remarks>
        /// <param name="parameters">ExternalRequestAddParameters</param>
        /// <returns>true if external request could be added; 404 if the ticket does not exist; 409 if the ticket is
        /// completed or its external requests are still being processed</returns>
        [HttpPost]
        [Authorize(Roles = $"{Roles.Admin}")]
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
            if (ticket == null)
            {
                return NotFound();
            }
            if (ticket.CompletionDate != null)
            {
                return Conflict("The ticket is already completed.");
            }
            if (await HasUnfinishedExternalRequest(ticket.Id))
            {
                return Conflict("The external requests of the ticket are still being processed.");
            }
            return await startProvisioning(ticket.Id);
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
        /// ImplTaskId (optional, implementation task of a firewall write request) &#xA;
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
                    ExtRequestState = parameters.ExtRequestState,
                    ImplTaskId = parameters.ImplTaskId
                };
                return await extRequestHandler.PatchState(extRequest);
            }
            else
            {
                return false;
            }
        }

        /// <summary>Starts the provisioning of a ticket in the middleware; returns whether it could be started.</summary>
        private async Task<bool> StartProvisioning(long ticketId)
        {
            using GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            using UserConfig userConfig = UserConfig.ForGlobalSettings(GlobalConfig, apiConnection);
            using ExternalRequestHandler extRequestHandler = new(userConfig, apiConnection);
            return await extRequestHandler.StartProvisioning(ticketId);
        }

        /// <summary>
        /// A request without finish date is still being processed; same criterion as the unique index uidx_ext_request_one_active_per_ticket.
        /// </summary>
        private async Task<bool> HasUnfinishedExternalRequest(long ticketId)
        {
            List<ExternalRequest> unfinishedRequests = await apiConnection.SendQueryAsync<List<ExternalRequest>>(ExtRequestQueries.getUnfinishedRequestsOfTicket, new { ticketId });
            return unfinishedRequests.Count > 0;
        }
    }
}
