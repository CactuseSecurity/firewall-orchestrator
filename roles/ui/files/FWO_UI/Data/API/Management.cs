using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Ui.Data.API
{
    public class Management
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; }

        [JsonPropertyName("user")]
        public string ImportUser { get; set; }

        [JsonPropertyName("secret")]
        public string PrivateKey { get; set; }

        [JsonPropertyName("configPath")]
        public string ConfigPath { get; set; }

        [JsonPropertyName("importerHostname")]
        public string ImporterHostname { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("sshPublicKey")]
        public string PublicKey { get; set; }

        [JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonPropertyName("forceInitialImport")]
        public bool ForceInitialImport { get; set; }

        [JsonPropertyName("hideInUi")]
        public bool HideInUi { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; }

        [JsonPropertyName("tenant_id")]
        public int TenantId { get; set; }

        [JsonPropertyName("devices")]
        public Device[] Devices { get; set; }

        [JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; }

        [JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; }

        [JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; }

        [JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; }

        public Management()
        {}
        
        public Management(Management management)
        {
            Id = management.Id;
            Name = management.Name;
            Hostname = management.Hostname;
            ImportUser = management.ImportUser;
            PrivateKey = management.PrivateKey;
            ConfigPath = management.ConfigPath;
            ImporterHostname = management.ImporterHostname;
            Port = management.Port;
            PublicKey = management.PublicKey;
            ImportDisabled = management.ImportDisabled;
            ForceInitialImport = management.ForceInitialImport;
            HideInUi = management.HideInUi;
            Comment = management.Comment;
            TenantId = management.TenantId;
            // Devices = management.Devices;
            // Objects = management.Objects;
            // Services = management.Services;
            // Users = management.Users;
            if(management.DeviceType != null)
            {
                DeviceType = new DeviceType(management.DeviceType);
            }
        }
    }

    public class ReturnId
    {
        [JsonPropertyName("newId")]
        public int NewId { get; set; }

        [JsonPropertyName("UpdatedId")]
        public int UpdatedId { get; set; }
    }
    public class NewReturning
    {
        [JsonPropertyName("returning")]
        public ReturnId[] ReturnIds { get; set; }
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
