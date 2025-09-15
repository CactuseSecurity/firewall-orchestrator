using FWO.Basics;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public struct ExtMgtData
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public string? ExtId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? ExtName { get; set; }
    }

    public class Management
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("hostname"), JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "";

        [JsonProperty("import_credential"), JsonPropertyName("import_credential")]
        public ImportCredential ImportCredential { get; set; } = new();

        [JsonProperty("configPath"), JsonPropertyName("configPath")]
        public string? ConfigPath { get; set; } = "";

        [JsonProperty("domainUid"), JsonPropertyName("domainUid")]
        public string? DomainUid { get; set; } = "";

        [JsonProperty("cloudSubscriptionId"), JsonPropertyName("cloudSubscriptionId")]
        public string? CloudSubscriptionId { get; set; } = "";

        [JsonProperty("cloudTenantId"), JsonPropertyName("cloudTenantId")]
        public string? CloudTenantId { get; set; } = "";

        [JsonProperty("superManager"), JsonPropertyName("superManager")]
        public int? SuperManagerId { get; set; }

        [JsonProperty("importerHostname"), JsonPropertyName("importerHostname")]
        public string ImporterHostname { get; set; } = "";

        [JsonProperty("port"), JsonPropertyName("port")]
        public int Port { get; set; }

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

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public Device[] Devices { get; set; } = [];

        [JsonProperty("deviceType"), JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; } = new();

        [JsonProperty("import"), JsonPropertyName("import")]
        public Import Import { get; set; } = new();

        [JsonProperty("extMgtData"), JsonPropertyName("extMgtData")]
        public string? ExtMgtData { get; set; }

        public long? RelevantImportId { get; set; }
        public bool Ignore { get; set; }
        public bool AwaitDevice { get; set; }
        public bool Delete { get; set; }
        public long ActionId { get; set; }

        public Management()
        {}

        public Management(Management management)
        {
            Id = management.Id;
            Name = management.Name;
            Hostname = management.Hostname;
            if (management.ImportCredential != null)
                ImportCredential = new ImportCredential(management.ImportCredential);
            else
                ImportCredential = new ImportCredential();
            ConfigPath = management.ConfigPath;
            DomainUid = management.DomainUid;
            CloudSubscriptionId = management.CloudSubscriptionId;
            CloudTenantId = management.CloudTenantId;
            SuperManagerId = management.SuperManagerId;
            ImporterHostname = management.ImporterHostname;
            Port = management.Port;
            ImportDisabled = management.ImportDisabled;
            ForceInitialImport = management.ForceInitialImport;
            HideInUi = management.HideInUi;
            Comment = management.Comment;
            DebugLevel = management.DebugLevel;
            Devices = management.Devices;
            if (management.DeviceType != null)
                DeviceType = new DeviceType(management.DeviceType);
            Import = management.Import;
            if (management.Import != null && management.Import.ImportAggregate != null &&
                management.Import.ImportAggregate.ImportAggregateMax != null &&
                management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
            {
                RelevantImportId = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
            }
            Ignore = management.Ignore;
            AwaitDevice = management.AwaitDevice;
            Delete = management.Delete;
            ActionId = management.ActionId;
            ExtMgtData = management.ExtMgtData;
        }

        public string Host()
        {
            return Hostname + ":" + Port;
        }
        
        public virtual bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeMand();
            Hostname = Hostname.SanitizeMand();
            ConfigPath = ConfigPath.SanitizeOpt();
            DomainUid = DomainUid.SanitizeOpt();
            ImporterHostname = ImporterHostname.SanitizeMand();
            Comment = Comment.SanitizeCommentOpt();
            CloudSubscriptionId = CloudSubscriptionId.SanitizeOpt();
            CloudTenantId = CloudTenantId.SanitizeOpt();
            return shortened;
        }
    }
}
