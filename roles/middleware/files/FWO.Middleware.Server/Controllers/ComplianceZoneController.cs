using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Controller class for compliance zone resolution.
/// </summary>
[Authorize]
[ApiController]
[Tags("Compliance")]
[Route("api/Compliance")]
public class ComplianceZoneController(ComplianceZoneService complianceZoneService, GlobalConfig globalConfig) : ControllerBase
{
    /// <summary>
    /// Returns the network zones of the configured designated zone matrix.
    /// </summary>
    /// <remarks>
    /// Zones are ordered by id, so the query parameters <c>limit</c> and <c>offset</c> page the result
    /// deterministically. Without a limit a page of at most the configured <c>maxRestResponseObjectLimit</c> zones
    /// (default 10000) is returned, and the <c>X-Has-More</c> response header tells whether further zones follow.
    /// The IP ranges of a zone are always returned completely.
    /// </remarks>
    /// <param name="limit">Maximum number of zones returned, between 1 and the configured maximum; omitted returns a page of the maximum size.</param>
    /// <param name="offset">Number of zones skipped before the first returned zone, at least 0; omitted skips no zone.</param>
    /// <returns>The matrix zones, or an empty list if no matrix is configured.</returns>
    [HttpGet("designatedZoneMatrix/zones")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [ProducesResponseType(typeof(List<ComplianceDesignatedZoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ComplianceDesignatedZoneResponse>>> GetDesignatedZoneMatrixZones(
        [FromQuery] int? limit = null, [FromQuery] int? offset = null)
    {
        int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
        Dictionary<string, string[]> pagingErrors = ListPaging.ValidatePaging(limit, offset, maxLimit);
        if (pagingErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(pagingErrors) { Status = StatusCodes.Status400BadRequest });
        }

        try
        {
            ListPage<ComplianceDesignatedZoneResponse> zones = await complianceZoneService.GetDesignatedZoneMatrixZonesAsync(
                ListPaging.GetPageSize(limit, maxLimit), offset);
            ListPaging.SetHasMoreHeader(HttpContext, zones.HasMore);
            return Ok(zones.Items);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Designated Zone Matrix Zones", "Error while getting designated zone matrix zones.", exception);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Returns the zones occupied by object trees.
    /// Each leaf accepts exactly one address representation: ipHost, ipNetwork, or a two-entry
    /// ipRange. Hosts and range endpoints must be maskless. Address text follows the shared FWO
    /// parser; CIDRs with host bits are normalized to their inclusive network bounds.
    /// </summary>
    /// <param name="request">The object tree to resolve.</param>
    [HttpPost("resolveZonesForObjects")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [ProducesResponseType(typeof(List<ComplianceDesignatedZoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ComplianceDesignatedZoneResponse>>> ResolveZonesForObjects([FromBody] ResolveZonesForObjectsRequest request)
    {
        if (!ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        try
        {
            return Ok(await complianceZoneService.ResolveZonesForObjectsAsync(request));
        }
        catch (UnassignableIpRangesException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (Exception exception)
        {
            Log.WriteError("Resolve Zones For Objects", "Error while resolving object zones.", exception);
            return StatusCode(500);
        }
    }
}
