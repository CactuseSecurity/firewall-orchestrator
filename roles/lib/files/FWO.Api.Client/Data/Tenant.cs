using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Middleware.RequestParameters;
using FWO.Api.Client;
using FWO.Api.Client.Queries;

namespace FWO.Api.Data
{
    public class Tenant
    {
        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int Id { get; set; }

        [JsonProperty("tenant_name"), JsonPropertyName("tenant_name")]
        public string Name { get; set; } = "";

        [JsonProperty("tenant_comment"), JsonPropertyName("tenant_comment")]
        public string? Comment { get; set; }

        [JsonProperty("tenant_projekt"), JsonPropertyName("tenant_projekt")]
        public string? Project { get; set; }

        [JsonProperty("tenant_can_view_all_devices"), JsonPropertyName("tenant_can_view_all_devices")]
        public bool ViewAllDevices { get; set; }

        [JsonProperty("tenant_is_superadmin"), JsonPropertyName("tenant_is_superadmin")]
        public bool Superadmin { get; set; } // curently not in use

        [JsonProperty("tenant_to_devices"), JsonPropertyName("tenant_to_devices")]
        public TenantGateway[] TenantGateways { get; set; } // TODO: Replace with Device[] (probably not possible)

        [JsonProperty("tenant_to_managements"), JsonPropertyName("tenant_to_managements")]
        public TenantManagement[] TenantManagements { get; set; }

        public int[] VisibleGatewayIds { get; set; } = Array.Empty<int>();
        public int[] VisibleManagementIds { get; set; } = Array.Empty<int>();

        public TenantViewManagement[] TenantVisibleManagements { get; set; } = Array.Empty<TenantViewManagement>();
        public TenantViewGateway[] TenantVisibleGateways { get; set; } = Array.Empty<TenantViewGateway>();


        public Tenant()
        {
            TenantGateways = new TenantGateway[]{};
            TenantManagements = new TenantManagement[]{};
            VisibleGatewayIds = new int[]{};
            VisibleManagementIds = new int[]{};
        }

        public Tenant(Tenant tenant)
        {
            Id = tenant.Id;
            Name = tenant.Name;
            Comment = tenant.Comment;
            Project = tenant.Project;
            ViewAllDevices = tenant.ViewAllDevices;
            TenantGateways = tenant.TenantGateways;
            TenantManagements = tenant.TenantManagements;

            if (tenant.TenantGateways != null)
            {
                foreach (TenantGateway gateway in tenant.TenantGateways)
                    {
                        VisibleGatewayIds = VisibleGatewayIds.Concat(new int[] { gateway.VisibleGateway.Id }).ToArray();
                    }
            }
            else
            {
                TenantGateways = new TenantGateway[]{};
                VisibleGatewayIds = new int[]{};
            }
        }

        public Tenant(TenantGetReturnParameters tenantGetParameters)
        {
            Id = tenantGetParameters.Id;
            Name = tenantGetParameters.Name;
            Comment = tenantGetParameters.Comment;
            Project = tenantGetParameters.Project;
            ViewAllDevices = tenantGetParameters.ViewAllDevices;
            List<TenantViewGateway> deviceList = new List<TenantViewGateway>();

            foreach(int id in VisibleGatewayIds)
            {
                TenantVisibleGateways.Append(new TenantViewGateway(id, "", true));
            }

            TenantVisibleGateways = deviceList.ToArray();
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeLdapNameMand(Name, ref shortened);
            Comment = Sanitizer.SanitizeOpt(Comment, ref shortened);
            Project = Sanitizer.SanitizeOpt(Project, ref shortened);
            return shortened;
        }

        public TenantGetReturnParameters ToApiParams()
        {
            TenantGetReturnParameters tenantGetParams = new TenantGetReturnParameters
            {
                Id = this.Id,
                Name = this.Name,
                Comment = this.Comment,
                Project = this.Project,
                ViewAllDevices = this.ViewAllDevices,
                VisibleGateways = new List<TenantViewGateway>(),
                VisibleManagements = new List<TenantViewManagement>(),
                SharedGateways = new List<TenantViewGateway>(),
                UnfilteredGateways = new List<TenantViewGateway>(),
                SharedManagements = new List<TenantViewManagement>(),
                UnfilteredManagements = new List<TenantViewManagement>()
            };

            if (TenantGateways != null)
            {
                foreach (var gateway in TenantGateways)
                {
                    tenantGetParams.VisibleGateways.Add(new TenantViewGateway(gateway.VisibleGateway.Id, gateway.VisibleGateway.Name != null ? gateway.VisibleGateway.Name : ""));
                    if (gateway.Shared)
                    {
                        tenantGetParams.SharedGateways.Add(new TenantViewGateway(gateway.VisibleGateway.Id, gateway.VisibleGateway.Name != null ? gateway.VisibleGateway.Name : ""));
                    }
                    else
                    {
                        tenantGetParams.UnfilteredGateways.Add(new TenantViewGateway(gateway.VisibleGateway.Id, gateway.VisibleGateway.Name != null ? gateway.VisibleGateway.Name : "", false));
                    }
                }
            }

            if (TenantManagements != null)
            {
                foreach (var mgm in TenantManagements) 
                {
                    tenantGetParams.VisibleManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name != null ? mgm.VisibleManagement.Name : ""));
                    if (mgm.Shared)
                    {
                        tenantGetParams.SharedManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name != null ? mgm.VisibleManagement.Name : ""));
                    }
                    else
                    {
                        tenantGetParams.UnfilteredManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name != null ? mgm.VisibleManagement.Name : ""));
                    }
                }
            }
            return tenantGetParams;
        }

        public TenantEditParameters ToApiUpdateParams()
        {
            TenantEditParameters tenantUpdateParams = new TenantEditParameters
            {
                Id = this.Id,
                Comment = this.Comment,
                Project = this.Project,
                ViewAllDevices = this.ViewAllDevices
            };
            return tenantUpdateParams;
        }

        public static async Task<Tenant?> getSingleTenant(ApiConnection conn, int tenantId)
        {
            Tenant[] tenants = Array.Empty<Tenant>(); 
            tenants = await conn.SendQueryAsync<Tenant[]>(AuthQueries.getTenants, new { tenant_id = tenantId });
            if (tenants.Length > 0)
            {
                return tenants[0];
            }
            else
            {
                return null;
            }
        }

        // the following method adds device visibility information to a tenant (fetched from API)
        public async Task AddDevices(ApiConnection conn)
        {
            var tenIdObj = new { tenantId = Id };

            Device[] deviceIds = await conn.SendQueryAsync<Device[]>(AuthQueries.getVisibleDeviceIdsPerTenant, tenIdObj, "getVisibleDeviceIdsPerTenant");
            VisibleGatewayIds = Array.ConvertAll(deviceIds, device => device.Id);

            Management[] managementIds = await conn.SendQueryAsync<Management[]>(AuthQueries.getVisibleManagementIdsPerTenant, tenIdObj, "getVisibleManagementIdsPerTenant");
            VisibleManagementIds = Array.ConvertAll(managementIds, management => management.Id);
        }

    }

    public class TenantGateway
    {
        [JsonProperty("device"), JsonPropertyName("device")]
        public Device VisibleGateway { get; set; } = new Device();

        [JsonProperty("shared"), JsonPropertyName("shared")]
        public bool Shared { get; set; } = false;
    }

    public class TenantManagement
    {
        [JsonProperty("management"), JsonPropertyName("management")]
        public Management VisibleManagement { get; set; } = new Management();

        [JsonProperty("shared"), JsonPropertyName("shared")]
        public bool Shared { get; set; } = false;
    }

}
