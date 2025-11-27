using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Report
{
    public class RulebaseReportController : RulebaseReport
    {

        public RulebaseReportController()
        { }

        public RulebaseReportController(RulebaseReport rulebase)
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
                    /* NOSONAR
                    // if (rule.NextRulebase != null)
                    // {
                    //     AssignRuleNumbers(rule.NextRulebase, ruleNumber);
                    // }*/
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
                if (rulebases[i].Id != rulebasesToMerge[i].Id)
                {
                    throw new NotSupportedException("Devices have to be in the same order in oder to merge.");
                }
                for (int rb = 0; rb < rulebases[i].Rules.Length && rb < rulebasesToMerge[i].Rules.Length; rb++)
                {
                    if (rulebasesToMerge[i].Rules.Length > 0)
                    {
                        rulebases[i].Rules = rulebases[i].Rules.Concat(rulebasesToMerge[i].Rules).ToArray();
                        newObjects = true;
                    }
                }

                if (rulebasesToMerge[i].RuleChanges?.Length > 0)
                {
                    rulebases[i].RuleChanges = rulebases[i].RuleChanges
                        ?.Concat(rulebasesToMerge[i].RuleChanges ?? []).ToArray();
                    newObjects = true;
                }

                rulebases[i].RuleStatistics.ObjectAggregate.ObjectCount +=
                    rulebasesToMerge[i].RuleStatistics.ObjectAggregate.ObjectCount; 
                
            }
            return newObjects;
        }
    }
}
