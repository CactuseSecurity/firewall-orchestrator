using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FWO.Report;
using System.Security.Claims;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Controller class for compliance import and report api.
    /// </summary>
    [Authorize]
    [ApiController]
    [Tags("Compliance")]
    [Route("api/[controller]")]
    public class ComplianceController(ApiConnection apiConnection, JwtWriter jwtWriter, List<Ldap> ldaps) : ControllerBase
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
        /// <remarks>
        /// The report is generated with the caller's own api permissions from the last compliance check results.
        /// It covers the requested managements, or all managements that are visible to the caller and relevant
        /// for the compliance check if no management ids are given.
        /// </remarks>
        /// <param name="parameters">ComplianceReportParameters</param>
        /// <returns>Report as csv string, or the validation errors if a requested management is not accessible</returns>
        [HttpPost("Report")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
        public async Task<ActionResult<string>> Get([FromBody] ComplianceReportParameters parameters)
        {
            try
            {
                AuthManager authManager = new(jwtWriter, ldaps, apiConnection);
                // the local user id keeps the rebuilt user in the caller's directory, as the dn alone may exist in several (SEC-11)
                UiUser targetUser = new()
                {
                    Name = User.FindFirstValue("unique_name") ?? "",
                    Dn = User.FindFirstValue("x-hasura-uuid") ?? "",
                    DbId = int.TryParse(User.FindFirstValue("x-hasura-user-id"), out int callerDbId) ? callerDbId : 0
                };
                string jwt = await authManager.AuthorizeUserAsync(targetUser, validatePassword: false);
                using ApiConnection apiConnectionUserContext = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
                apiConnectionUserContext.SetBestRoleForReport(User, ReportType.ComplianceReport);

                using GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
                using UserConfig userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnectionUserContext, targetUser.DbId);

                return await GenerateReport(parameters.ManagementIds, apiConnectionUserContext, userConfig, HttpContext?.RequestAborted ?? CancellationToken.None);
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Compliance Report", "Error while getting report.", exception);
            }
            return "";
        }

        /// <summary>
        /// Generates the compliance report restricted to the requested managements. Every requested management
        /// must be visible through the given api connection and relevant for the compliance check, otherwise the
        /// request is rejected instead of silently returning another scope (SEC-22).
        /// </summary>
        /// <param name="requestedManagementIds">Requested management ids, empty for all accessible managements.</param>
        /// <param name="apiConnectionUserContext">Api connection carrying the caller's permissions.</param>
        /// <param name="userConfig">Config of the caller.</param>
        /// <param name="cancellationToken">Cancels the generation when the request ends.</param>
        /// <returns>Report as csv string or the validation errors.</returns>
        internal async Task<ActionResult<string>> GenerateReport(List<int> requestedManagementIds, ApiConnection apiConnectionUserContext,
            UserConfig userConfig, CancellationToken cancellationToken)
        {
            ReportCompliance reportCompliance = new(new(""), userConfig, ReportType.ComplianceReport);
            await reportCompliance.GetManagementAndDevices(apiConnectionUserContext);

            RequestValidationErrorResponse validationErrors = ValidateManagementIds(requestedManagementIds, reportCompliance.Managements);
            if (validationErrors.Errors.Count > 0)
            {
                return BadRequest(validationErrors);
            }

            reportCompliance.ManagementScope = [.. requestedManagementIds.Distinct()];
            await reportCompliance.Generate(userConfig.ElementsPerFetch, apiConnectionUserContext, _ => Task.CompletedTask, cancellationToken);
            return reportCompliance.ExportToCsv();
        }

        private static RequestValidationErrorResponse ValidateManagementIds(List<int> requestedManagementIds, List<Management> accessibleManagements)
        {
            HashSet<int> accessibleManagementIds = [.. accessibleManagements.Select(management => management.Id)];
            RequestValidationErrorResponse validationErrors = new();
            for (int index = 0; index < requestedManagementIds.Count; index++)
            {
                if (!accessibleManagementIds.Contains(requestedManagementIds[index]))
                {
                    validationErrors.Errors.Add(new()
                    {
                        Path = $"managementIds[{index}]",
                        Message = $"Management {requestedManagementIds[index]} does not exist, is not visible to the caller or is not relevant for the compliance check."
                    });
                }
            }
            return validationErrors;
        }
    }
}
