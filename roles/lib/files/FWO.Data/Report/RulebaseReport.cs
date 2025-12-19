using Newtonsoft.Json;
using System.Text.Json.Serialization; 

namespace FWO.Data.Report
{
    public class RulebaseReport
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("changelog_rules"), JsonPropertyName("changelog_rules")]
        public RuleChange[]? RuleChanges { get; set; }

        [JsonProperty("rules_aggregate"), JsonPropertyName("rules_aggregate")]
        public ObjectStatistics RuleStatistics { get; set; } = new ObjectStatistics();

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public Rule[] Rules { get; set; } = [];

        public RulebaseReport()
        { }
    }
}
