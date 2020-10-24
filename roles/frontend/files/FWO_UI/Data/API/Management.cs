using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class Management
    {
        [JsonPropertyName("mgm_id")]
        public int Id { get; set; }

        [JsonPropertyName("mgm_name")]
        public string Name { get; set; }

        [JsonPropertyName("ssh_private_key")]
        public string PrivateKey { get; set; }

        [JsonPropertyName("devices")]
        public Device[] Devices { get; set; }

        [JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; }

        [JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; }

        [JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; }

        [JsonPropertyName("stm_dev_typ")]
        public DeviceType DeviceType { get; set; }
    }

    public class ReturnManagement
    {
        [JsonPropertyName("returning")]
        public Management[] ReturnId { get; set; }
    }

    public static class ManagementUtility
    {
        public static bool Merge(this Management[] managements, Management[] managementsToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < managements.Length; i++)
            {
                if (managements[i].Id == managementsToMerge[i].Id)
                {
                    if (managements[i].Objects != null && managementsToMerge[i].Objects != null && managementsToMerge[i].Objects.Length > 0)
                    {
                        managements[i].Objects = managements[i].Objects.Concat(managementsToMerge[i].Objects).ToArray();
                        newObjects = true;
                    }                       

                    if (managements[i].Services != null && managementsToMerge[i].Services != null && managementsToMerge[i].Services.Length > 0)
                    {
                        managements[i].Services = managements[i].Services.Concat(managementsToMerge[i].Services).ToArray();
                        newObjects = true;
                    }

                    if (managements[i].Users != null && managementsToMerge[i].Users != null && managementsToMerge[i].Users.Length > 0)
                    {
                        managements[i].Users = managements[i].Users.Concat(managementsToMerge[i].Users).ToArray();
                        newObjects = true;
                    }
                      
                    if (managements[i].Devices != null && managementsToMerge[i].Devices != null && managementsToMerge[i].Devices.Length > 0)
                    {
                        newObjects = managements[i].Devices.Merge(managementsToMerge[i].Devices);
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
