using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace FWO.Ui.Data.API
{
    public class Tenant
    {
        [JsonPropertyName("tenant_id")]
        public int Id { get; set; }

        [JsonPropertyName("tenant_name")]
        public string Name { get; set; }

        [JsonPropertyName("tenant_comment")]
        public string Comment { get; set; }

        [JsonPropertyName("tenant_projekt")]
        public string Project { get; set; }

        [JsonPropertyName("tenant_can_view_all_devices")]
        public bool ViewAllDevices { get; set; }

        [JsonPropertyName("tenant_is_superadmin")]
        public bool Superadmin { get; set; }

        [JsonPropertyName("tenant_to_devices")]
        public TenantDevice[] TenantDevices { get; set; }

        public string DeviceList()
        {
            List<string> deviceList = new List<string>();
            foreach(TenantDevice device in TenantDevices)
            {
                deviceList.Add(device.VisibleDevice.Name);
            }
            return string.Join(", ", deviceList);
        }
    }

    public class TenantDevice
    {
        [JsonPropertyName("device")]
        public Device VisibleDevice { get; set; }
    }
}
