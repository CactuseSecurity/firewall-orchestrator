using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Controller class for compliance zone resolution and compliance checks.
/// </summary>
[Authorize]
[ApiController]
[Route("api/Compliance")]
public class ComplianceCheckController(
    ApiConnection apiConnection,
    ComplianceCheckStatusTracker complianceCheckStatusTracker,
    ComplianceZoneService complianceZoneService) : ControllerBase
{
    /// <summary>
    /// Returns the network zones of the configured designated zone matrix.
    /// </summary>
    /// <returns>The matrix zones, or an empty list if no matrix is configured.</returns>
    [HttpGet("DesignatedZoneMatrix/Zones")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
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
    /// </summary>
    /// <param name="request">The object tree to resolve.</param>
    [HttpPost("ResolveZonesForObjects")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
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

    /// <summary>
    /// Compliance Check
    /// </summary>
    /// <returns></returns>
    [HttpGet("ComplianceCheck")]
    [Authorize(Roles = $"{Roles.Admin}")]
    public async Task<bool> InitialComplianceCheck()
    {
        try
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);
            ComplianceCheck complianceCheck = new(userConfig, apiConnection);
            await complianceCheck.RunComplianceCheck(ComplianceCheckType.Variable);
            await complianceCheck.PersistDataAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts an initial compliance check asynchronously.
    /// </summary>
    /// <returns>The identifier of the started job.</returns>
    [HttpPost("ComplianceCheck/Start")]
    [Authorize(Roles = $"{Roles.Admin}")]
    public ActionResult<ComplianceCheckStartResult> StartInitialComplianceCheck()
    {
        ComplianceCheckJobStatus? activeJob = complianceCheckStatusTracker.GetActiveJob();
        if (activeJob is not null)
        {
            return Conflict(new ComplianceCheckStartResult
            {
                JobId = activeJob.JobId
            });
        }

        ComplianceCheckJobStatus jobStatus = complianceCheckStatusTracker.CreateQueuedJob();

        _ = Task.Run(async () =>
        {
            try
            {
                complianceCheckStatusTracker.SetRunning(jobStatus.JobId);

                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);
                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                await complianceCheck.RunComplianceCheck(ComplianceCheckType.Variable);
                await complianceCheck.PersistDataAsync();

                complianceCheckStatusTracker.SetSucceeded(jobStatus.JobId);
            }
            catch (Exception exception)
            {
                Log.WriteError("Initial Compliance Check", "Error while executing initial compliance check.", exception);
                complianceCheckStatusTracker.SetFailed(jobStatus.JobId, exception.Message);
            }
        });

        return Accepted(new ComplianceCheckStartResult
        {
            JobId = jobStatus.JobId
        });
    }

    /// <summary>
    /// Returns the current status of an asynchronously started initial compliance check.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The current job status.</returns>
    [HttpGet("ComplianceCheck/Status/{jobId}")]
    [Authorize(Roles = $"{Roles.Admin}")]
    public ActionResult<ComplianceCheckJobStatus> GetInitialComplianceCheckStatus(string jobId)
    {
        ComplianceCheckJobStatus? jobStatus = complianceCheckStatusTracker.Get(jobId);
        if (jobStatus is null)
        {
            return NotFound();
        }

        return Ok(jobStatus);
    }
}
