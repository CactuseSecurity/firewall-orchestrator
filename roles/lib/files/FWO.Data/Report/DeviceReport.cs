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


        private List<Rule> Rules = [];


        public void SetRulesForDev(List<Rule> rulesToReport)
        {
            Rules = rulesToReport;
        }

        public bool IsLinked(Rule rule)
        {
            List<RulebaseLink> activeRulebaseLinks = [.. RulebaseLinks.Where(link => link.GatewayId == Id && link.Removed == null)];
            return activeRulebaseLinks.Any(link => link.NextRulebaseId == rule.RulebaseId);
        }

        public List<Rule> GetRuleListForDevice()
        {
            List<RulebaseLink> activeRulebaseLinks = [.. RulebaseLinks.Where(link => link.GatewayId == Id && link.Removed == null)];
            return [.. Rules.Where(rule => activeRulebaseLinks.Any(link => link.NextRulebaseId == rule.RulebaseId))];
        }

        public List<Rule> GetRuleList()
        {
            return Rules;
        }

        public DeviceReport()
        {
            RulebaseLinks = [];
        }

        public int? GetInitialRulebaseId(ManagementReport managementReport)
        {
            return RulebaseLinks.FirstOrDefault(_ => _.IsInitial)?.NextRulebaseId;
        }

        public void AddRule(Rule rule)
        {
            Rules.Add(rule);
        }

        public int GetNumberOfRules()
        {
            return Rules.Count;
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
}
