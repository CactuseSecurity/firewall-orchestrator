using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Controller class for compliance checks.
/// </summary>
[Authorize]
[ApiController]
[Tags("Compliance")]
[Route("api/Compliance")]
public class ComplianceCheckController(
    ApiConnection apiConnection,
    ComplianceCheckStatusTracker complianceCheckStatusTracker) : ControllerBase
{
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
