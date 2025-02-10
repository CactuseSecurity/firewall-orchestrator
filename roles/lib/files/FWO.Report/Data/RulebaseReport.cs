using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Api.Data;

namespace FWO.Report
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

        public RulebaseReport(RulebaseReport rulebase)
        {
            Id = rulebase.Id;
            Name = rulebase.Name;
            RuleChanges = rulebase.RuleChanges;
            RuleStatistics = rulebase.RuleStatistics;
        }

        public void AssignRuleNumbers(Rulebase? rb = null, int ruleNumber = 1)
        {
            if (rb != null)
            {
                foreach (Rule rule in rb.Rules) // only on rule per rule_metadata
                {
                    if (string.IsNullOrEmpty(rule.SectionHeader)) // Not a section header
                    {
                        rule.DisplayOrderNumber = ruleNumber++;
                    }
                    // if (rule.NextRulebase != null)
                    // {
                    //     AssignRuleNumbers(rule.NextRulebase, ruleNumber);
                    // }
                }
            }
        }

        public bool ContainsRules()
        {
            if (Rules.Length>0)
            {
                return true;
            }
            return false;
        }
    }


    public static class RulebaseUtility
    {
        // adding rules fetched in slices
        public static bool Merge(this RulebaseReport[] rulebases, RulebaseReport[] rulebasesToMerge)
        {
            bool newObjects = false;

            for (int i = 0; i < rulebases.Length && i < rulebasesToMerge.Length; i++)
            {
                if (rulebases[i].Id == rulebasesToMerge[i].Id)
                {
                    try
                    {
                        for (int rb = 0; rb < rulebases[i].Rules.Length && rb < rulebasesToMerge[i].Rules.Length; rb++)
                        {
                            if (rulebases[i].Rules != null && rulebasesToMerge[i].Rules != null && rulebasesToMerge[i].Rules.Length > 0)
                            {
                                rulebases[i].Rules = rulebases[i].Rules?.Concat(rulebasesToMerge[i].Rules!).ToArray();
                                newObjects = true;
                            }
                        }
                        if (rulebases[i].RuleChanges != null && rulebasesToMerge[i].RuleChanges != null && rulebasesToMerge[i].RuleChanges?.Length > 0)
                        {
                            rulebases[i].RuleChanges = rulebases[i].RuleChanges!.Concat(rulebasesToMerge[i].RuleChanges!).ToArray();
                            newObjects = true;
                        }
                        if (rulebases[i].RuleStatistics != null && rulebasesToMerge[i].RuleStatistics != null)
                            rulebases[i].RuleStatistics.ObjectAggregate.ObjectCount += rulebasesToMerge[i].RuleStatistics.ObjectAggregate.ObjectCount; // correct ??
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
