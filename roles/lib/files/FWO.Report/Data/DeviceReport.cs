using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Api.Data;

namespace FWO.Report
{
    public class DeviceReport
    {
        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; }

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

        public DeviceReport()
        { }

        public DeviceReport(DeviceReport device)
        {
            Id = device.Id;
            Name = device.Name;
            RulebaseLinks = device.RulebaseLinks;
            RuleChanges = device.RuleChanges;
            RuleStatistics = device.RuleStatistics;
        }

        public void AssignRuleNumbers(RulebaseLink? rbLinkIn = null, int ruleNumber = 1)
        {
            // rbLinkIn ??= RbLink;
            // if (rbLinkIn != null)
            // {
            //     if (rbLinkIn.LinkType == 0)   // TODO: use enum here
            //     {
            //         foreach (Rule rule in rbLinkIn.NextRulebase.Rules) // only on rule per rule_metadata
            //         {
            //             if (string.IsNullOrEmpty(rule.SectionHeader)) // Not a section header
            //             {
            //                 rule.DisplayOrderNumber = ruleNumber++;
            //             }
            //             if (rule.NextRulebase != null)
            //             {
            //                 AssignRuleNumbers(rule.NextRulebase, ruleNumber);
            //             }
            //         }
            //     }
            // }
        }

        public bool ContainsRules()
        {
            return true;
            // if (RbLink?.NextRulebase.Rules.Length>0)
            // {
            //     return true;
            // }
            // return false;
        }
        public int? GetInitialRulebaseId(ManagementReport managementReport)
        {
            return RulebaseLinks.FirstOrDefault(_ => _.LinkType == 0)?.NextRulebaseId;
        }

    }

}
