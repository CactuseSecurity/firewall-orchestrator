using FWO.Basics;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides workflow change history endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/workflow")]
[AggregatedValidationErrors]
public class WorkflowChangeHistoryController : ControllerBase
{
    private readonly WorkflowChangeHistoryService changeHistoryService;
    private readonly GlobalConfig globalConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowChangeHistoryController"/> class.
    /// </summary>
    /// <param name="changeHistoryService">The workflow change history service.</param>
    /// <param name="globalConfig">Global configuration holding the maximum page size.</param>
    public WorkflowChangeHistoryController(WorkflowChangeHistoryService changeHistoryService, GlobalConfig globalConfig)
    {
        this.changeHistoryService = changeHistoryService;
        this.globalConfig = globalConfig;
    }

    /// <summary>
    /// Returns the audit proof critical changes of a workflow ticket, newest first.
    /// </summary>
    /// <remarks>
    /// An audit proof critical change is a change history entry of the ticket that the workflow module marked
    /// as audit-proof critical: a content change made in a user session by someone other than the
    /// ticket requester. Changes written by background jobs are never reported.
    /// <para>
    /// A ticket that exists but carries no such change answers with an empty <c>changes</c> list. A
    /// ticketId that names no ticket answers 404 with the error contract of this endpoint, so an
    /// empty result cannot be mistaken for a mistyped or already deleted ticket id.
    /// </para>
    /// <para>
    /// Changes are ordered newest first, so <c>options.limit</c> and <c>options.offset</c> page the result
    /// deterministically. Without a limit a page of at most the configured <c>maxRestResponseObjectLimit</c>
    /// changes (default 10000) is returned, and the <c>X-Has-More</c> response header tells whether further
    /// changes follow.
    /// </para>
    /// </remarks>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAuditProofCriticalChanges")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(GetAuditProofCriticalChangesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetAuditProofCriticalChangesResponse>> GetAuditProofCriticalChanges([FromBody] GetAuditProofCriticalChangesRequest request)
    {
        int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
        RequestValidationErrorResponse validationErrors = GetAuditProofCriticalChangesRequestValidator.Validate(request, maxLimit);
        if (validationErrors.Errors.Count > 0)
        {
            return BadRequest(validationErrors);
        }

        // Validation rejects both an absent and a non-positive ticketId, so a value is present here.
        long ticketId = request.TicketId.GetValueOrDefault();
        try
        {
            (GetAuditProofCriticalChangesResponse Response, bool HasMore)? page = await changeHistoryService.GetAuditProofCriticalChangesAsync(
                ticketId, request.Options.Filter, ListPaging.GetPageSize(request.Options.Limit, maxLimit), request.Options.Offset);
            if (page == null)
            {
                return NotFound(GetAuditProofCriticalChangesRequestValidator.BuildUnknownTicketError(ticketId));
            }

            ListPaging.SetHasMoreHeader(HttpContext, page.Value.HasMore);
            return Ok(page.Value.Response);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Audit Proof Critical Changes", $"Error while fetching audit proof critical changes of ticket {ticketId}.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
