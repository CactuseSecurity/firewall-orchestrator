using FWO.Basics;
using FWO.Basics.Exceptions;
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
/// Provides path analysis endpoints.
/// </summary>
[Authorize]
[ApiController]
[AggregatedValidationErrors]
[Tags("PathAnalysis")]
[Route("api/pathAnalysis")]
public class PathAnalysisController : ControllerBase
{
    private readonly PathAnalysisService pathAnalysisService;
    private readonly GlobalConfig globalConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathAnalysisController"/> class.
    /// </summary>
    /// <param name="pathAnalysisService">Runs the path analyses.</param>
    /// <param name="globalConfig">Supplies the configured default algorithm and zone matrix for validation.</param>
    public PathAnalysisController(PathAnalysisService pathAnalysisService, GlobalConfig globalConfig)
    {
        this.pathAnalysisService = pathAnalysisService;
        this.globalConfig = globalConfig;
    }

    /// <summary>
    /// Returns the firewall devices on the paths between every source and every destination.
    /// </summary>
    /// <remarks>
    /// Every source is combined with every destination. The result lists each device once and in no particular
    /// order, together with the algorithm and zone matrix actually used. Omitted options fall back to the
    /// configured defaults.
    /// <para>
    /// 400 reports every invalid key at once, including a missing option whose configured default is known to
    /// be unusable. 404 reports a requested zone matrix that does not exist or holds no zones. A configured
    /// designated zone matrix that turns out to be unusable when the analysis runs is a server failure and
    /// answers 500; naming 'options.matrixId' explicitly avoids it.
    /// </para>
    /// </remarks>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("resolvePathDevices")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(ResolvePathDevicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResolvePathDevicesResponse>> ResolvePathDevices([FromBody] ResolvePathDevicesRequest request)
    {
        RequestValidationErrorResponse validationErrors = ResolvePathDevicesRequestValidator.Validate(
            request, globalConfig.PathAnalysisAlgorithm, globalConfig.DesignatedZoneMatrixId);
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
            return NotFound(ResolvePathDevicesRequestValidator.BuildUnknownMatrixError(exception.MatrixId));
        }
        catch (Exception exception)
        {
            Log.WriteError("Resolve Path Devices", "Error while resolving path devices.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
