using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class Management
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("hostname"), JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "";

        [JsonProperty("user"), JsonPropertyName("user")]
        public string? ImportUser { get; set; }

        [JsonProperty("secret"), JsonPropertyName("secret")]
        public string PrivateKey { get; set; } = "";

        [JsonProperty("configPath"), JsonPropertyName("configPath")]
        public string ConfigPath { get; set; } = "";

        [JsonProperty("importerHostname"), JsonPropertyName("importerHostname")]
        public string ImporterHostname { get; set; } = "";

        [JsonProperty("port"), JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonProperty("sshPublicKey"), JsonPropertyName("sshPublicKey")]
        public string? PublicKey { get; set; }

        [JsonProperty("importDisabled"), JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonProperty("forceInitialImport"), JsonPropertyName("forceInitialImport")]
        public bool ForceInitialImport { get; set; }

        [JsonProperty("hideInUi"), JsonPropertyName("hideInUi")]
        public bool HideInUi { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("debugLevel"), JsonPropertyName("debugLevel")]
        public int? DebugLevel { get; set; }

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int TenantId { get; set; }

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public Device[] Devices { get; set; } = new Device[]{};

        [JsonProperty("networkObjects"), JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; } = new NetworkObject[]{};

        [JsonProperty("serviceObjects"), JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; } = new NetworkService[]{};

        [JsonProperty("userObjects"), JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; } = new NetworkUser[]{};

        [JsonProperty("reportNetworkObjects"), JsonPropertyName("reportNetworkObjects")]
        public NetworkObjectWrapper[] ReportObjects { get; set; } = new NetworkObjectWrapper[]{};

        [JsonProperty("reportServiceObjects"), JsonPropertyName("reportServiceObjects")]
        public ServiceWrapper[] ReportServices { get; set; } = new ServiceWrapper[]{};

        [JsonProperty("reportUserObjects"), JsonPropertyName("reportUserObjects")]
        public UserWrapper[] ReportUsers { get; set; } = new UserWrapper[]{};

        [JsonProperty("deviceType"), JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; } = new DeviceType();

        [JsonProperty("import"), JsonPropertyName("import")]
        public Import Import { get; set; } = new Import();

        // [JsonProperty("pointInTime"), JsonPropertyName("pointInTime")]
        // public DateTime ReportTime { get; set; }
        public long? RelevantImportId { get; set; }
        public bool Ignore { get; set; }

        //[JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public List<long> ReportedRuleIds { get; set; } = new List<long>();
        public List<long> ReportedNetworkServiceIds { get; set; } = new List<long>();

        [JsonProperty("objects_aggregate"), JsonPropertyName("objects_aggregate")]
        public ObjectStatistics NetworkObjectStatistics { get; set; } = new ObjectStatistics();

        [JsonProperty("services_aggregate"), JsonPropertyName("services_aggregate")]
        public ObjectStatistics ServiceObjectStatistics { get; set; } = new ObjectStatistics();

        [JsonProperty("usrs_aggregate"), JsonPropertyName("usrs_aggregate")]
        public ObjectStatistics UserObjectStatistics { get; set; } = new ObjectStatistics();
        
        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        public ObjectStatistics RuleStatistics { get; set; } = new ObjectStatistics();

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
            DebugLevel = management.DebugLevel;
            TenantId = management.TenantId;
            Devices = management.Devices;
            Objects = management.Objects;
            Services = management.Services;
            Users = management.Users;
            ReportObjects = management.ReportObjects;
            ReportServices = management.ReportServices;
            ReportUsers = management.ReportUsers;
            DeviceType = management.DeviceType;
            Import = management.Import;
            Ignore = management.Ignore;
            ReportedRuleIds = management.ReportedRuleIds;
            ReportedNetworkServiceIds = management.ReportedNetworkServiceIds;
            if (management.Import != null && management.Import.ImportAggregate != null &&
                management.Import.ImportAggregate.ImportAggregateMax != null &&
                management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                RelevantImportId = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId;

            if (management.DeviceType != null)
                DeviceType = new DeviceType(management.DeviceType);
        }

        public string Host()
        {
            return Hostname + ":" + Port;
        }      
    }

    public static class ManagementUtility
    {
        public static bool Merge(this Management[] managements, Management[] managementsToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < managementsToMerge.Length; i++)
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
                    // important: if any management still returns rules, newObjects is set to true
                    if (managements[i].Devices.Merge(managementsToMerge[i].Devices) == true)
                        newObjects = true;
                }
            }

            return newObjects;
        }

        public static bool Merge(this Management management, Management managementToMerge)
        {
            bool newObjects = false;

            if (management.Objects != null && managementToMerge.Objects != null && managementToMerge.Objects.Length > 0)
            {
                management.Objects = management.Objects.Concat(managementToMerge.Objects).ToArray();
                newObjects = true;
            }

            if (management.Services != null && managementToMerge.Services != null && managementToMerge.Services.Length > 0)
            {
                management.Services = management.Services.Concat(managementToMerge.Services).ToArray();
                newObjects = true;
            }

            if (management.Users != null && managementToMerge.Users != null && managementToMerge.Users.Length > 0)
            {
                management.Users = management.Users.Concat(managementToMerge.Users).ToArray();
                newObjects = true;
            }

            if (management.Devices != null && managementToMerge.Devices != null && managementToMerge.Devices.Length > 0)
            {
                // important: if any management still returns rules, newObjects is set to true
                if (management.Devices.Merge(managementToMerge.Devices) == true)
                    newObjects = true;
            }
            return newObjects;
        }
    }
}
