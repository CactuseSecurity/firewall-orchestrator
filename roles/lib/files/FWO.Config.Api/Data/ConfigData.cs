using FWO.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Config.Api.Data
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UserConfigDataAttribute : Attribute { }

    public class ConfigData : ICloneable
    {
        [JsonProperty("defaultLanguage"), JsonPropertyName("defaultLanguage")]
        public virtual string DefaultLanguage { get; protected set; } = "English";

        [JsonProperty("elementsPerFetch"), JsonPropertyName("elementsPerFetch"), UserConfigData]
        public int ElementsPerFetch { get; protected set; } = 100;

        [JsonProperty("maxInitialFetchesRightSidebar"), JsonPropertyName("maxInitialFetchesRightSidebar")]
        public int MaxInitialFetchesRightSidebar { get; protected set; } = 10;

        [JsonProperty("autoFillRightSidebar"), JsonPropertyName("autoFillRightSidebar")]
        public bool AutoFillRightSidebar { get; protected set; } = false;

        [JsonProperty("dataRetentionTime"), JsonPropertyName("dataRetentionTime")]
        public int DataRetentionTime { get; protected set; } = 731;

        [JsonProperty("importSleepTime"), JsonPropertyName("importSleepTime")]
        public int ImportSleepTime { get; protected set; } = 40;

        [JsonProperty("autoDiscoverSleepTime"), JsonPropertyName("autoDiscoverSleepTime")]
        public int AutoDiscoverSleepTime { get; protected set; } = 24;

        [JsonProperty("autoDiscoverStartAt"), JsonPropertyName("autoDiscoverStartAt")]
        public DateTime AutoDiscoverStartAt { get; protected set; } = DateTime.Now;

        [JsonProperty("fwApiElementsPerFetch"), JsonPropertyName("fwApiElementsPerFetch")]
        public int FwApiElementsPerFetch { get; protected set; } = 150;

        [JsonProperty("recertificationPeriod"), JsonPropertyName("recertificationPeriod")]
        public int RecertificationPeriod { get; protected set; } = 365;

        [JsonProperty("recertificationNoticePeriod"), JsonPropertyName("recertificationNoticePeriod")]
        public int RecertificationNoticePeriod { get; protected set; } = 30;

        [JsonProperty("recertificationDisplayPeriod"), JsonPropertyName("recertificationDisplayPeriod")]
        public int RecertificationDisplayPeriod { get; protected set; } = 30;

        [JsonProperty("ruleRemovalGracePeriod"), JsonPropertyName("ruleRemovalGracePeriod")]
        public int RuleRemovalGracePeriod { get; protected set; } = 60;

        [JsonProperty("commentRequired"), JsonPropertyName("commentRequired")]
        public bool CommentRequired { get; protected set; } = false;

        [JsonProperty("pwMinLength"), JsonPropertyName("pwMinLength")]
        public int PwMinLength { get; protected set; } = 10;

        [JsonProperty("pwUpperCaseRequired"), JsonPropertyName("pwUpperCaseRequired")]
        public bool PwUpperCaseRequired { get; protected set; } = false;

        [JsonProperty("pwLowerCaseRequired"), JsonPropertyName("pwLowerCaseRequired")]
        public bool PwLowerCaseRequired { get; protected set; } = false;

        [JsonProperty("pwNumberRequired"), JsonPropertyName("pwNumberRequired")]
        public bool PwNumberRequired { get; protected set; } = false;

        [JsonProperty("pwSpecialCharactersRequired"), JsonPropertyName("pwSpecialCharactersRequired")]
        public bool PwSpecialCharactersRequired { get; protected set; } = false;

        [JsonProperty("minCollapseAllDevices"), JsonPropertyName("minCollapseAllDevices"), UserConfigData]
        public int MinCollapseAllDevices { get; protected set; } = 15;

        [JsonProperty("messageViewTime"), JsonPropertyName("messageViewTime"), UserConfigData]
        public int MessageViewTime { get; protected set; } = 7;

        public ConfigData()
        {

        }

        public object Clone()
        {
            return MemberwiseClone();
            // Watch out for references they need to be deep cloned (currently no references present)
        }
    }
}
