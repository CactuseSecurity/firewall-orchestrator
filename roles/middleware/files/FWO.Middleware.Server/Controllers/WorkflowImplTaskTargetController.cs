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
    /// The targets follow the provisioning settings addressObjectCreation resp. serviceObjectCreation of the
    /// sub-managements of the super-management: Submanager targets the sub-management, Supermanager the
    /// super-management. A management without sub-managements is its own target. The targets are resolved with
    /// the rights of the middleware server, so the result does not depend on the managements the caller sees.
    /// <para>
    /// Callers other than admin may only ask for a management visible to them (JWT claim
    /// x-hasura-visible-managements); any other management answers 403.
    /// </para>
    /// </remarks>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.WorkflowRolesList}, {Roles.Modeller}")]
    [HttpPost("getImplTaskTargets")]
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
        if (!CallerCanSeeManagement(managementId))
        {
            return Forbid();
        }
        try
        {
            return Ok(await implTaskTargetService.GetImplTaskTargetsAsync(managementId, request.IsServiceObject, request.Options.Filter));
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Implementation Task Targets", $"Error while resolving the target managements of management {managementId}.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private bool CallerCanSeeManagement(int managementId)
    {
        return User.IsInRole(Roles.Admin) || WorkflowController.GetClaimIds(User, kVisibleManagementsClaim).Contains(managementId);
    }
}
