using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Api.Data;

namespace FWO.Report
{
    public class DeviceReport
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("rulebase_link"), JsonPropertyName("rulebase_link")]
        public RulebaseLink? RbLink { get; set; }

        [JsonProperty("changelog_rules"), JsonPropertyName("changelog_rules")]
        public RuleChange[]? RuleChanges { get; set; }

        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        public ObjectStatistics RuleStatistics { get; set; } = new ObjectStatistics();

        public DeviceReport()
        { }

        public DeviceReport(DeviceReport device)
        {
            Id = device.Id;
            Name = device.Name;
            RbLink = device.RbLink;
            RuleChanges = device.RuleChanges;
            RuleStatistics = device.RuleStatistics;
        }

        public void AssignRuleNumbers(RulebaseLink? rbLinkIn = null, int ruleNumber = 1)
        {
            rbLinkIn ??= RbLink;
            if (rbLinkIn != null)
            {
                if (rbLinkIn.LinkType == 0)   // TODO: use enum here
                {
                    foreach (Rule rule in rbLinkIn.NextRulebase.Rules) // only on rule per rule_metadata
                    {
                        if (string.IsNullOrEmpty(rule.SectionHeader)) // Not a section header
                        {
                            rule.DisplayOrderNumber = ruleNumber++;
                        }
                        if (rule.NextRulebase != null)
                        {
                            AssignRuleNumbers(rule.NextRulebase, ruleNumber);
                        }
                    }
                }
            }
        }

        public bool ContainsRules()
        {
            if (RbLink?.NextRulebase.Rules.Length>0)
            {
                return true;
            }
            return false;
        }
    }


    public static class DeviceUtility
    {
        // adding rules fetched in slices
        public static bool Merge(this DeviceReport[] devices, DeviceReport[] devicesToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < devices.Length && i < devicesToMerge.Length; i++)
            {
                if (devices[i].Id == devicesToMerge[i].Id)
                {
                    try
                    {
                        if (devices[i].RbLink != null && devicesToMerge[i].RbLink != null)
                        {
                            // for (int rb = 0; rb < devices[i].RbLink.Length && rb < devicesToMerge[i].RbLink.Length; rb++)
                            // {
                                // if (devices[i].RulebaseLinks[rb].NextRuleBase.Rules != null && devicesToMerge[i].RbLink[rb] != null && devicesToMerge[i].RbLink[rb]?.NextRuleBase.Rules.Length > 0)
                                // {
                                //     devices[i].RbLink[rb].NextRuleBase.Rules = devices[i].RbLink[rb]?.NextRuleBase.Rules?.Concat(devicesToMerge[i].RbLink[rb].NextRuleBase.Rules!).ToArray();
                                //     newObjects = true;
                                // }
                            // }
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
