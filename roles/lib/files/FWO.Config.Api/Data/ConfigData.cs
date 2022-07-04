﻿using FWO.Logging;
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
        public readonly bool Editable;

        [JsonProperty("DefaultLanguage"), JsonPropertyName("DefaultLanguage")]
        public virtual string DefaultLanguage { get; set; } = "English";

        [JsonProperty("sessionTimeout"), JsonPropertyName("sessionTimeout")]
        public int SessionTimeout { get; set; } = 720;

        [JsonProperty("sessionTimeoutNoticePeriod"), JsonPropertyName("sessionTimeoutNoticePeriod")]
        public int SessionTimeoutNoticePeriod { get; set; } = 60;

        //        [JsonProperty("maxMessages"), JsonPropertyName("maxMessages"), UserConfigData]
        //        public int MaxMessages { get; set; } = 3;

        [JsonProperty("elementsPerFetch"), JsonPropertyName("elementsPerFetch"), UserConfigData]
        public int ElementsPerFetch { get; set; } = 100;

        [JsonProperty("maxInitialFetchesRightSidebar"), JsonPropertyName("maxInitialFetchesRightSidebar")]
        public int MaxInitialFetchesRightSidebar { get; set; } = 10;

        [JsonProperty("autoFillRightSidebar"), JsonPropertyName("autoFillRightSidebar")]
        public bool AutoFillRightSidebar { get; set; } = false;

        [JsonProperty("dataRetentionTime"), JsonPropertyName("dataRetentionTime")]
        public int DataRetentionTime { get; set; } = 731;

        [JsonProperty("importSleepTime"), JsonPropertyName("importSleepTime")]
        public int ImportSleepTime { get; set; } = 40;

        [JsonProperty("importCheckCertificates"), JsonPropertyName("importCheckCertificates")]
        public bool ImportCheckCertificates { get; set; } = false;

        [JsonProperty("importSuppressCertificateWarnings"), JsonPropertyName("importSuppressCertificateWarnings")]
        public bool ImportSuppressCertificateWarnings { get; set; } = true;

        [JsonProperty("autoDiscoverSleepTime"), JsonPropertyName("autoDiscoverSleepTime")]
        public int AutoDiscoverSleepTime { get; set; } = 24;

        [JsonProperty("autoDiscoverStartAt"), JsonPropertyName("autoDiscoverStartAt")]
        public DateTime AutoDiscoverStartAt { get; set; } = new DateTime();

        [JsonProperty("fwApiElementsPerFetch"), JsonPropertyName("fwApiElementsPerFetch")]
        public int FwApiElementsPerFetch { get; set; } = 150;

        [JsonProperty("recertificationPeriod"), JsonPropertyName("recertificationPeriod")]
        public int RecertificationPeriod { get; set; } = 365;

        [JsonProperty("recertificationNoticePeriod"), JsonPropertyName("recertificationNoticePeriod")]
        public int RecertificationNoticePeriod { get; set; } = 30;

        [JsonProperty("recertificationDisplayPeriod"), JsonPropertyName("recertificationDisplayPeriod")]
        public int RecertificationDisplayPeriod { get; set; } = 30;

        [JsonProperty("ruleRemovalGracePeriod"), JsonPropertyName("ruleRemovalGracePeriod")]
        public int RuleRemovalGracePeriod { get; set; } = 60;

        [JsonProperty("commentRequired"), JsonPropertyName("commentRequired")]
        public bool CommentRequired { get; set; } = false;

        [JsonProperty("pwMinLength"), JsonPropertyName("pwMinLength")]
        public int PwMinLength { get; set; } = 10;

        [JsonProperty("pwUpperCaseRequired"), JsonPropertyName("pwUpperCaseRequired")]
        public bool PwUpperCaseRequired { get; set; } = false;

        [JsonProperty("pwLowerCaseRequired"), JsonPropertyName("pwLowerCaseRequired")]
        public bool PwLowerCaseRequired { get; set; } = false;

        [JsonProperty("pwNumberRequired"), JsonPropertyName("pwNumberRequired")]
        public bool PwNumberRequired { get; set; } = false;

        [JsonProperty("pwSpecialCharactersRequired"), JsonPropertyName("pwSpecialCharactersRequired")]
        public bool PwSpecialCharactersRequired { get; set; } = false;

        [JsonProperty("minCollapseAllDevices"), JsonPropertyName("minCollapseAllDevices"), UserConfigData]
        public int MinCollapseAllDevices { get; set; } = 15;

        [JsonProperty("messageViewTime"), JsonPropertyName("messageViewTime"), UserConfigData]
        public int MessageViewTime { get; set; } = 7;

        [JsonProperty("dailyCheckStartAt"), JsonPropertyName("dailyCheckStartAt")]
        public DateTime DailyCheckStartAt { get; set; } = new DateTime();

        [JsonProperty("maxImportDuration"), JsonPropertyName("maxImportDuration")]
        public int MaxImportDuration { get; set; } = 4;

        [JsonProperty("maxImportInterval"), JsonPropertyName("maxImportInterval")]
        public int MaxImportInterval { get; set; } = 12;

        public ConfigData(bool editable = false)
        {
            Editable = editable;
        }

        public object Clone()
        {
            // Watch out for references they need to be deep cloned (currently none)
            ConfigData configData = (ConfigData)MemberwiseClone();
            return configData;
        }

        public object CloneEditable()
        {
            object clone = Clone();
            typeof(ConfigData).GetProperty("Editable")?.SetValue(clone, true);
            return clone;
        }
    }
}
