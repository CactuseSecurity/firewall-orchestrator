using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.Api
{
    public class Device
    {
        [JsonPropertyName("dev_id")]
        public int Id { get; set; }

        [JsonPropertyName("dev_name")]
        public string Name { get; set; }

        [JsonPropertyName("rules")]
        public Rule[] Rules { get; set; }
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
                    throw new NotSupportedException("Managements have to be in the same order in oder to merge.");
                }
            }

            return newObjects;
        }
    }
}
