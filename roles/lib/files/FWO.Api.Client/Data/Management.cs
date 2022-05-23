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
        public string Secret { get; set; } = "";

        [JsonProperty("configPath"), JsonPropertyName("configPath")]
        public string ConfigPath { get; set; } = "";

        [JsonProperty("superManager"), JsonPropertyName("superManager")]
        public int? SuperManagerId { get; set; }

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
        public NetworkObject[] ReportObjects { get; set; } = new NetworkObject[]{};

        [JsonProperty("reportServiceObjects"), JsonPropertyName("reportServiceObjects")]
        public NetworkService[] ReportServices { get; set; } = new NetworkService[]{};

        [JsonProperty("reportUserObjects"), JsonPropertyName("reportUserObjects")]
        public NetworkUser[] ReportUsers { get; set; } = new NetworkUser[]{};

        [JsonProperty("deviceType"), JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; } = new DeviceType();

        [JsonProperty("import"), JsonPropertyName("import")]
        public Import Import { get; set; } = new Import();

        public long? RelevantImportId { get; set; }
        public bool Ignore { get; set; }
        public bool AwaitDevice { get; set; }
        public bool Delete { get; set; }
        public long ActionId { get; set; }

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
            Secret = management.Secret;
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
            AwaitDevice = management.AwaitDevice;
            Delete = management.Delete;
            ActionId = management.ActionId;
            ReportedRuleIds = management.ReportedRuleIds;
            SuperManagerId = management.SuperManagerId;
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
        
        public void AssignRuleNumbers()
        {
            foreach (Device device in Devices)
            {
                device.AssignRuleNumbers();
            }
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Hostname = Sanitizer.SanitizeMand(Hostname, ref shortened);
            ImportUser = Sanitizer.SanitizeOpt(ImportUser, ref shortened);
            ConfigPath = Sanitizer.SanitizeMand(ConfigPath, ref shortened);
            ImporterHostname = Sanitizer.SanitizeMand(ImporterHostname, ref shortened);
            Comment = Sanitizer.SanitizeOpt(Comment, ref shortened);
            PublicKey = Sanitizer.SanitizeKeyOpt(PublicKey, ref shortened);
            Secret = (DeviceType.IsLegacyDevType() ? Sanitizer.SanitizeKeyMand(Secret, ref shortened) : Sanitizer.SanitizePasswMand(Secret, ref shortened));
            return shortened;
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
