using FWO.Basics;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Controller class for compliance zone resolution.
/// </summary>
[Authorize]
[ApiController]
[Tags("Compliance")]
[Route("api/Compliance")]
public class ComplianceZoneController(ComplianceZoneService complianceZoneService) : ControllerBase
{
    /// <summary>
    /// Returns the network zones of the configured designated zone matrix.
    /// </summary>
    /// <returns>The matrix zones, or an empty list if no matrix is configured.</returns>
    [HttpGet("designatedZoneMatrix/zones")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [ProducesResponseType(typeof(List<ComplianceDesignatedZoneResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ComplianceDesignatedZoneResponse>>> GetDesignatedZoneMatrixZones()
    {
        try
        {
            List<ComplianceDesignatedZoneResponse> zones = await complianceZoneService.GetDesignatedZoneMatrixZonesAsync();
            return Ok(zones);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Designated Zone Matrix Zones", "Error while getting designated zone matrix zones.", exception);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Returns the zones occupied by object trees.
    /// Only IPv4 leaf addresses are supported; IPv6 ranges are rejected during validation.
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
        catch (Exception exception)
        {
            Log.WriteError("Resolve Zones For Objects", "Error while resolving object zones.", exception);
            return StatusCode(500);
        }
    }
}
