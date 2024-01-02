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

        // [JsonProperty("sharedGateways"), JsonPropertyName("sharedGateways")]
        // public TenantViewGateway[] TenantSharedGateways { get; set; } // TODO: Replace with Device[] (probably not possible)

        // [JsonProperty("sharedManagements"), JsonPropertyName("sharedManagements")]
        // public TenantViewManagement[] TenantSharedManagements { get; set; } // TODO: Replace with Device[] (probably not possible)

        // [JsonProperty("unfilteredGateways"), JsonPropertyName("unfilteredGateways")]
        // public TenantViewGateway[] TenantUnfilteredGateways { get; set; } // TODO: Replace with Device[] (probably not possible)

        // [JsonProperty("unfilteredManagements"), JsonPropertyName("unfilteredManagements")]
        // public TenantViewManagement[] TenantUnfilteredManagements { get; set; } // TODO: Replace with Device[] (probably not possible)

        public int[] VisibleGatewayIds { get; set; } = Array.Empty<int>();
        public int[] SharedGatewayIds { get; set; } = Array.Empty<int>();
        public int[] UnfilteredGatewayIds { get; set; } = Array.Empty<int>();
        public int[] VisibleManagementIds { get; set; } = Array.Empty<int>();
        public int[] SharedManagementIds { get; set; } = Array.Empty<int>();
        public int[] UnfilteredManagementIds { get; set; } = Array.Empty<int>();

        public TenantViewManagement[] TenantVisibleManagements { get; set; } = Array.Empty<TenantViewManagement>();
        public TenantViewGateway[] TenantVisibleGateways { get; set; } = Array.Empty<TenantViewGateway>();


        public Tenant()
        {
            TenantGateways = new TenantGateway[]{};
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

            // TenantSharedGateways = tenant.TenantSharedGateways;
            // TenantSharedManagements = tenant.TenantSharedManagements;

            // TenantUnfilteredGateways = tenant.TenantUnfilteredGateways;
            // TenantUnfilteredManagements = tenant.TenantUnfilteredManagements;

            // TenantVisibleGateways = tenant.TenantSharedGateways.Concat(tenant.TenantUnfilteredGateways).ToArray();

            // foreach (TenantViewGateway gateway in TenantSharedGateways)
            // {
            //     VisibleGatewayIds.Append(gateway.Id);
            // }
            // foreach (TenantViewGateway gateway in TenantUnfilteredGateways)
            // {
            //     VisibleGatewayIds.Append(gateway.Id);
            // }
            // foreach (TenantViewManagement mgm in TenantSharedManagements)
            // {
            //     VisibleManagementIds.Append(mgm.Id);
            // }
            // foreach (TenantViewManagement mgm in TenantUnfilteredManagements)
            // {
            //     VisibleManagementIds.Append(mgm.Id);
            // }
            // TenantVisibleGateways = tenant.TenantSharedGateways.Concat(tenant.TenantUnfilteredGateways).ToArray();

            foreach (TenantGateway gateway in TenantGateways)
            {
                // array = array.Concat(new int[] { 2 }).ToArray();
                VisibleGatewayIds = VisibleGatewayIds.Concat(new int[] { gateway.VisibleGateway.Id }).ToArray();
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
                // VisibleGatewayIds = TenantVisibleGateways.VisibleGatewayIds.Concat(new int[] { gateway.VisibleGateway.Id }).ToArray();

                TenantVisibleGateways.Append(new TenantViewGateway(id, "", true));
            }

            // foreach(TenantViewGateway gw in tenantGetParameters.SharedGateways)
            // {
            //     deviceList.Add(new TenantViewGateway(){Id = gw.Id, Name = gw.Name, shared = true});
            // }
            // foreach(TenantViewGateway gw in tenantGetParameters.UnfilteredGateways)
            // {
            //     deviceList.Add(new TenantViewGateway(){Id = gw.Id, Name = gw.Name, shared = true});
            // }

            TenantVisibleGateways = deviceList.ToArray();
            // TenantVisibleManagements = new int[]{};
        }

        public string DeviceList()
        {
            List<string> deviceList = new List<string>();
            foreach (TenantViewGateway gw in TenantVisibleGateways)
            {
                if (gw.Name != null)
                    deviceList.Add(gw.Name);
            }
            return string.Join(", ", deviceList);
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

            foreach (var mgm in TenantManagements) 
            {
                tenantGetParams.VisibleManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name != null ? mgm.VisibleManagement.Name : ""));
                if (mgm.Shared)
                {
                    tenantGetParams.SharedManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name != null ? mgm.VisibleManagement.Name : ""));
                    // TODO: add visible gateways under this management
                }
                else
                {
                    tenantGetParams.UnfilteredManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name != null ? mgm.VisibleManagement.Name : ""));
                    // TODO: add all gateways under this management as unfiltered
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

        public static async Task<Tenant> getSingleTenant(ApiConnection conn, int tenantId)
        {
            Tenant[] tenants = Array.Empty<Tenant>(); 
            tenants = await conn.SendQueryAsync<Tenant[]>(AuthQueries.getTenants, new { tenant_id = tenantId });
            if (tenants.Length>0)
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
