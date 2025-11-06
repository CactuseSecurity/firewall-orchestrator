using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Services.RuleTreeBuilder;

namespace FWO.Test.Mocks
{
    public class MockReportRules : ReportRules
    {
        public static int RulebaseId { get; set; } = 0;
        public static int RuleId { get; set; } = 0;

        public MockReportRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, Func<List<ManagementReport>>? setupFunc = null) : base(query, userConfig, reportType)
        {
            if (setupFunc == null)
            {
                setupFunc = SetupSingleManagementReportEmpty;
            }

            List<ManagementReport> managementReports = setupFunc();

            foreach (var managementReport in managementReports)
            {
                ReportData.ManagementData.Add(managementReport);
            }

        }

        public List<ManagementReport> SetupSingleManagementReportEmpty()
        {
            List<ManagementReport> reports = new()
            {
                new ManagementReport
                {
                    Rulebases = [],
                    Devices =
                    [
                        CreateDeviceReport()
                    ]
                }
            };

            return reports;
        }

        public static RulebaseReport CreateRulebaseReport(string rulebaseName = "", int numberOfRules = 0)
        {
            RulebaseId++;

            if (rulebaseName == "")
            {
                rulebaseName = $"Mock Rulebase {RulebaseId}";
            }

            List<Rule> rules = new();

            if (numberOfRules > 0)
            {
                for (int i = 1; i <= numberOfRules; i++)
                {
                    RuleId++;
                    rules.Add(new Rule
                    {
                        Id = RuleId,
                        Uid = $"rule-{RulebaseId}.{RuleId}",
                        RulebaseId = RulebaseId,
                        Name = $"Mock Rule {RuleId}"
                    });
                }
            }

            return new RulebaseReport
            {
                Id = RulebaseId,
                Name = rulebaseName,
                Rules = rules.ToArray()
            };
        }

        public static DeviceReport CreateDeviceReport(int deviceId = 0, string deviceName = "", List<RulebaseLink>? rulebaseLinks = null)
        {
            if (deviceId == 0)
            {
                deviceId = 1;
            }

            if (deviceName == "")
            {
                deviceName = $"Mock Device {deviceId}";
            }

            return new DeviceReport
            {
                Id = deviceId,
                Uid = $"device-{deviceId}",
                Name = deviceName,
                RulebaseLinks = rulebaseLinks?.ToArray() ?? []
            };
        }

        public static RuleTreeItem CreateRuleTreeItem(int ruleId, int rulebaseId, List<int> position, List<ITreeItem<Rule>>? children = null)
        {
            RuleTreeItem item = new RuleTreeItem
            {
                Identifier = $"Rule (ID/UID): {ruleId}/rule-{rulebaseId}.{ruleId}",
                Data = new Rule { Id = ruleId, Uid = $"rule-{rulebaseId}.{ruleId}", RulebaseId = rulebaseId },
                Position = position
            };

            if (children != null)
            {
                item.Children = children;
            }

            return item;
        }

    }
}
