using System.Text.Json.Serialization;
using Newtonsoft.Json;
using FWO.Data.Report;
using FWO.Data;

namespace FWO.Report
{
    public class DeviceReportController: DeviceReport
    {

        public DeviceReportController(DeviceReportController device)
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
