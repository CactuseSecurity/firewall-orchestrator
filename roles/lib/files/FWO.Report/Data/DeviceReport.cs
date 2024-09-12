using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Basics;
using FWO.Api.Data;
using Org.BouncyCastle.Crypto.Engines;

namespace FWO.Report
{
    public class DeviceReport // : Device
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("rulebase_on_gateways"), JsonPropertyName("rulebase_on_gateways")]
        public RulebaseOnGateway[] OrderedRulebases { get; set; } = [];

        // [JsonProperty("rules"), JsonPropertyName("rules")]
        // public Rule[]? Rules { get; set; }

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
            OrderedRulebases = device.OrderedRulebases;
            RuleChanges = device.RuleChanges;
            RuleStatistics = device.RuleStatistics;
        }

        public void AssignRuleNumbers()
        {
            if (OrderedRulebases != null)
            {
                foreach (RulebaseOnGateway rulebase in OrderedRulebases)
                {
                    int ruleNumber = 1; // each rulebase will now start with number 1
                    foreach (Rule rule in rulebase.Rulebase.RuleMetadata[0].Rules) // only on rule per rule_metadata
                    {
                        if (string.IsNullOrEmpty(rule.SectionHeader)) // Not a section header
                        {
                            rule.DisplayOrderNumber = ruleNumber++;
                        }
                    }
                }
            }
        }

        public bool ContainsRules()
        {
            foreach (var rb in OrderedRulebases)
            {
                if (rb.Rulebase.RuleMetadata[0].Rules.Length>0)
                {
                    return true;
                }
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
                        if (devices[i].OrderedRulebases != null && devicesToMerge[i].OrderedRulebases != null && devicesToMerge[i].OrderedRulebases?.Length > 0)
                        {
                            for (int rb = 0; rb < devices[i].OrderedRulebases.Length && rb < devicesToMerge[i].OrderedRulebases.Length; rb++)
                            {
                                if (devices[i].OrderedRulebases[rb].Rulebase.RuleMetadata[0].Rules != null && devicesToMerge[i].OrderedRulebases[rb] != null && devicesToMerge[i].OrderedRulebases[rb]?.Rulebase.RuleMetadata[0].Rules.Length > 0)
                                {
                                    devices[i].OrderedRulebases[rb].Rulebase.RuleMetadata[0].Rules = devices[i].OrderedRulebases[rb]?.Rulebase.RuleMetadata[0].Rules?.Concat(devicesToMerge[i].OrderedRulebases[rb].Rulebase.RuleMetadata[0].Rules!).ToArray();
                                    newObjects = true;
                                }
                            }
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
