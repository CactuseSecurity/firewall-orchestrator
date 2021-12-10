using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class Management
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "";

        [JsonPropertyName("user")]
        public string? ImportUser { get; set; }

        [JsonPropertyName("secret")]
        public string PrivateKey { get; set; } = "";

        [JsonPropertyName("configPath")]
        public string ConfigPath { get; set; } = "";

        [JsonPropertyName("importerHostname")]
        public string ImporterHostname { get; set; } = "";

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("sshPublicKey")]
        public string? PublicKey { get; set; }

        [JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonPropertyName("forceInitialImport")]
        public bool ForceInitialImport { get; set; }

        [JsonPropertyName("hideInUi")]
        public bool HideInUi { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("debugLevel")]
        public int? DebugLevel { get; set; }

        [JsonPropertyName("tenant_id")]
        public int TenantId { get; set; }

        [JsonPropertyName("devices")]
        public Device[] Devices { get; set; } = new Device[]{};

        [JsonPropertyName("networkObjects")]
        public NetworkObject[] Objects { get; set; } = new NetworkObject[]{};

        [JsonPropertyName("serviceObjects")]
        public NetworkService[] Services { get; set; } = new NetworkService[]{};

        [JsonPropertyName("userObjects")]
        public NetworkUser[] Users { get; set; } = new NetworkUser[]{};

        [JsonPropertyName("reportNetworkObjects")]
        public NetworkObjectWrapper[] ReportObjects { get; set; } = new NetworkObjectWrapper[]{};

        [JsonPropertyName("reportServiceObjects")]
        public ServiceWrapper[] ReportServices { get; set; } = new ServiceWrapper[]{};

        [JsonPropertyName("reportUserObjects")]
        public UserWrapper[] ReportUsers { get; set; } = new UserWrapper[]{};

        [JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; } = new DeviceType();

        [JsonPropertyName("import")]
        public Import Import { get; set; } = new Import();

        // [JsonPropertyName("pointInTime")]
        // public DateTime ReportTime { get; set; }
        public long? RelevantImportId { get; set; }
        public bool Ignore { get; set; }

        //[JsonPropertyName("rule_id")]
        public List<long> ReportedRuleIds { get; set; } = new List<long>();
        public List<long> ReportedNetworkServiceIds { get; set; } = new List<long>();

        [JsonPropertyName("objects_aggregate")]
        public ObjectStatistics NetworkObjectStatistics { get; set; } = new ObjectStatistics();

        [JsonPropertyName("services_aggregate")]
        public ObjectStatistics ServiceObjectStatistics { get; set; } = new ObjectStatistics();

        [JsonPropertyName("usrs_aggregate")]
        public ObjectStatistics UserObjectStatistics { get; set; } = new ObjectStatistics();
        
        [JsonPropertyName("rules_aggregate")]
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
                newObjects |= managements[i].Merge(managementsToMerge[i]);

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

        public static bool MergeReportObjects(this Management management, Management managementToMerge)
        {
            bool newObjects = false;

            if (management.ReportObjects != null && managementToMerge.ReportObjects != null && managementToMerge.ReportObjects.Length > 0)
            {
                management.ReportObjects = management.ReportObjects.Concat(managementToMerge.ReportObjects).ToArray();
                newObjects = true;
            }

            if (management.ReportServices != null && managementToMerge.ReportServices != null && managementToMerge.ReportServices.Length > 0)
            {
                management.ReportServices = management.ReportServices.Concat(managementToMerge.ReportServices).ToArray();
                newObjects = true;
            }

            if (management.ReportUsers != null && managementToMerge.ReportUsers != null && managementToMerge.ReportUsers.Length > 0)
            {
                management.ReportUsers = management.ReportUsers.Concat(managementToMerge.ReportUsers).ToArray();
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
