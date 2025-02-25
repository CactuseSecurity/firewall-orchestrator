using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Middleware.RequestParameters;


namespace FWO.Data
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
        public TenantGateway[] TenantGateways { get; set; } = []; // TODO: Replace with Device[] (probably not possible)

        [JsonProperty("tenant_to_managements"), JsonPropertyName("tenant_to_managements")]
        public TenantManagement[] TenantManagements { get; set; } = [];

        public int[] VisibleGatewayIds { get; set; } = [];
        public int[] VisibleManagementIds { get; set; } = [];

        public TenantViewManagement[] TenantVisibleManagements { get; set; } = [];
        public TenantViewGateway[] TenantVisibleGateways { get; set; } = [];


        public Tenant()
        {}

        public Tenant(Tenant tenant)
        {
            Id = tenant.Id;
            Name = tenant.Name;
            Comment = tenant.Comment;
            Project = tenant.Project;
            ViewAllDevices = tenant.ViewAllDevices;
            TenantGateways = tenant.TenantGateways;
            TenantManagements = tenant.TenantManagements;

            foreach (TenantGateway gateway in tenant.TenantGateways)
            {
                VisibleGatewayIds = [.. VisibleGatewayIds, gateway.VisibleGateway.Id];
            }
        }

        public Tenant(TenantGetReturnParameters tenantGetParameters)
        {
            Id = tenantGetParameters.Id;
            Name = tenantGetParameters.Name;
            Comment = tenantGetParameters.Comment;
            Project = tenantGetParameters.Project;
            ViewAllDevices = tenantGetParameters.ViewAllDevices;
            List<TenantViewGateway> deviceList = [];

            foreach(int id in VisibleGatewayIds)
            {
                TenantVisibleGateways = [.. TenantVisibleGateways, new TenantViewGateway(id, "", true)];
            }

            TenantVisibleGateways = [.. deviceList]; // ???
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
            TenantGetReturnParameters tenantGetParams = new()
            {
                Id = Id,
                Name = Name,
                Comment = Comment,
                Project = Project,
                ViewAllDevices = ViewAllDevices,
                VisibleGateways = [],
                VisibleManagements = [],
                SharedGateways = [],
                UnfilteredGateways = [],
                SharedManagements = [],
                UnfilteredManagements = []
            };

            foreach (var gateway in TenantGateways)
            {
                tenantGetParams.VisibleGateways.Add(new TenantViewGateway(gateway.VisibleGateway.Id, gateway.VisibleGateway.Name ?? ""));
                if (gateway.Shared)
                {
                    tenantGetParams.SharedGateways.Add(new TenantViewGateway(gateway.VisibleGateway.Id, gateway.VisibleGateway.Name ?? ""));
                }
                else
                {
                    tenantGetParams.UnfilteredGateways.Add(new TenantViewGateway(gateway.VisibleGateway.Id, gateway.VisibleGateway.Name ?? "", false));
                }
            }
            foreach (var mgm in TenantManagements) 
            {
                tenantGetParams.VisibleManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name ?? ""));
                if (mgm.Shared)
                {
                    tenantGetParams.SharedManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name ?? ""));
                }
                else
                {
                    tenantGetParams.UnfilteredManagements.Add(new TenantViewManagement(mgm.VisibleManagement.Id, mgm.VisibleManagement.Name ?? ""));
                }
            }
            return tenantGetParams;
        }

        public TenantEditParameters ToApiUpdateParams()
        {
            TenantEditParameters tenantUpdateParams = new()
            {
                Id = Id,
                Comment = Comment,
                Project = Project,
                ViewAllDevices = ViewAllDevices
            };
            return tenantUpdateParams;
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
