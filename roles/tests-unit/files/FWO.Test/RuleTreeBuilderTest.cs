using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using FWO.Services.RuleTreeBuilder;
using FWO.Data;
using FWO.Data.Report;
using FWO.Test.Mocks;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleTreeBuilderTest
    {
        private RulebaseReport[]? Rulebases
        {
            get => _reportRules.ReportData.ManagementData.FirstOrDefault()?.Rulebases;
            set
            {
                if(_reportRules.ReportData.ManagementData.FirstOrDefault() != null && value != null)
                {
                    _reportRules.ReportData.ManagementData.First().Rulebases = value;
                }
            } 
        }

        private RulebaseLink[]? RulebaseLinks
        {
            get => _reportRules.ReportData.ManagementData.FirstOrDefault()?.Devices.FirstOrDefault()?.RulebaseLinks;
            set
            {
                if (_reportRules.ReportData.ManagementData.FirstOrDefault()?.Devices.FirstOrDefault() != null && value != null)
                {
                    _reportRules.ReportData.ManagementData.First().Devices.First().RulebaseLinks = value;
                }
            }
        }
        
        private RuleTreeBuilder _ruleTreeBuilder = default!;
        private MockReportRules _reportRules = default!;
        private RuleTreeItem? _controlTree;

        [SetUp]
        public void SetUpTestMethod()
        {
            _ruleTreeBuilder = new RuleTreeBuilder();
            _reportRules = new MockReportRules(new(""), new(), Basics.ReportType.Rules);
            MockReportRules.RulebaseId = 0;
            MockReportRules.RuleId = 0;
        }

        [Test]
        public void BuildRulebaseLinkQueue_WithEmptyArraysAsArgs_ReturnsNull()
        {
            // Arrange

            Queue<(RulebaseLink, RulebaseReport)>? queue;

            // Act

            queue = _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Assert

            Assert.That(queue is null);
        }

        [Test]
        public void BuildRulebaseLinkQueue_BasicSetup_Succeeds()
        {
            // Arrange

            Queue<(RulebaseLink, RulebaseReport)>? queue;
            SetUpMockReportRulesBasic(true);

            // Act

            queue = _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Assert
            //Assert.That(_ruleTreeBuilder.RuleTree.ToJson(), Is.EqualTo(_reportRules.ControlTree.ToJson()));
        }

        [Test]
        public void BuildRuleTree_BasicSetup_Succeeds()
        {
            // Arrange

            Queue<(RulebaseLink, RulebaseReport)>? queue;
            SetUpMockReportRulesBasic(true);
            _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Act

            _ruleTreeBuilder.BuildRuleTree();

            // Assert
            Assert.That(_ruleTreeBuilder.RuleTree.ToJson(), Is.EqualTo(_controlTree!.ToJson()));
        }

        private void SetUpMockReportRulesBasic(bool buildControlTree)
        {
            if (buildControlTree)
            {
                _controlTree = new RuleTreeItem
                {
                    IsRoot = true,
                    Children = new List<Basics.ITreeItem<Rule>>
                    {
                        new RuleTreeItem
                        {
                            Header = "Ordered layer with rules",
                            Children = new List<Basics.ITreeItem<Rule>>
                            {
                                MockReportRules.CreateRuleTreeItem(1, 1, new List<int>{1,1}),
                                MockReportRules.CreateRuleTreeItem(2, 1, new List<int>{1,2}),
                                MockReportRules.CreateRuleTreeItem(3, 1, new List<int>{1,3})
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
                                        MockReportRules.CreateRuleTreeItem(4, 3, new List<int>{2,1}),
                                        MockReportRules.CreateRuleTreeItem(5, 3, new List<int>{2,2}),
                                        MockReportRules.CreateRuleTreeItem(6, 3, new List<int>{2,3})
                                    }
                                },
                                new RuleTreeItem
                                {
                                    Header = "Section with inline layer",
                                    Children = new List<Basics.ITreeItem<Rule>>
                                    {
                                        MockReportRules.CreateRuleTreeItem(7, 4, new List<int>{2,4}),
                                        MockReportRules.CreateRuleTreeItem(8, 4, new List<int>{2,5},
                                            new List<Basics.ITreeItem<Rule>>
                                            {
                                                MockReportRules.CreateRuleTreeItem(10, 5, new List<int>{2,5,1}),
                                                MockReportRules.CreateRuleTreeItem(11, 5, new List<int>{2,5,2}),
                                                MockReportRules.CreateRuleTreeItem(12, 5, new List<int>{2,5,3})
                                            }
                                        ),
                                        MockReportRules.CreateRuleTreeItem(9, 4, new List<int>{2,6})
                                    }
                                }
                            }
                        }
                    }
                };
            }

            Rulebases =
            [
                MockReportRules.CreateRulebaseReport(rulebaseName: "Ordered layer with rules", numberOfRules: 3),
                MockReportRules.CreateRulebaseReport(rulebaseName: "Ordered layer with sections", numberOfRules: 0),
                MockReportRules.CreateRulebaseReport(rulebaseName: "First section in ordered layer", numberOfRules: 3),
                MockReportRules.CreateRulebaseReport(rulebaseName: "Section with inline layer", numberOfRules: 3),
                MockReportRules.CreateRulebaseReport(rulebaseName: "Inline layer", numberOfRules: 3)
            ];

            RulebaseLinks =
            [
                new RulebaseLink{ NextRulebaseId = 1, LinkType = 2, IsInitial = true },
                new RulebaseLink{ NextRulebaseId = 2, LinkType = 2 },
                new RulebaseLink{ FromRulebaseId = 2, NextRulebaseId = 3, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRulebaseId = 3, NextRulebaseId = 4, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRuleId = 8, FromRulebaseId = 4, NextRulebaseId = 5, LinkType = 3 }
            ];
        }


    }
}
