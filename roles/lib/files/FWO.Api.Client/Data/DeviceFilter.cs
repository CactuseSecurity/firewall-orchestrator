using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
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
        public List<DeviceSelect> Devices { get; set; } = [];

        public ElementReference? UiReference { get; set; }

        public bool Visible { get; set; } = true;
        public bool Selected { get; set; } = false;
        public bool Shared { get; set; } = true;
        public ManagementSelect Clone()
        {
            List<DeviceSelect> ClonedDevices = [];
            foreach(var dev in Devices)
            {
                ClonedDevices.Add(new DeviceSelect(dev));
            }

			return new ManagementSelect()
            {
                Id = Id,
                Name = Name,
                Devices = ClonedDevices,
                UiReference = UiReference,
                Visible = Visible,
                Selected = Selected
            };
        }
    }

    public class DeviceSelect
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        public bool Visible { get; set; } = true;

        public bool Selected { get; set; } = false;
        public bool Shared { get; set; } = true;
        public DeviceSelect()
        {}

        public DeviceSelect(DeviceSelect dev)
        {
            Id = dev.Id;
            Name = dev.Name;
            Visible = dev.Visible;
            Selected = dev.Selected;
        }
    }

    public class DeviceFilter
    {
        [JsonProperty("management"), JsonPropertyName("management")]
        public List<ManagementSelect> Managements { get; set; } = [];

        [JsonProperty("visibleManagements"), JsonPropertyName("visibleManagements")]
        public List<ManagementSelect> VisibleManagements { get; set; } = [];

        [JsonProperty("visibleGateways"), JsonPropertyName("visibleGateways")]
        public List<DeviceSelect> VisibleGateways { get; set; } = [];

        public DeviceFilter()
        {}

        public DeviceFilter(DeviceFilter devFilter)
        {
            Managements = new List<ManagementSelect>(devFilter.Managements);
        }

        public DeviceFilter(List<ManagementSelect> mgmSelect)
         {
            Managements =  new List<ManagementSelect>(mgmSelect);
        }        
        public DeviceFilter(List<int> devIds)
        {
            ManagementSelect dummyManagement = new();
            foreach(int id in devIds)
            {
                dummyManagement.Devices.Add(new DeviceSelect(){Id = id});
            }
            Managements.Add(dummyManagement);
        }
        public DeviceFilter(int[] devIds)
        {
            ManagementSelect dummyManagement = new();
            foreach(int id in devIds)
            {
                dummyManagement.Devices.Add(new DeviceSelect(){Id = id});
            }
            Managements.Add(dummyManagement);
        }

        public DeviceFilter Clone()
        {
            List<ManagementSelect> ClonedManagements = [];
            foreach(var mgt in Managements)
            {
                ClonedManagements.Add(mgt.Clone());
            }

			return new DeviceFilter()
            {
                Managements = ClonedManagements
            };
        }

        public bool areAllDevicesSelected()
        {
            foreach (ManagementSelect management in Managements)
                foreach (DeviceSelect device in management.Devices)
                    if (!device.Selected && device.Visible)
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
                // only select visible managements
                management.Selected = selectAll && management.Visible;
                foreach (DeviceSelect device in management.Devices)
                {
                    // only select visible devices
                    device.Selected = selectAll && device.Visible;
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
            List<int> selectedMgmts = [];
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
            List<string> devs = [];
            foreach (ManagementSelect mgmt in Managements)
                foreach (DeviceSelect dev in mgmt.Devices)
                    if (dev.Selected)
                        devs.Add(dev.Name ?? "");
            return string.Join(", ", devs);
        }

        public static List<int> ExtractAllDevIds(Management[] managements)
        {
            List<int> devs = [];
            foreach (Management mgmt in managements)
                foreach (Device dev in mgmt.Devices)
                    devs.Add(dev.Id);
            return devs;
        }

        public static List<int> ExtractSelectedDevIds(Management[] managements)
        {
            List<int> selectedDevs = [];
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
                            // the next line could be the problem as it changes an object:
                            if (device.Visible)
                            {
                                device.Selected = incomingDev.Selected;
                            }
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
                int selectedDevicesCount = management.Devices.Where(d => d.Selected).Count();
                int visibleDevicesCount = management.Devices.Where(d => d.Visible).Count();
                // Management is selected if all visible devices are selected
                management.Selected = management.Devices.Count > 0 && selectedDevicesCount == visibleDevicesCount;
            }
        }

        public int NumberMgmtDev()
        {
            int counter = 0;
            foreach (ManagementSelect management in Managements)
            {
                if (management.Visible)
                {
                    counter++;
                    foreach (DeviceSelect device in management.Devices)
                    {
                        if (device.Visible)
                        {
                            counter++;
                        }
                    }
                }
            }
            return counter;
        }

        public override string ToString()
        {
            StringBuilder result = new();
            foreach (ManagementSelect management in Managements)
            {
                result.Append($"{management.Name} [{string.Join(", ", management.Devices.ConvertAll(device => device.Name))}]; ");
            }
            return result.ToString();
        }
    }
}
