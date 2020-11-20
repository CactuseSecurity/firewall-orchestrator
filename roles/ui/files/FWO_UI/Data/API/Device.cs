using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class Device
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; }

        [JsonPropertyName("management")]
        public Management Management { get; set; }

        [JsonPropertyName("rulebase")]
        public string Rulebase { get; set; }

        [JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonPropertyName("hideInUi")]
        public bool HideInUi { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; }

        [JsonPropertyName("rules")]
        public Rule[] Rules { get; set; }

        public Device()
        {}
        
        public Device(Device device)
        {
            Id = device.Id;
            Name = device.Name;
            if (device.DeviceType != null)
            {
                DeviceType = new DeviceType(device.DeviceType);
            }
            if (device.Management != null)
            {
                Management = new Management(device.Management);
            }
            Rulebase = device.Rulebase;
            ImportDisabled = device.ImportDisabled;
            HideInUi = device.HideInUi;
            Comment = device.Comment;
            // Rules = device.Rules;
        }
    }

    public static class DeviceUtility
    {
        public static bool Merge(this Device[] devices, Device[] devicesToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].Id == devicesToMerge[i].Id)
                {
                    if (devices[i].Rules != null && devicesToMerge[i].Rules != null && devicesToMerge[i].Rules.Length > 0)
                    {
                        devices[i].Rules = devices[i].Rules.Concat(devicesToMerge[i].Rules).ToArray();
                        newObjects = true;
                    }
                }
                else
                {
                    throw new NotSupportedException("Devices have to be in the same order in oder to merge.");
                }
            }

            return newObjects;
        }
    }
}
