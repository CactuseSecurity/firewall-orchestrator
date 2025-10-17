using System.Text.Json.Serialization;
using FWO.Basics;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class Device
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("deviceType"), JsonPropertyName("deviceType")]
        public DeviceType DeviceType { get; set; } = new DeviceType();

        [JsonProperty("management"), JsonPropertyName("management")]
        public Management Management { get; set; } = new Management();

        [JsonProperty("local_rulebase_name"), JsonPropertyName("local_rulebase_name")]
        public string? LocalRulebase { get; set; }

        [JsonProperty("global_rulebase_name"), JsonPropertyName("global_rulebase_name")]
        public string? GlobalRulebase { get; set; }

        [JsonProperty("package_name"), JsonPropertyName("package_name")]
        public string? Package { get; set; }

        [JsonProperty("importDisabled"), JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonProperty("hideInUi"), JsonPropertyName("hideInUi")]
        public bool HideInUi { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        public bool Selected { get; set; } = false;
        public bool Relevant { get; set; }
        public bool AwaitMgmt { get; set; }
        public bool Delete { get; set; }
        public long ActionId { get; set; }

        public Device()
        { }

        public Device(Device device)
        {
            Id = device.Id;
            Name = device.Name;
            DeviceType = new DeviceType(device.DeviceType);
            Management = new Management(device.Management);
            LocalRulebase = device.LocalRulebase;
            GlobalRulebase = device.GlobalRulebase;
            Package = device.Package;
            ImportDisabled = device.ImportDisabled;
            HideInUi = device.HideInUi;
            Comment = device.Comment;
            Relevant = device.Relevant;
            AwaitMgmt = device.AwaitMgmt;
            Delete = device.Delete;
            ActionId = device.ActionId;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeOpt(ref shortened);
            LocalRulebase = LocalRulebase.SanitizeOpt(ref shortened);
            GlobalRulebase = GlobalRulebase.SanitizeOpt(ref shortened);
            Package = Package.SanitizeOpt(ref shortened);
            Comment = Comment.SanitizeCommentOpt(ref shortened);
            return shortened;
        }
    }
}
