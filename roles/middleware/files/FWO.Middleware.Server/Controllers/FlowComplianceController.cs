using FWO.Basics;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides read-only flow compliance endpoints.
/// These endpoints are role-authorized, but they are not filtered on a modeller or owner basis.
/// </summary>
[Authorize]
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
    /// Returns the compliance state for the requested flows using shared compliance data.
    /// This evaluation is not scoped to a modeller or owner.
    /// Source and destination ranges support IPv4 and IPv6 addresses through ipStart and ipEnd.
    /// Optional host masks (/32 and /128) are ignored; all other masks are rejected.
    /// CIDR networks must use ipNetwork, must carry the network address itself, and are expanded to
    /// range boundaries before evaluation.
    /// Criteria that only support IPv4 report an IPv6 flow as NotAssessable instead of as a violation.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
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
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getPolicyIds")]
    public async Task<ActionResult<GetPolicyIdsResponse>> GetPolicyIds([FromBody] GetPolicyIdsRequest request)
    {
        if (!FlowComplianceRequestValidator.TryValidatePolicyIds(request, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(new GetPolicyIdsResponse
        {
            Policies = await flowComplianceService.GetPolicyIdsAsync()
        });
    }
}
