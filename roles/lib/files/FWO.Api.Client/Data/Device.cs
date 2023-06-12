using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
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

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public Rule[]? Rules { get; set; }

        [JsonProperty("changelog_rules"), JsonPropertyName("changelog_rules")]
        public RuleChange[]? RuleChanges { get; set; }

        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        public ObjectStatistics RuleStatistics { get; set; } = new ObjectStatistics();

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

        public void AssignRuleNumbers()
        {
            if (Rules != null)
            {
                int ruleNumber = 1;

                foreach (Rule rule in Rules)
                {
                    if (string.IsNullOrEmpty(rule.SectionHeader)) // Not a section header
                    {
                        rule.DisplayOrderNumber = ruleNumber++;
                    }
                }
            }
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            LocalRulebase = Sanitizer.SanitizeOpt(LocalRulebase, ref shortened);
            GlobalRulebase = Sanitizer.SanitizeOpt(GlobalRulebase, ref shortened);
            Package = Sanitizer.SanitizeOpt(Package, ref shortened);
            Comment = Sanitizer.SanitizeCommentOpt(Comment, ref shortened);
            return shortened;
        }
        
        public bool ContainsRules()
        {
            return (Rules != null && Rules.Count()>0);
        }
    }


    public static class DeviceUtility
    {
        // adding rules fetched in slices
        public static bool Merge(this Device[] devices, Device[] devicesToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < devices.Length && i < devicesToMerge.Length; i++)
            {
                if (devices[i].Id == devicesToMerge[i].Id)
                {
                    try
                    {
                        if (devices[i].Rules != null && devicesToMerge[i].Rules != null && devicesToMerge[i].Rules?.Length > 0)
                        {
                            devices[i].Rules = devices[i].Rules?.Concat(devicesToMerge[i].Rules!).ToArray();
                            newObjects = true;
                        }
                        if (devices[i].RuleChanges != null && devicesToMerge[i].RuleChanges != null && devicesToMerge[i].RuleChanges?.Length > 0)
                        {
                            devices[i].RuleChanges = devices[i].RuleChanges!.Concat(devicesToMerge[i].RuleChanges!).ToArray();
                            newObjects = true;
                        }
                        if (devices[i].RuleStatistics != null && devicesToMerge[i].RuleStatistics != null)
                            devices[i].RuleStatistics.ObjectAggregate.ObjectCount += devicesToMerge[i].RuleStatistics.ObjectAggregate.ObjectCount; // correct ??
                    }
                    catch (NullReferenceException)
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
