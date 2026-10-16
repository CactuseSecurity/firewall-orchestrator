using FWO.Basics;
using FWO.Data.Workflow;
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
/// Provides the target managements of workflow object tasks.
/// </summary>
[Authorize]
[ApiController]
[Route("api/workflow")]
[AggregatedValidationErrors]
public class WorkflowImplTaskTargetController : ControllerBase
{
    private const string kVisibleManagementsClaim = "x-hasura-visible-managements";
    private readonly ImplTaskTargetService implTaskTargetService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowImplTaskTargetController"/> class.
    /// </summary>
    /// <param name="implTaskTargetService">The implementation task target service.</param>
    public WorkflowImplTaskTargetController(ImplTaskTargetService implTaskTargetService)
    {
        this.implTaskTargetService = implTaskTargetService;
    }

    /// <summary>
    /// Returns the managements an object task (group or single object) gets one implementation task each on.
    /// </summary>
    /// <remarks>
    /// The targets follow the device specific settings addressObjectCreation, serviceObjectCreation resp.
    /// timeObjectCreation of the sub-managements of the super-management: Submanager targets the sub-management,
    /// Supermanager the super-management, ignore none of them. A management without sub-managements is its own
    /// target unless it is set to ignore itself. The list is empty when every candidate is set to ignore, which the
    /// settings allow for time objects only. The targets are resolved with
    /// the rights of the middleware server, so the result does not depend on the managements the caller sees.
    /// <para>
    /// Callers other than admin may only ask for a management visible to them (JWT claim
    /// x-hasura-visible-managements); any other management answers 403. Targets not visible to such a caller
    /// are returned with their id only and an empty name, so the endpoint does not disclose the names of
    /// managements hidden from the caller; the name filter does not match them either.
    /// </para>
    /// <para>
    /// Targets are ordered by name and then id, visible targets first, so <c>options.limit</c> and
    /// <c>options.offset</c> page the result deterministically. Without a limit a page of at most
    /// <see cref="GetImplTaskTargetsOptions.kMaxLimit"/> targets is returned, and the <c>X-Has-More</c> response
    /// header tells whether further targets follow. Every call resolves the targets again, so the endpoint carries
    /// the expensive rate-limit policy.
    /// </para>
    /// </remarks>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.WorkflowRolesList}, {Roles.Modeller}")]
    [HttpPost("getImplTaskTargets")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(GetImplTaskTargetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetImplTaskTargetsResponse>> GetImplTaskTargets([FromBody] GetImplTaskTargetsRequest request)
    {
        RequestValidationErrorResponse validationErrors = GetImplTaskTargetsRequestValidator.Validate(request);
        if (validationErrors.Errors.Count > 0)
        {
            return BadRequest(validationErrors);
        }

        // Validation rejects both an absent and a non-positive managementId, so a value is present here.
        int managementId = request.ManagementId.GetValueOrDefault();
        HashSet<int>? visibleManagementIds = GetVisibleManagementIds();
        if (visibleManagementIds != null && !visibleManagementIds.Contains(managementId))
        {
            return Forbid();
        }
        try
        {
            ListPage<ImplTaskTargetResponse> page = await implTaskTargetService.GetImplTaskTargetsAsync(managementId, GetObjectKind(request),
                request.Options, visibleManagementIds);
            ListPaging.SetHasMoreHeader(HttpContext, page.HasMore);
            return Ok(new GetImplTaskTargetsResponse { Targets = page.Items });
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Implementation Task Targets", $"Error while resolving the target managements of management {managementId}.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Returns the managements visible to the caller, or null for the admin, who sees every management.
    /// </summary>
    private HashSet<int>? GetVisibleManagementIds()
    {
        return User.IsInRole(Roles.Admin) ? null : WorkflowController.GetClaimIds(User, kVisibleManagementsClaim);
    }

    /// <summary>
    /// Returns the kind of objects the request asks the targets for; validation excludes both flags being set.
    /// </summary>
    private static ObjectTaskKind GetObjectKind(GetImplTaskTargetsRequest request)
    {
        if (request.IsTimeObject)
        {
            return ObjectTaskKind.Time;
        }
        return request.IsServiceObject ? ObjectTaskKind.Service : ObjectTaskKind.Network;
    }
}
