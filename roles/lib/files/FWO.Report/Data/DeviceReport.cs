using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.GlobalConstants;
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
                int ruleNumber = 1;

                foreach (RulebaseOnGateway rulebase in OrderedRulebases)
                {
                    // if (string.IsNullOrEmpty(rulebase.SectionHeader)) // Not a section header
                    // {
                    //     rulebase.DisplayOrderNumber = ruleNumber++;
                    // }
                }
            }
        }

        // public bool GatewayHasRules()
        // {
        //     // dev.Rules != null && dev.Rules.Length > 0
        //     return true;
        // }
        // public bool ManagementHasRules()
        // {
        //     // Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)
        //     return true;
        // }        

        public bool ContainsRules()
        {
            return OrderedRulebases != null && OrderedRulebases.Length > 0;
            // TODO: not only return if there are rulebases, but if there are any rules in them
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
                                if (devices[i].OrderedRulebases[rb].Rulebase.Rules != null && devicesToMerge[i].OrderedRulebases[rb] != null && devicesToMerge[i].OrderedRulebases[rb]?.Rulebase.Rules.Length > 0)
                                {
                                    devices[i].OrderedRulebases[rb].Rulebase.Rules = devices[i].OrderedRulebases[rb]?.Rulebase.Rules?.Concat(devicesToMerge[i].OrderedRulebases[rb].Rulebase.Rules!).ToArray();
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
