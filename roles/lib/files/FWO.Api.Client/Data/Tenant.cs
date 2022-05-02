using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Middleware.RequestParameters;

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
        public TenantDevice[] TenantDevices { get; set; } // TODO: Replace with Device[] (probably not possible)

        public int[] VisibleDevices { get; set; } // TODO: Remove later (probably not possible)
        public int[] VisibleManagements { get; set; } // TODO: Remove later (probably not possible)

        public Tenant()
        {
            TenantDevices = new TenantDevice[]{};
            VisibleDevices = new int[]{};
            VisibleManagements = new int[]{};
        }

        public Tenant(Tenant tenant)
        {
            Id = tenant.Id;
            Name = tenant.Name;
            Comment = tenant.Comment;
            Project = tenant.Project;
            ViewAllDevices = tenant.ViewAllDevices;
            // Superadmin = tenant.Superadmin;
            TenantDevices = tenant.TenantDevices;
            VisibleDevices = tenant.VisibleDevices;
            VisibleManagements = tenant.VisibleManagements;
        }

        public Tenant(TenantGetReturnParameters tenantGetParameters)
        {
            Id = tenantGetParameters.Id;
            Name = tenantGetParameters.Name;
            Comment = tenantGetParameters.Comment;
            Project = tenantGetParameters.Project;
            ViewAllDevices = tenantGetParameters.ViewAllDevices;
            // Superadmin = tenantGetParameters.Superadmin;
            List<TenantDevice> deviceList = new List<TenantDevice>();
            if (tenantGetParameters.Devices != null)
            {
                foreach(TenantViewDevice apiDevice in tenantGetParameters.Devices)
                {
                    Device visibleDevice = new Device(){Id = apiDevice.Id, Name = apiDevice.Name};
                    deviceList.Add(new TenantDevice(){VisibleDevice = visibleDevice});
                }
            }
            TenantDevices = deviceList.ToArray();
            VisibleDevices = new int[]{};
            VisibleManagements = new int[]{};
        }

        public string DeviceList()
        {
            List<string> deviceList = new List<string>();
            foreach (TenantDevice device in TenantDevices)
            {
                if (device.VisibleDevice.Name != null)
                    deviceList.Add(device.VisibleDevice.Name);
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
                // Superadmin = this.Superadmin,
                Devices = new List<TenantViewDevice>()
            };
            foreach (TenantDevice device in TenantDevices)
            {
                tenantGetParams.Devices.Add(new TenantViewDevice(){ Id = device.VisibleDevice.Id, Name = (device.VisibleDevice.Name != null ? device.VisibleDevice.Name : "")});
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
    }

    public class TenantDevice
    {
        [JsonProperty("device"), JsonPropertyName("device")]
        public Device VisibleDevice { get; set; } = new Device();
    }
}
