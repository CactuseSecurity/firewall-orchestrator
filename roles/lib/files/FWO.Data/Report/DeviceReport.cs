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

        /// <summary>
        /// Gets or sets at most one rule hit stored for this gateway. Rule reports read from it whether the rule
        /// hit import has delivered hits for the gateway at all. Empty when the query did not select it. Read
        /// from the API only: it is not part of the report exports.
        /// </summary>
        [JsonProperty("importedRuleHits"), JsonPropertyName("importedRuleHits")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
        public List<RuleHit> ImportedRuleHits { get; set; } = [];

        /// <summary>
        /// Keeps <see cref="ImportedRuleHits"/> out of what Newtonsoft.Json writes.
        /// </summary>
        public static bool ShouldSerializeImportedRuleHits()
        {
            return false;
        }

        /// <summary>
        /// Tells whether the rule hit import has stored at least one hit for this gateway. Without any hit a
        /// rule that has no hit on the gateway cannot be told apart from a gateway whose hits were never read.
        /// </summary>
        public bool HasImportedRuleHits()
        {
            return ImportedRuleHits.Count > 0;
        }

        /// <summary>
        /// Management rulebases view only: rulebases of this rule tree that are listed completely in an earlier
        /// rule tree of the management, mapped to the id of that tree (not part of the API data or exports).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<int, int> ReferencedRulebaseTreeIds { get; set; } = [];


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

        public List<Rule> GetRuleList()
        {
            return Rules;
        }

        public DeviceReport()
        {
            RulebaseLinks = [];
        }

        public DeviceReport(DeviceReport deviceReport)
        {
            Uid = deviceReport.Uid;
            Id = deviceReport.Id;
            Name = deviceReport.Name;
            RulebaseLinks = deviceReport.RulebaseLinks;
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
