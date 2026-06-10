using FWO.Basics;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides flow compliance endpoints.
/// </summary>
[Authorize(Roles = $"{Roles.Admin}")]
[ApiController]
[Route("api/flow")]
public class FlowComplianceController : ControllerBase
{
    private readonly FlowComplianceService flowComplianceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowComplianceController"/> class.
    /// </summary>
    /// <param name="flowComplianceService">The flow compliance service.</param>
    public FlowComplianceController(FlowComplianceService flowComplianceService)
    {
        this.flowComplianceService = flowComplianceService;
    }

    /// <summary>
    /// Returns the compliance state for the requested flows.
    /// </summary>
    [HttpPost("getFlowComplianceState")]
    public async Task<ActionResult<List<FlowComplianceStateResponse>>> GetFlowComplianceState([FromBody] GetFlowComplianceStateRequest request)
    {
        if (!FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowComplianceService.GetFlowComplianceStateAsync(request));
    }

    /// <summary>
    /// Returns the policy identifiers for the current dataset.
    /// </summary>
    [HttpPost("getPolicyIds")]
    public async Task<ActionResult<List<PolicyIdResponse>>> GetPolicyIds([FromBody] GetPolicyIdsRequest request)
    {
        if (!FlowComplianceRequestValidator.TryValidatePolicyIds(request, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowComplianceService.GetPolicyIdsAsync());
    }
}
