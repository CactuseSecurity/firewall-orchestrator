using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report;
using FWO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        private ApiConnection? apiConnectionUserContext = null;
        private UserConfig? userConfig = null;

        /// <summary>
        /// Get Report
        /// </summary>
        /// <param name="parameters">ReportGetParameters</param>
        /// <returns>Report as json string</returns>
        [HttpPost("Get")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Reporter}, {Roles.ReporterViewAll}, {Roles.Modeller}, {Roles.Recertifier}")]
        public async Task<string> Get([FromBody] ReportGetParameters parameters)
        {
            try
            {
                if(!await InitUserEnvironment() || apiConnectionUserContext == null || userConfig == null)
                {
                    return "";  // todo: Error message?
                }

                ReportBase? report = await ReportGenerator.GenerateFromTemplate(await ConvertParameters(parameters), apiConnectionUserContext, userConfig, DefaultInit.DoNothing);
                return report?.ExportToJson() ?? "";
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Report", "Error while getting report.", exception);
            }
            return "";
        }

        private async Task<bool> InitUserEnvironment()
        {
            AuthManager authManager = new (jwtWriter, ldaps, apiConnection);
            UiUser targetUser = new() { Name = User.FindFirstValue("unique_name") ?? "", Dn = User.FindFirstValue("x-hasura-uuid") ?? "" };
            string jwt = await authManager.AuthorizeUserAsync(targetUser, validatePassword: false);
            apiConnectionUserContext = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
            apiConnectionUserContext.SetProperRole(User, [Roles.Admin, Roles.Auditor, Roles.Reporter, Roles.ReporterViewAll, Roles.Modeller, Roles.Recertifier]);
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
            userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnectionUserContext, targetUser.DbId);
            return true;
        }

        private async Task<ReportTemplate> ConvertParameters(ReportGetParameters reportApiParams)
        {
            ReportTemplate template = new();
            if(reportApiParams != null)
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
                _ => 1,
            };
        }

        private async Task<DeviceFilter> ConstructDeviceFilter(ApiDeviceFilter apiDeviceFilter)
        {
            DeviceFilter deviceFilter = new();
            if(apiDeviceFilter.ManagementIds.Count > 0 || apiDeviceFilter.DeviceIds.Count > 0)
            {
                try
                {
                    List<ManagementSelect> managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
                    foreach(ManagementSelect mgt in managements)
                    {
                        foreach (DeviceSelect device in mgt.Devices)
                        {
                            if(apiDeviceFilter.ManagementIds.Contains(mgt.Id) || apiDeviceFilter.DeviceIds.Contains(device.Id))
                            {
                                mgt.Selected = mgt.Visible;
                                device.Selected = device.Visible;
                            }
                        }
                        if(mgt.Selected)
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
            if(ipOrFilters.Count > 0)
            {
                allAndFilters.Add("(" + string.Join(" or ", ipOrFilters) + ")");
            }

            if(apiRuleFilter.Services.Count > 0)
            {
                List<string> serviceOrFilters = await ConstructServiceFilters(apiRuleFilter.Services);
                if(serviceOrFilters.Count > 0)
                {
                    allAndFilters.Add("(" + string.Join(" or ", serviceOrFilters) + ")");
                }
            }

            if(!string.IsNullOrEmpty(action))
            {
                allAndFilters.Add($"action={action}");
            }
            if(active != null)
            {
                allAndFilters.Add($"disabled={!active}");
            }
            return string.Join(" and ", allAndFilters);;
        }

        private async Task<List<string>> ConstructServiceFilters(List<ApiService> apiServices)
        {
            List<string> serviceOrFilters = [];
            try
            {
                List<IpProtocol> ipProtos = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols);
                foreach(ApiService service in apiServices)
                {
                    List<string> serviceSubFilters = [];
                    if(!string.IsNullOrEmpty(service.Name))
                    {
                        serviceSubFilters.Add($"svc={service.Name}");
                    }
                    if(service.Protocol != null)
                    {
                        string? protoNameFromId = ipProtos.FirstOrDefault(p => p.Id == service.Protocol)?.Name;
                        if(!string.IsNullOrEmpty(protoNameFromId))
                        {
                            serviceSubFilters.Add($"protocol={protoNameFromId}");
                        }
                    }
                    if(service.Port != null)
                    {
                        serviceSubFilters.Add($"port={service.Port}");
                    }
                    if(serviceSubFilters.Count > 0)
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
