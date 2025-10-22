using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Report
{
    public class DeviceReport
    {
        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("rulebase_links"), JsonPropertyName("rulebase_links")]
        public RulebaseLink[] RulebaseLinks { get; set; }

        [JsonProperty("changelog_rules"), JsonPropertyName("changelog_rules")]
        public RuleChange[]? RuleChanges { get; set; }

        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        public ObjectStatistics RuleStatistics { get; set; } = new ObjectStatistics();

        [JsonProperty("unusedRules_Count"), JsonPropertyName("unusedRules_Count")]
        public ObjectStatistics UnusedRulesStatistics { get; set; } = new();

        public List<Rule> GetRuleList()
        {
            // TODO: implement this method to return a list of rules associated with the device // MERGE
            return new List<Rule>();
        }

        public DeviceReport()
        { }

        public DeviceReport(DeviceReport device)
        {
            // TODO: implement this method to return a list of rules associated with the device
        }

        public void AddRule(Rule rule)
        {
            // TODO: implement this method to add a rule to the device
        }
        public int GetNumerOfRules()
        {
            // foreach (Rule rule in Rules)
            // {
            //     if (string.IsNullOrEmpty(rule.SectionHeader)) // Not a section header
            //     {
            //         rule.DisplayOrderNumber = ruleNumber++;
            //     }
            // }
            return 0;
            // TODO: implement this method to return the numer of rules for this device
        }

        public bool ContainsRules()
        {
            return true;
            // merge:            // return Rules != null && Rules.Count() > 0;
        }

        /// <summary>
        /// Conforms <see cref="DeviceReport"/> internal data to be valid for further usage.
        /// </summary>
        public void EnforceValidity()
        {
            if (UnusedRulesStatistics.ObjectAggregate.ObjectCount >= RuleStatistics.ObjectAggregate.ObjectCount)
            {
                UnusedRulesStatistics.ObjectAggregate.ObjectCount = 0;
            }
        }
    }


    public static class DeviceUtility
    {
        // adding rules fetched in slices
        public static (bool, Dictionary<string, int>) Merge(this DeviceReport[] devices, DeviceReport[] devicesToMerge)
        {
            bool newObjects = false;

            Dictionary<string, int> addedCounts = new()
            {
                { "Rules", 0 },
                { "RuleChanges", 0 },
            };

            // for (int i = 0; i < devices.Length && i < devicesToMerge.Length; i++)
            // {
            //     if (devices[i].Id != devicesToMerge[i].Id)
            //     {
            //         throw new NotSupportedException("Devices have to be in the same order in oder to merge.");
            //     }

            //     if (devices[i].Rules != null && devicesToMerge[i].Rules?.Length > 0)
            //     {
            //         devices[i].Rules = [.. devices[i].Rules!, .. devicesToMerge[i].Rules!];
            //         newObjects = true;
            //         addedCounts["Rules"] = Math.Max(addedCounts["Rules"], devicesToMerge[i].Rules!.Length);
            //     }
            //     if (devices[i].RuleChanges != null && devicesToMerge[i].RuleChanges?.Length > 0)
            //     {
            //         devices[i].RuleChanges = [.. devices[i].RuleChanges!, .. devicesToMerge[i].RuleChanges!];
            //         newObjects = true;
            //         addedCounts["RuleChanges"] = Math.Max(addedCounts["RuleChanges"], devicesToMerge[i].RuleChanges!.Length);
            //     }
            //     if (devices[i].RuleStatistics != null && devicesToMerge[i].RuleStatistics != null)
            //     {
            //         devices[i].RuleStatistics.ObjectAggregate.ObjectCount += devicesToMerge[i].RuleStatistics.ObjectAggregate.ObjectCount; // TODO: correct ??
            //     }
            // }
            return (newObjects, addedCounts);
        }
    }
}
