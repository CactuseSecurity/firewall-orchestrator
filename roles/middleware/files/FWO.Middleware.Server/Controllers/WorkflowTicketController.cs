using FWO.Basics;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides workflow ticket endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/workflow")]
[AggregatedValidationErrors]
public class WorkflowTicketController : ControllerBase
{
    private readonly FlowRequestService flowRequestService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowTicketController"/> class.
    /// </summary>
    /// <param name="flowRequestService">The request workflow service that loads tickets and resolves their status.</param>
    public WorkflowTicketController(FlowRequestService flowRequestService)
    {
        this.flowRequestService = flowRequestService;
    }

    /// <summary>
    /// Returns an existing workflow ticket with all of its details: request tasks with their elements,
    /// approvals, implementation tasks, owners and comments, and the ticket-level comments.
    /// </summary>
    /// <remarks>
    /// <c>options.filter</c> restricts the returned request tasks; the ticket itself is always
    /// returned when it exists. A ticketId that names no ticket answers 404 with the error contract
    /// of this endpoint.
    /// </remarks>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getTicket")]
    [ProducesResponseType(typeof(GetTicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetTicketResponse>> GetTicket([FromBody] GetTicketRequest request)
    {
        RequestValidationErrorResponse validationErrors = GetTicketRequestValidator.Validate(request);
        if (validationErrors.Errors.Count > 0)
        {
            return BadRequest(validationErrors);
        }

        // Validation rejects both an absent and a non-positive ticketId, so a value is present here.
        long ticketId = request.TicketId.GetValueOrDefault();
        try
        {
            GetTicketResponse? response = await flowRequestService.GetTicketAsync(ticketId, request.Options.Filter);
            if (response == null)
            {
                return NotFound(GetTicketRequestValidator.BuildUnknownTicketError(ticketId));
            }

            return Ok(response);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Ticket", $"Error while fetching workflow ticket {ticketId}.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
