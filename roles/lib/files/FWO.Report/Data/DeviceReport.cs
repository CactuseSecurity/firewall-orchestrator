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

        [JsonProperty("rulebase_links"), JsonPropertyName("rulebase_links")]
        public RulebaseLink[] Rulebases { get; set; } = [];

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public Rule[]? Rules { get; set; }

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
            Rulebases = device.Rulebases;
            RuleChanges = device.RuleChanges;
            RuleStatistics = device.RuleStatistics;
        }

        public void AssignRuleNumbers()
        {
            if (Rulebases != null)
            {
                foreach (RulebaseLink rulebase in Rulebases)
                {
                    int ruleNumber = 1; // each rulebase will now start with number 1
                    foreach (RuleMetadata rule in rulebase.Rulebase.RuleMetadata) // only on rule per rule_metadata
                    {
                        if (string.IsNullOrEmpty(rule.Rules[0].SectionHeader)) // Not a section header
                        {
                            rule.Rules[0].DisplayOrderNumber = ruleNumber++;
                        }
                    }
                }
            }
        }

        public bool ContainsRules()
        {
            foreach (var rb in Rulebases)
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
                        if (devices[i].Rulebases != null && devicesToMerge[i].Rulebases != null && devicesToMerge[i].Rulebases?.Length > 0)
                        {
                            for (int rb = 0; rb < devices[i].Rulebases.Length && rb < devicesToMerge[i].Rulebases.Length; rb++)
                            {
                                if (devices[i].Rulebases[rb].Rulebase.RuleMetadata[0].Rules != null && devicesToMerge[i].Rulebases[rb] != null && devicesToMerge[i].Rulebases[rb]?.Rulebase.RuleMetadata[0].Rules.Length > 0)
                                {
                                    devices[i].Rulebases[rb].Rulebase.RuleMetadata[0].Rules = devices[i].Rulebases[rb]?.Rulebase.RuleMetadata[0].Rules?.Concat(devicesToMerge[i].Rulebases[rb].Rulebase.RuleMetadata[0].Rules!).ToArray();
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
