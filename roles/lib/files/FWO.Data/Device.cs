using System.Collections.Generic;
using System.Text.Json.Serialization;
using FWO.Basics;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class Device : IEqualityComparer<Device>
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string? Uid { get; set; }

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

        [JsonProperty("global_rulebase_uid"), JsonPropertyName("global_rulebase_uid")]
        public string? GlobalRulebaseUid { get; set; }

        [JsonProperty("package_name"), JsonPropertyName("package_name")]
        public string? Package { get; set; }

        [JsonProperty("importDisabled"), JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonProperty("hideInUi"), JsonPropertyName("hideInUi")]
        public bool HideInUi { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("rulebase_links"), JsonPropertyName("rulebase_links")]
        public RulebaseLink[] RulebaseLinks { get; set; } = [];

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
            Uid = device.Uid;
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

        /// <summary>
        /// Compares this device against another device based on name and UID.
        /// </summary>
        /// <param name="device">The device to compare against.</param>
        /// <returns>True when name and UID are considered equal.</returns>
        public bool Equals(Device? device)
        {
            if (device == null)
            {
                return false;
            }
            return Name.GenerousCompare(device.Name) && Uid.GenerousCompare(device.Uid);
        }

        /// <summary>
        /// Determines whether two devices are equal based on name and UID.
        /// </summary>
        /// <param name="first">The first device instance.</param>
        /// <param name="second">The second device instance.</param>
        /// <returns>True when both devices are considered equal.</returns>
        public bool Equals(Device? first, Device? second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }
            if (first is null || second is null)
            {
                return false;
            }
            return first.Name.GenerousCompare(second.Name) && first.Uid.GenerousCompare(second.Uid);
        }

        /// <summary>
        /// Returns a hash code for a device based on name and UID.
        /// </summary>
        /// <param name="device">The device to hash.</param>
        /// <returns>A hash code that aligns with the equality comparison.</returns>
        public int GetHashCode(Device device)
        {
            if (device == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(NormalizeComparable(device.Name));
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(NormalizeComparable(device.Uid));
                return hash;
            }
        }

        private static string NormalizeComparable(string? value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        /// <summary>
        /// Sanitizes string properties and returns whether any values were shortened.
        /// </summary>
        /// <returns>True when any property required shortening.</returns>
        public bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeOpt(ref shortened);
            Uid = Uid.SanitizeOpt(ref shortened);
            LocalRulebase = LocalRulebase.SanitizeOpt(ref shortened);
            GlobalRulebase = GlobalRulebase.SanitizeOpt(ref shortened);
            Package = Package.SanitizeOpt(ref shortened);
            Comment = Comment.SanitizeCommentOpt(ref shortened);
            return shortened;
        }
    }
}
