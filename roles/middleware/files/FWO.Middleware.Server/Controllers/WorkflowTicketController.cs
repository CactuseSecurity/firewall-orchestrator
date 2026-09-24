using FWO.Basics;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides flow request endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/workflow")]
public class WorkflowTicketController : ControllerBase
{
    private readonly WorkflowTicketService workflowTicketService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowTicketController"/> class.
    /// </summary>
    /// <param name="workflowTicketService">The workflow ticket service.</param>
    public WorkflowTicketController(WorkflowTicketService workflowTicketService)
    {
        this.workflowTicketService = workflowTicketService;
    }

    /// <summary>
    /// Creates a new workflow ticket.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost("createTicket")]
    [ProducesResponseType(typeof(CreateTicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateTicketResponse>> CreateTicket([FromBody] CreateTicketRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Request body is missing.");
            }
            int requesterId = FWO.Basics.JwtClaimParser.ExtractIntClaimValues(User.Claims, "x-hasura-user-id").FirstOrDefault();
            string callerName = User.FindFirstValue("unique_name") ?? "";
            CreateTicketResponse response = await workflowTicketService.CreateTicketAsync(request, requesterId, callerName);
            return Ok(response);
        }
        catch (ArgumentException argumentException)
        {
            return BadRequest(argumentException.Message);
        }
        catch (Exception exception)
        {
            Log.WriteError("Create Ticket", "Error while creating workflow request.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Returns the status of an existing workflow ticket.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getTicketStatus")]
    [ProducesResponseType(typeof(GetTicketStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetTicketStatusResponse>> GetTicketStatus([FromBody] GetTicketStatusRequest request)
    {
        if (request.TicketId <= 0)
        {
            return BadRequest("'ticketId' must be greater than 0.");
        }

        try
        {
            GetTicketStatusResponse? response = await workflowTicketService.GetTicketStatusAsync(request.TicketId);
            return response == null ? NotFound() : Ok(response);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Ticket Status", "Error while fetching workflow ticket status.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
