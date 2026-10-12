using FWO.Basics;
using FWO.Basics.Exceptions;
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
[Route("api/path_analysis")]
[AggregatedValidationErrors]
public class PathAnalysisController : ControllerBase
{
    private readonly PathAnalysisService pathAnalysisService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowChangeHistoryController"/> class.
    /// </summary>
    /// <param name="pathAnalysisService">The workflow change history service.</param>
    public PathAnalysisController(PathAnalysisService pathAnalysisService)
    {
        this.pathAnalysisService = pathAnalysisService;
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
    /// </remarks>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("resolvePathDevices")]
    [ProducesResponseType(typeof(ResolvePathDevicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResolvePathDevicesResponse>> ResolvePathDevices([FromBody] ResolvePathDevicesRequest request)
    {
        RequestValidationErrorResponse validationErrors = ResolvePathDevicesRequestValidator.Validate(request);
        if (validationErrors.Errors.Count > 0)
        {
            return BadRequest(validationErrors);
        }

        try
        {
            return Ok(await pathAnalysisService.ResolvePathDevicesAsync(request));
        }
        catch (ZoneMatrixUnavailableException exception) when (request.Options.MatrixId != null)
        {
            // Only a matrix id the caller supplied is the caller's fault. One taken from the configured
            // designated zone matrix falls through to the general handler below and is logged as a server failure.
            return NotFound(ResolvePathDevicesRequestValidator.BuildUnknownMatrixError(exception.MatrixId));
        }
        catch (Exception exception)
        {
            Log.WriteError("Resolve Path Devices", "Error while resolving path devices.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
