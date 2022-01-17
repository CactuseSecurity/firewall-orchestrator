using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ManagementSelect
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public List<DeviceSelect> Devices { get; set; } = new List<DeviceSelect>();
    }

    public class DeviceSelect
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }
        
        public bool Selected { get; set; } = false;
    }

    public class DeviceFilter
    {
        [JsonProperty("management"), JsonPropertyName("management")]
        public List<ManagementSelect> Managements { get; set; } = new List<ManagementSelect>();


        public DeviceFilter()
        {}

        public DeviceFilter(List<int> devIds)
        {
            ManagementSelect dummyManagement = new ManagementSelect();
            foreach(int id in devIds)
            {
                dummyManagement.Devices.Add(new DeviceSelect(){Id = id});
            }
            Managements.Add(dummyManagement);
        }

        public bool areAllDevicesSelected()
        {
            foreach (ManagementSelect management in Managements)
                foreach (DeviceSelect device in management.Devices)
                    if (!device.Selected)
                        return false;
            return true;
        }

        public bool isAnyDeviceFilterSet()
        {
            foreach (ManagementSelect management in Managements)
                foreach (DeviceSelect device in management.Devices)
                    if (device.Selected)
                        return true;
            return false;
        }

        public void applyFullDeviceSelection(bool selectAll)
        {
            foreach (ManagementSelect management in Managements)
                foreach (DeviceSelect device in management.Devices)
                    device.Selected = selectAll;
        }

        public static bool IsSelectedManagement(ManagementSelect management)
        {
            foreach (DeviceSelect device in management.Devices)
            {
                if (device.Selected)
                {
                    return true;
                }
            }     
            return false;
        }

        public List<int> getSelectedManagements()
        {
            List<int> selectedMgmts = new List<int>();
            foreach (ManagementSelect mgmt in Managements)
            {
                if (IsSelectedManagement(mgmt))
                {
                    selectedMgmts.Add(mgmt.Id);
                }
            }
            return selectedMgmts;
        }

        public string listAllSelectedDevices()
        {
            List<string> devs = new List<string>();
            foreach (ManagementSelect mgmt in Managements)
                foreach (DeviceSelect dev in mgmt.Devices)
                    if (dev.Selected)
                        devs.Add(dev.Name ?? "");
            return string.Join(", ", devs);
        }

        public static List<int> ExtractAllDevIds(Management[] managements)
        {
            List<int> devs = new List<int>();
            foreach (Management mgmt in managements)
                foreach (Device dev in mgmt.Devices)
                    devs.Add(dev.Id);
            return devs;
        }

        public static List<int> ExtractSelectedDevIds(Management[] managements)
        {
            List<int> selectedDevs = new List<int>();
            foreach (Management mgmt in managements)
                foreach (Device dev in mgmt.Devices)
                    if (dev.Selected)
                        selectedDevs.Add(dev.Id);
            return selectedDevs;
        }
    }
}
