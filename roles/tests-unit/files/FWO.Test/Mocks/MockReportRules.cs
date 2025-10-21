using FWO.Api.Client;
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
        private int _rulebaseId = 0;
        private int _ruleId = 0;

        public RuleTreeItem ControlTree = new();

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

        public List<ManagementReport> SetupSingleManagementReportBasic()
        {
            ControlTree = new RuleTreeItem
            {
                IsRoot = true,
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    new RuleTreeItem
                    {
                        Header = "Ordered layer with rules",
                        Children = new List<Basics.ITreeItem<Rule>>
                        {
                            new RuleTreeItem
                            {
                                Identifier = "Rule (ID/UID): 1/rule-1.1",
                                Data = new Rule { Id = 1, Uid = "rule-1.1", RulebaseId = 1 },
                                Position = new List<int>{1,1}
                            },
                            new RuleTreeItem
                            {
                                Identifier = "Rule (ID/UID): 2/rule-1.2",
                                Data = new Rule { Id = 2, Uid = "rule-1.2", RulebaseId = 1 },
                                Position = new List<int>{1,2}
                            },
                            new RuleTreeItem
                            {
                                Identifier = "Rule (ID/UID): 3/rule-1.3",
                                Data = new Rule { Id = 3, Uid = "rule-1.3", RulebaseId = 1 },
                                Position = new List<int>{1,3}
                            }
                        }
                    },
                    new RuleTreeItem
                    {
                        Header = "Ordered layer with sections",
                        Children = new List<Basics.ITreeItem<Rule>>
                        {
                            new RuleTreeItem
                            {
                                Header = "First section in ordered layer",
                                Children = new List<Basics.ITreeItem<Rule>>
                                {
                                    new RuleTreeItem
                                    {
                                        Identifier = "Rule (ID/UID): 4/rule-2.1",
                                        Data = new Rule { Id = 4, Uid = "rule-2.1", RulebaseId = 3 },
                                        Position = new List<int>{2,1}
                                    },
                                    new RuleTreeItem
                                    {
                                        Identifier = "Rule (ID/UID): 5/rule-2.2",
                                        Data = new Rule { Id = 5, Uid = "rule-2.2", RulebaseId = 3 },
                                        Position = new List<int>{2,2}
                                    },
                                    new RuleTreeItem
                                    {
                                        Identifier = "Rule (ID/UID): 6/rule-2.3",
                                        Data = new Rule { Id = 6, Uid = "rule-2.3", RulebaseId = 3 },
                                        Position = new List<int>{2,3}
                                    }
                                }
                            },
                            new RuleTreeItem
                            {
                                Header = "Section with inline layer",
                                Children = new List<Basics.ITreeItem<Rule>>
                                {
                                    new RuleTreeItem
                                    {
                                        Identifier = "Rule (ID/UID): 7/rule-2.4",
                                        Data = new Rule { Id = 7, Uid = "rule-2.4", RulebaseId = 4 },
                                        Position = new List<int>{2,4}
                                    },
                                    new RuleTreeItem
                                    {
                                        Identifier = "Rule (ID/UID): 8/rule-2.5",
                                        Data = new Rule { Id = 8, Uid = "rule-2.5", RulebaseId = 4 },
                                        Position = new List<int>{2,5},
                                        Children = new List<Basics.ITreeItem<Rule>>
                                        {
                                            new RuleTreeItem
                                            {
                                                Identifier = "Rule (ID/UID): 10/rule-2.5.1",
                                                Data = new Rule { Id = 10, Uid = "rule-2.5.1", RulebaseId = 5 },
                                                Position = new List<int>{2,5,1}
                                            },
                                            new RuleTreeItem
                                            {
                                                Identifier = "Rule (ID/UID): 11/rule-2.5.2",
                                                Data = new Rule { Id = 11, Uid = "rule-2.5.2", RulebaseId = 5 },
                                                Position = new List<int>{2,5,2}
                                            },
                                            new RuleTreeItem
                                            {
                                                Identifier = "Rule (ID/UID): 12/rule-2.5.3",
                                                Data = new Rule { Id = 12, Uid = "rule-2.5.3", RulebaseId = 5 },
                                                Position = new List<int>{2,5,3}
                                            }

                                        }
                                    },
                                    new RuleTreeItem
                                    {
                                        Identifier = "Rule (ID/UID): 9/rule-2.6",
                                        Data = new Rule { Id = 9, Uid = "rule-2.6", RulebaseId = 4 },
                                        Position = new List<int>{2,6}
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return new()
            {
                new ManagementReport
                {
                    Rulebases =
                    [
                        CreateRulebaseReport(rulebaseName: "Ordered layer with rules", numberOfRules: 3),
                        CreateRulebaseReport(rulebaseName: "Ordered layer with sections", numberOfRules: 0),
                        CreateRulebaseReport(rulebaseName: "First section in ordered layer", numberOfRules: 3),
                        CreateRulebaseReport(rulebaseName: "Section with inline layer", numberOfRules: 3),
                        CreateRulebaseReport(rulebaseName: "Inline layer", numberOfRules: 3)
                    ],
                    Devices =
                    [
                        CreateDeviceReport(rulebaseLinks:
                        [
                            new RulebaseLink
                            {
                                NextRulebaseId = 1,
                                LinkType = 2,
                                IsInitial = true
                            },
                            new RulebaseLink
                            {
                                NextRulebaseId = 2,
                                LinkType = 2
                            },
                            new RulebaseLink
                            {
                                FromRulebaseId = 2,
                                NextRulebaseId = 3,
                                LinkType = 4,
                                IsSection = true
                            },
                            new RulebaseLink
                            {
                                FromRulebaseId = 3,
                                NextRulebaseId = 4,
                                LinkType = 4,
                                IsSection = true
                            },
                            new RulebaseLink
                            {
                                FromRuleId = 8,
                                FromRulebaseId = 4,
                                NextRulebaseId = 5,
                                LinkType = 3
                            }
                        ])
                    ]
                }
            };
        }

        private RulebaseReport CreateRulebaseReport(string rulebaseName = "", int numberOfRules = 0)
        {
            _rulebaseId++;

            if (rulebaseName == "")
            {
                rulebaseName = $"Mock Rulebase {_rulebaseId}";
            }

            List<Rule> rules = new();

            if (numberOfRules > 0)
            {
                for (int i = 1; i <= numberOfRules; i++)
                {
                    _ruleId++;
                    rules.Add(new Rule
                    {
                        Id = _ruleId,
                        Uid = $"rule-{_rulebaseId}.{_ruleId}",
                        RulebaseId = _rulebaseId,
                        Name = $"Mock Rule {_ruleId}"
                    });
                }
            }

            return new RulebaseReport
            {
                Id = _rulebaseId,
                Name = rulebaseName,
                Rules = rules.ToArray()
            };
        }
        
        private DeviceReport CreateDeviceReport(int deviceId = 0, string deviceName = "", List<RulebaseLink>? rulebaseLinks = null)
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
    }
}