using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using FWO.Report;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Controller class for compliance api
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ComplianceController(ApiConnection apiConnection, ComplianceCheckStatusTracker complianceCheckStatusTracker) : ControllerBase
    {
        /// <summary>
        /// Import Compliance Matrix
        /// </summary>
        /// <param name="parameters">ComplianceImportMatrixParameters</param>
        /// <returns>Failed import filenames</returns>
        [HttpPost("ImportMatrix")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<string> Post([FromBody] ComplianceImportMatrixParameters parameters)
        {
            try
            {
                GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                ZoneMatrixDataImport matrixDataImport = new(apiConnection, GlobalConfig);
                return await matrixDataImport.Run(parameters.FileName, parameters.Data, parameters.UserName, parameters.UserDn);
            }
            catch (Exception exception)
            {
                Log.WriteError("Import Compliance Matrix", "Error while importing matrix.", exception);
                return exception.Message;
            }
        }

        /// <summary>
        /// Get Compliance Report
        /// </summary>
        /// <param name="parameters">ComplianceReportParameters</param>
        /// <returns>Report as json string</returns>
        [HttpPost("Report")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Reporter}, {Roles.ReporterViewAll}, {Roles.FwAdmin}, {Roles.Recertifier}")]
        public async Task<string> Get([FromBody] ComplianceReportParameters parameters)
        {
            try
            {
                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);

                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                await complianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

                ReportCompliance reportCompliance = new(new(""), userConfig, ReportType.ComplianceReport);
                await reportCompliance.GetManagementAndDevices(apiConnection);
                List<Management> relevantManagements = ComplianceCheck.GetRelevantManagements(globalConfig, reportCompliance.Managements!);
                reportCompliance.Managements = relevantManagements;
                reportCompliance.GetViewDataFromRules(complianceCheck.RulesInCheck!);
                string reportString = reportCompliance.ExportToCsv();
                return reportString;
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Compliance Report", "Error while getting report.", exception);
            }
            return "";
        }

        /// <summary>
        /// Returns the network zones of the configured designated zone matrix.
        /// </summary>
        /// <returns>The matrix zones, or an empty list if no matrix is configured.</returns>
        [HttpGet("DesignatedZoneMatrix/Zones")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        public async Task<ActionResult<List<ComplianceNetworkZone>>> GetDesignatedZoneMatrixZones()
        {
            try
            {
                List<ComplianceNetworkZone> zones = await LoadDesignatedZoneMatrixZonesAsync();
                return Ok(zones);
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Designated Zone Matrix Zones", "Error while getting designated zone matrix zones.", exception);
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

        /// <summary>
        /// Loads the zones belonging to the configured designated matrix.
        /// </summary>
        private async Task<List<ComplianceNetworkZone>> LoadDesignatedZoneMatrixZonesAsync()
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, false);
            if (globalConfig.ComplianceDesignatedZoneMatrixId <= 0)
            {
                return [];
            }

            return await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(
                ComplianceQueries.getNetworkZonesForMatrix,
                new { criterionId = globalConfig.ComplianceDesignatedZoneMatrixId }) ?? [];
        }

        private static string ConvertOutput(List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> forbiddenCommunicationsOutput)
        {
            return JsonSerializer.Serialize(forbiddenCommunicationsOutput);
        }
    }
}
