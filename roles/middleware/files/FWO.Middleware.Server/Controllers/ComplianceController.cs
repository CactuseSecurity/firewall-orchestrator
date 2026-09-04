using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FWO.Report;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Controller class for compliance import and report api.
    /// </summary>
    [Authorize]
    [ApiController]
    [Tags("Compliance")]
    [Route("api/[controller]")]
    public class ComplianceController(ApiConnection apiConnection) : ControllerBase
    {
        /// <summary>
        /// Import Compliance Matrix
        /// </summary>
        /// <param name="parameters">ImportMatrixParameters</param>
        /// <returns>Failed import filenames</returns>
        [HttpPost("ImportMatrix")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<string> Post([FromBody] ImportMatrixParameters parameters)
        {
            try
            {
                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                ZoneMatrixDataImport matrixDataImport = new(apiConnection, globalConfig);
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
    }
}
