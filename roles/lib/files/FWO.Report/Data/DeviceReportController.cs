using System.Text.Json.Serialization;
using Newtonsoft.Json;
using FWO.Data.Report;
using FWO.Data;

namespace FWO.Report
{
    public class DeviceReportController: DeviceReport
    {
        public DeviceReportController()
        {
        }

        public DeviceReportController(DeviceReportController device)
        {
            Id = device.Id;
            Name = device.Name;
            RulebaseLinks = device.RulebaseLinks;
            RuleChanges = device.RuleChanges;
            RuleStatistics = device.RuleStatistics;
        }


        public static DeviceReportController FromDeviceReport(DeviceReport deviceReport)
        {
            var controller = new DeviceReportController
            {
                Id = deviceReport.Id,
                Name = deviceReport.Name,
                RulebaseLinks = deviceReport.RulebaseLinks,
                RuleChanges = deviceReport.RuleChanges,
                RuleStatistics = deviceReport.RuleStatistics
            };
            return controller;
        }
        
        public void AssignRuleNumbers(RulebaseLink? rbLinkIn = null, int ruleNumber = 1)
        {
            // rbLinkIn ??= RbLink;
            // if (rbLinkIn != null)
            // {
            //     if (rbLinkIn.IsInitialRulebase())   // TODO: use enum here
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
            return RulebaseLinks.FirstOrDefault(_ => _.IsInitialRulebase())?.NextRulebaseId;
        }

        public static explicit operator DeviceReportController(bool v)
        {
            throw new NotImplementedException();
        }
    }

}
