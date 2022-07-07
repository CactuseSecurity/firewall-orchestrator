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

        public bool Selected { get; set; } = false;
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
            {
                management.Selected = selectAll;
                foreach (DeviceSelect device in management.Devices)
                {
                    device.Selected = selectAll;
                }
            }
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

        public void SynchronizeDevFilter(DeviceFilter incomingDevFilter)
        {
            // unknown incoming devices (e.g. from templates) are ignored, because they have been removed inbetween
            foreach (ManagementSelect management in Managements)
            {
                ManagementSelect? incomingMgt = incomingDevFilter.Managements.Find(x => x.Id == management.Id);
                if (incomingMgt != null)
                {
                    foreach (DeviceSelect device in management.Devices)
                    {
                        DeviceSelect? incomingDev = incomingMgt.Devices.Find(x => x.Id == device.Id);
                        if (incomingDev != null)
                        {
                            device.Selected = incomingDev.Selected;
                        }
                    }
                }
            }
            SynchronizeMgmtFilter();
        }

        public void SynchronizeMgmtFilter()
        {
            foreach (ManagementSelect management in Managements)
            {
                management.Selected = false;
                if(management.Devices.Count > 0)
                {
                    management.Selected = true;
                }
                foreach (DeviceSelect device in management.Devices)
                {
                    if (!device.Selected)
                    {
                        management.Selected = false;
                    }
                }
            }
        }

        public int NumberMgmtDev()
        {
            int counter = 0;
            foreach (ManagementSelect management in Managements)
            {
                counter ++;
                foreach (DeviceSelect device in management.Devices)
                {
                    counter ++;
                }
            }
            return counter;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
