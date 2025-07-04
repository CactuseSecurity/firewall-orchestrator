using FWO.Api.Client;
using FWO.Api.Client.Queries;
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
        /// Get Compliance Report
        /// </summary>
        /// <param name="parameters">ComplianceReportParameters</param>
        /// <returns>Report as json string</returns>
        [HttpPost("Get")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Reporter}, {Roles.ReporterViewAll}, {Roles.FwAdmin}, {Roles.Recertifier}")]
        public async Task<string> Get([FromBody] ComplianceReportParameters parameters)
        {
            try
            {
                GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(GlobalConfig, apiConnection, new(){ Language = GlobalConst.kEnglish });

                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunicationsOutput = await complianceCheck.CheckApps(parameters.AppIds);
                return ConvertOutput(forbiddenCommunicationsOutput);
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Compliance Report", "Error while getting report.", exception);
            }
            return "";
        }

        private static string ConvertOutput(List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunicationsOutput)
        {
            return JsonSerializer.Serialize(forbiddenCommunicationsOutput);
        }
    }
}
