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
/// Provides workflow change history endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/workflow")]
public class WorkflowChangeHistoryController : ControllerBase
{
    private readonly WorkflowChangeHistoryService changeHistoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowChangeHistoryController"/> class.
    /// </summary>
    /// <param name="changeHistoryService">The workflow change history service.</param>
    public WorkflowChangeHistoryController(WorkflowChangeHistoryService changeHistoryService)
    {
        this.changeHistoryService = changeHistoryService;
    }

    /// <summary>
    /// Returns the audit proof critical changes of a workflow ticket, newest first.
    /// </summary>
    /// <remarks>
    /// An audit proof critical change is a change history entry of the ticket that the workflow module marked
    /// as audit-proof critical: a content change made in a user session by someone other than the
    /// ticket requester. Changes written by background jobs are never reported.
    /// </remarks>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAuditProofCriticalChanges")]
    [ProducesResponseType(typeof(GetAuditProofCriticalChangesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetAuditProofCriticalChangesResponse>> GetAuditProofCriticalChanges([FromBody] GetAuditProofCriticalChangesRequest request)
    {
        RequestValidationErrorResponse validationErrors = GetAuditProofCriticalChangesRequestValidator.Validate(request);
        if (validationErrors.Errors.Count > 0)
        {
            return BadRequest(validationErrors);
        }

        // Validation rejects both an absent and a non-positive ticketId, so a value is present here.
        long ticketId = request.TicketId.GetValueOrDefault();
        try
        {
            return Ok(await changeHistoryService.GetAuditProofCriticalChangesAsync(ticketId, request.Options.Filter));
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Audit Proof Critical Changes", $"Error while fetching audit proof critical changes of ticket {ticketId}.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
