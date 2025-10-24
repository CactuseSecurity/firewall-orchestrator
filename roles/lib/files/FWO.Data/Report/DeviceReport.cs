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

        public List<Rule> GetRuleList()
        {
            // TODO: implement this method to return a list of rules associated with the device
            return [];
        }

        public void AddRule(Rule rule)
        {
            // TODO: implement this method to add a rule to the device
        }
        public int GetNumerOfRules()
        {
            return 0;
            // TODO: implement this method to return the numer of rules for this device
        }

    }
}
