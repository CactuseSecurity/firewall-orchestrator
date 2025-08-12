using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Controller class for compliance api
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ComplianceController(ApiConnection apiConnection) : ControllerBase
    {
        /// <summary>
        /// Import Compliance Matrix
        /// </summary>
        /// <param name="parameters">ComplianceImportMatrixParameters</param>
        /// <returns>Failed import filenames</returns>
        [HttpPost("ImportMatrix")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<List<string>> Post([FromBody] ComplianceImportMatrixParameters parameters)
        {
            try
            {
                GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                 ZoneMatrixDataImport matrixDataImport = new(apiConnection, GlobalConfig);
                List<string> failedImports = await matrixDataImport.Run(parameters.FileName, parameters.Data);
                return failedImports;
            }
            catch (Exception exception)
            {
                Log.WriteError("Import Compliance Matrix", "Error while importing matrix.", exception);
            }
            return [];
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
                GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(GlobalConfig, apiConnection, new(){ Language = GlobalConst.kEnglish });

                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                await complianceCheck.CheckAll();
                List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> forbiddenCommunicationsOutput = complianceCheck.Results;
                return ConvertOutput(forbiddenCommunicationsOutput);
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Compliance Report", "Error while getting report.", exception);
            }
            return "";
        }

        private static string ConvertOutput(List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> forbiddenCommunicationsOutput)
        {
            return JsonSerializer.Serialize(forbiddenCommunicationsOutput);
        }
    }
}
