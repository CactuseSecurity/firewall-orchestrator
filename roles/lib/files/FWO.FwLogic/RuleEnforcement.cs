using System.Text.Json;
using FWO.Basics;
using FWO.Data;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Data.Report;

namespace FWO.FwLogic
{
    public class EnforcingDevice : DeviceReport
    {
        // public EnforcingDevice(string name, string uid, string type, string version)
        //     : base(name, uid, type, type, version)
        // {
        // }

        public override string ToString()
        {
            return $"{Name} ({Uid})";
        }

        public List<Rule> Rules { get; set; } = [];

        public List<Rule> GetRuleListForDevice()
        {
            var activeRulebaseLinks = RulebaseLinks
                .Where(link => link.GatewayId == Id && link.Removed != null)
                .ToList();

            return [.. Rules.Where(rule => activeRulebaseLinks.Any(link => link.NextRulebaseId == rule.RulebaseId))];
        }

    }

    public class RuleToDevices : Rule
    {

        public List<DeviceReport> EnforcingDevices { get; set; } = [];

        // get a list of all devices that have a link to a rulebase this rule belongs to
        public List<DeviceReport> GetAllEnforcingDevices(List<RulebaseLink> currentRulebaseLinks)
        {
            List<DeviceReport> devReportList = [];

            // filter for all rulebaselinks that point to the rulebase of this rule
            var rulebaseLinksCurrentRulebase = currentRulebaseLinks
                .Where(link => link.NextRulebaseId == RulebaseId)
                .ToList();

            foreach (var rulebaseLink in rulebaseLinksCurrentRulebase)
            {
                // get all devices that have a link to this rulebase
                DeviceReport dev = new() { Id = rulebaseLink.GatewayId, Name = "" };
                if (devReportList.FirstOrDefault(device => device.Id == dev.Id) != null)
                {
                    devReportList.Add(dev);
                }
            }
            return devReportList;
        }

    }

}

