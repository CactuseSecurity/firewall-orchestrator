using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Report.Filter.Exceptions;
using FWO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
	/// Controller class for role api
	/// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController(JwtWriter jwtWriter, List<Ldap> ldaps, ApiConnection apiConnection) : ControllerBase
    {
        private readonly ApiConnection apiConnection = apiConnection;
        private readonly JwtWriter jwtWriter = jwtWriter;
        private readonly List<Ldap> ldaps = ldaps;

        /// <summary>
        /// Get Report
        /// </summary>
        /// <param name="parameters">ReportGetParameters. Each list holds at most
        /// <see cref="ReportGetParametersValidator.kMaxFilterValues"/> values (device filter ids at most
        /// <see cref="ReportGetParametersValidator.kMaxDeviceFilterIds"/>), each text at most
        /// <see cref="ReportGetParametersValidator.kMaxFilterTextLength"/> characters, and IP values must be
        /// addresses, CIDR networks or ranges.</param>
        /// <returns>Report as json string; 400 with all field errors when the request is invalid.</returns>
        [HttpPost("Get")]
        [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
        [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Reporter}, {Roles.ReporterViewAll}, {Roles.Modeller}, {Roles.Recertifier}")]
        public async Task<ActionResult<string>> Get([FromBody] ReportGetParameters parameters)
        {
            Dictionary<string, string[]> validationErrors = ReportGetParametersValidator.Validate(parameters);
            if (validationErrors.Count > 0)
            {
                return BadRequest(new ValidationProblemDetails(validationErrors) { Status = StatusCodes.Status400BadRequest });
            }

            try
            {
                ReportTemplate template = await ConvertParameters(parameters);
                ValidationProblemDetails? filterError = ValidateFilter(template);
                if (filterError != null)
                {
                    return BadRequest(filterError);
                }

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
                ReportType reportType = (ReportType)template.ReportParams.ReportType;

                using GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
                using UserConfig userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnectionUserContext, targetUser.DbId);
                apiConnectionUserContext.SetBestRoleForReport(User, reportType, userConfig.GetExplicitlyDeniedRoles(reportType));

                ReportBase? report = await ReportGenerator.GenerateFromTemplate(template, apiConnectionUserContext, userConfig, DefaultInit.DoNothing);

                return report?.ExportToJson() ?? "";
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Report", "Error while getting report.", exception);
            }
            return "";
        }

        /// <summary>
        /// Checks the report filter built from the request before any report work is done. Compiling builds the query
        /// without accessing the database, so syntax and semantic errors are both found.
        /// </summary>
        /// <returns>The validation problem for a too long or invalid filter; null when the filter compiles.</returns>
        private static ValidationProblemDetails? ValidateFilter(ReportTemplate template)
        {
            if (template.Filter.Length > Compiler.kMaxFilterLength)
            {
                return BuildFilterError($"'rule-filter' together with 'action' and 'active' results in a report filter longer than {Compiler.kMaxFilterLength} characters. Request fewer values.");
            }
            try
            {
                Compiler.Compile(template);
                return null;
            }
            catch (FilterException exception)
            {
                return BuildFilterError($"'rule-filter' together with 'action' and 'active' results in an invalid report filter: {exception.Message}");
            }
        }

        private static ValidationProblemDetails BuildFilterError(string message)
        {
            Dictionary<string, string[]> errors = [];
            string[] filterErrors = new string[1];
            filterErrors[0] = message;
            errors["rule-filter"] = filterErrors;
            return new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest };
        }

        /// <summary>
        /// Quotes a free-text value for the report filter, so that blanks, brackets or keywords in it stay part
        /// of the value instead of changing the filter.
        /// </summary>
        internal static string QuoteFilterValue(string value)
        {
            string escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escapedValue}\"";
        }

        private async Task<ReportTemplate> ConvertParameters(ReportGetParameters reportApiParams)
        {
            ReportTemplate template = new();
            if (reportApiParams != null)
            {
                template.ReportParams.ReportType = ConstructReportType(reportApiParams.ApiReportType, reportApiParams.ApiReportView);
                template.ReportParams.DeviceFilter = await ConstructDeviceFilter(reportApiParams.ApiDeviceFilter);
                template.Filter = await ConstructFilter(reportApiParams.ApiRuleFilter, reportApiParams.Action, reportApiParams.Active);
            }
            return template;
        }

        private static int ConstructReportType(string apiReportType, List<string> apiReportView)
        {
            return apiReportType.ToLower().Trim() switch
            {
                "rules" => apiReportView.FirstOrDefault(x => x.ToLower() == "resolved") == null ? 1 : apiReportView.FirstOrDefault(x => x.ToLower() == "technical") == null ? 5 : 6,
                "changes" => apiReportView.FirstOrDefault(x => x.ToLower() == "resolved") == null ? 2 : apiReportView.FirstOrDefault(x => x.ToLower() == "technical") == null ? 8 : 9,
                "statistics" => 3,
                "natrules" => 4,
                "recertification" => 7,
                "unusedrules" => 10,
                "connections" => 21,
                "apprules" => 22,
                "variance" => 23,
                "owners" => 51,
                _ => 1,
            };
        }

        private async Task<DeviceFilter> ConstructDeviceFilter(ApiDeviceFilter apiDeviceFilter)
        {
            DeviceFilter deviceFilter = new();
            if (apiDeviceFilter.ManagementIds.Count > 0 || apiDeviceFilter.DeviceIds.Count > 0)
            {
                try
                {
                    List<ManagementSelect> managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
                    foreach (ManagementSelect mgt in managements)
                    {
                        foreach (DeviceSelect device in mgt.Devices.Where(d => apiDeviceFilter.ManagementIds.Contains(mgt.Id) || apiDeviceFilter.DeviceIds.Contains(d.Id)))
                        {
                            mgt.Selected = mgt.Visible;
                            device.Selected = device.Visible;
                        }
                        if (mgt.Selected)
                        {
                            deviceFilter.Managements.Add(mgt);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Construct Device Filters", $" leads to exception: {exception.Message}");
                }
            }
            return deviceFilter;
        }

        private async Task<string> ConstructFilter(ApiRuleFilter apiRuleFilter, string? action, bool? active)
        {
            List<string> allAndFilters = [];
            List<string> ipOrFilters = apiRuleFilter.SourceIps.ConvertAll(s => "src=" + s);
            ipOrFilters.AddRange(apiRuleFilter.DestinationIps.ConvertAll(d => "dst=" + d));
            ipOrFilters.AddRange(apiRuleFilter.Ips.ConvertAll(i => "src=" + i));
            ipOrFilters.AddRange(apiRuleFilter.Ips.ConvertAll(i => "dst=" + i));
            if (ipOrFilters.Count > 0)
            {
                allAndFilters.Add("(" + string.Join(" or ", ipOrFilters) + ")");
            }

            if (apiRuleFilter.Services.Count > 0)
            {
                List<string> serviceOrFilters = await ConstructServiceFilters(apiRuleFilter.Services);
                if (serviceOrFilters.Count > 0)
                {
                    allAndFilters.Add("(" + string.Join(" or ", serviceOrFilters) + ")");
                }
            }

            if (!string.IsNullOrEmpty(action))
            {
                allAndFilters.Add($"action={QuoteFilterValue(action)}");
            }
            if (active != null)
            {
                allAndFilters.Add($"disabled={!active}");
            }
            return string.Join(" and ", allAndFilters);
        }

        private async Task<List<string>> ConstructServiceFilters(List<ApiService> apiServices)
        {
            List<string> serviceOrFilters = [];
            try
            {
                List<IpProtocol> ipProtos = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols);
                foreach (ApiService service in apiServices)
                {
                    List<string> serviceSubFilters = [];
                    if (!string.IsNullOrEmpty(service.Name))
                    {
                        serviceSubFilters.Add($"svc={QuoteFilterValue(service.Name)}");
                    }
                    if (service.Protocol != null)
                    {
                        string? protoNameFromId = ipProtos.FirstOrDefault(p => p.Id == service.Protocol)?.Name;
                        if (!string.IsNullOrEmpty(protoNameFromId))
                        {
                            serviceSubFilters.Add($"protocol={protoNameFromId}");
                        }
                    }
                    if (service.Port != null)
                    {
                        serviceSubFilters.Add($"port={service.Port}");
                    }
                    if (serviceSubFilters.Count > 0)
                    {
                        serviceOrFilters.Add(string.Join(" and ", serviceSubFilters));
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("Construct Service Filters", $" leads to exception: {exception.Message}");
            }
            return serviceOrFilters;
        }
    }
}
