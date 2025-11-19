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
}
