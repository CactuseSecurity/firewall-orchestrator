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

        /// <summary>
        /// Active links pointing to this rulebase (only fetched for management rulebases view, cleared after scoping).
        /// </summary>
        [JsonProperty("rulebase_links", NullValueHandling = NullValueHandling.Ignore), JsonPropertyName("rulebase_links")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RulebaseLink[]? IncomingLinks { get; set; }

        public RulebaseReport()
        { }
    }
}
