using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class Device
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("deviceType")]
        public DeviceType? DeviceType { get; set; }

        [JsonPropertyName("management")]
        public Management? Management { get; set; }

        [JsonPropertyName("local_rulebase_name")]
        public string? LocalRulebase { get; set; }

        [JsonPropertyName("global_rulebase_name")]
        public string? GlobalRulebase { get; set; }

        [JsonPropertyName("package_name")]
        public string? Package { get; set; }

        [JsonPropertyName("importDisabled")]
        public bool ImportDisabled { get; set; }

        [JsonPropertyName("hideInUi")]
        public bool HideInUi { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("rules")]
        public Rule[]? Rules { get; set; }

        [JsonPropertyName("changelog_rules")]
        public RuleChange[]? RuleChanges { get; set; }

        [JsonPropertyName("rules_aggregate")]
        public ObjectStatistics? RuleStatistics { get; set; }

        public bool Selected { get; set; } = false;

        public Device()
        { }

        public Device(Device device)
        {
            Id = device.Id;
            Name = device.Name;
            if (device.DeviceType != null)
            {
                DeviceType = new DeviceType(device.DeviceType);
            }
            if (device.Management != null)
            {
                Management = new Management(device.Management);
            }
            LocalRulebase = device.LocalRulebase;
            GlobalRulebase = device.GlobalRulebase;
            Package = device.Package;
            ImportDisabled = device.ImportDisabled;
            HideInUi = device.HideInUi;
            Comment = device.Comment;
        }
    }


    public static class DeviceUtility
    {
        // adding rules fetched in slices
        public static bool Merge(this Device[] devices, Device[] devicesToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].Id == devicesToMerge[i].Id)
                {
                    try
                    {
                        if (devices[i].Rules != null && devicesToMerge[i].Rules != null && devicesToMerge[i].Rules.Length > 0)
                        {
                            devices[i].Rules = devices[i].Rules.Concat(devicesToMerge[i].Rules).ToArray();
                            newObjects = true;
                        }
                        if (devices[i].RuleChanges != null && devicesToMerge[i].RuleChanges != null && devicesToMerge[i].RuleChanges.Length > 0)
                        {
                            devices[i].RuleChanges = devices[i].RuleChanges.Concat(devicesToMerge[i].RuleChanges).ToArray();
                            newObjects = true;
                        }
                        if (devices[i].RuleStatistics != null && devicesToMerge[i].RuleStatistics != null)
                            devices[i].RuleStatistics = devicesToMerge[i].RuleStatistics;
                    }
                    catch (NullReferenceException ex)
                    {
                        throw new ArgumentNullException("Rules is null");
                    }
                }
                else
                {
                    throw new NotSupportedException("Devices have to be in the same order in oder to merge.");
                }
            }
            return newObjects;
        }
    }
}
