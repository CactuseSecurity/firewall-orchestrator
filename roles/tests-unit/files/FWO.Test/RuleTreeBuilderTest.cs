using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using FWO.Services.RuleTreeBuilder;
using FWO.Data;
using FWO.Data.Report;
using FWO.Test.Mocks;
using FWO.Test.Tools.CustomAssert;
using NUnit.Framework.Legacy;

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

        #region BuildRulebaseLinkQueue

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
            CollectionAssert.AreEqual(RulebaseLinks!, queue!.Select(tuple => tuple.Item1)); // Equal because same objects
            Assert.That(IsEqualTo(Rulebases!, queue!.Select(tuple => tuple.Item2))); // Needs custom equality check, because rulebase objects get cloned in BuildRulebaseLinkQueue.

            /*
                For the basic setup, the order of the queue is exactly related to the order of the Arrays. Fuurther testing should verify if this is the case for every setup.
                If so this might be a possibility to reuce the complexity of the tested methods.
            */
        }

        [Test]
        public void BuildRulebaseLinkQueue_ComplexSetup_Succeeds()
        {
            // Arrange

            Queue<(RulebaseLink, RulebaseReport)>? queue;
            SetUpMockReportRulesComplex(true);

            // Act

            queue = _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Assert
            CollectionAssert.AreEqual(RulebaseLinks!, queue!.Select(tuple => tuple.Item1)); // Equal because same objects
            Assert.That(IsEqualTo(Rulebases!, queue!.Select(tuple => tuple.Item2))); // Needs custom equality check, because rulebase objects get cloned in BuildRulebaseLinkQueue.
        }

        #endregion

        #region BuildRuleTree

        [Test]
        public void BuildRuleTree_SectionInOrderedLayerAfterRules_Succeeds()
        {
            // Arrange

            _controlTree = new RuleTreeItem
            {
                IsRoot = true,
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    CreateControlTreeItemRulesThenSection(1, 0, 1, 0)
                }
            };

            Rulebases =
            [
                MockReportRules.CreateRulebaseReport("Rules then section", 3),
                MockReportRules.CreateRulebaseReport("Section 1", 3)
            ];

            RulebaseLinks =
            [
                new RulebaseLink{ NextRulebaseId = 1, LinkType = 2, IsInitial = true },
                new RulebaseLink{ FromRulebaseId = 1, NextRulebaseId = 2, LinkType = 4, IsSection = true }
            ];
            _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Act

            _ruleTreeBuilder.BuildRuleTree();

            // Assert

            AssertWithDump.AreEqual(_controlTree!.ToJson(), _ruleTreeBuilder.RuleTree.ToJson());
            //Assert.That(_ruleTreeBuilder.RuleTree.ToJson(), Is.EqualTo(_controlTree!.ToJson()));
        }

        [Test]
        public void BuildRuleTree_SectionsInInlineLayers_Succeeds()
        {
            // Arrange

            _controlTree = new RuleTreeItem
            {
                IsRoot = true,
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    CreateControlTreeItemSectionsInInlineLayers(1, 0, 1, 0)
                }
            };

            Rulebases =
            [
                MockReportRules.CreateRulebaseReport("Sections in inline layers", 5),
                MockReportRules.CreateRulebaseReport("Inline layer 5", 0),
                MockReportRules.CreateRulebaseReport("Section 1", 3),
                MockReportRules.CreateRulebaseReport("Inline layer 6", 3),
                MockReportRules.CreateRulebaseReport("Section 2", 3)
            ];

            RulebaseLinks =
            [
                new RulebaseLink{ NextRulebaseId = 1, LinkType = 2, IsInitial = true },
                new RulebaseLink{ FromRuleId = 1, FromRulebaseId = 1, NextRulebaseId = 2, LinkType = 3 },
                new RulebaseLink{ FromRulebaseId = 2, NextRulebaseId = 3, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRuleId = 2, FromRulebaseId = 1, NextRulebaseId = 4, LinkType = 3  },
                new RulebaseLink{ FromRulebaseId = 4, NextRulebaseId = 5, LinkType = 4, IsSection = true },
            ];
            _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Act

            _ruleTreeBuilder.BuildRuleTree();

            // Assert

            AssertWithDump.AreEqual(_controlTree!.ToJson(), _ruleTreeBuilder.RuleTree.ToJson());
            //Assert.That(_ruleTreeBuilder.RuleTree.ToJson(), Is.EqualTo(_controlTree!.ToJson()));
        }

        [Test]
        public void BuildRuleTree_BasicSetup_Succeeds()
        {
            // Arrange

            SetUpMockReportRulesBasic(true);
            _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Act

            _ruleTreeBuilder.BuildRuleTree();

            // Assert
            Assert.That(_ruleTreeBuilder.RuleTree.ToJson(), Is.EqualTo(_controlTree!.ToJson()));
        }

        [Test]
        public void BuildRuleTree_ComplexSetup_Succeeds()
        {
            // Arrange

            SetUpMockReportRulesComplex(true);
            _ruleTreeBuilder.BuildRulebaseLinkQueue(RulebaseLinks!, Rulebases!);

            // Act

            _ruleTreeBuilder.BuildRuleTree();

            // Assert

            AssertWithDump.AreEqual(_controlTree!.ToJson(), _ruleTreeBuilder.RuleTree.ToJson());
            //Assert.That(_ruleTreeBuilder.RuleTree.ToJson(), Is.EqualTo(_controlTree!.ToJson()));
        }

        #endregion

        #region GetSectionParent
       

        #endregion

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

        private void SetUpMockReportRulesComplex(bool buildControlTree)
        {
            if (buildControlTree)
            {
                _controlTree = new RuleTreeItem
                {
                    IsRoot = true,
                    Children = new List<Basics.ITreeItem<Rule>>
                    {
                        CreateControlTreeItemNoRulesButSection(),
                        CreateControlTreeItemRulesThenSection(2, 3, 2, 2),
                        CreateControlTreeItemInlineLayersFirst(),
                        CreateControlTreeItemInlineLayersInTheMiddle(),
                        CreateControlTreeItemInlineLayersInSections(),
                        CreateControlTreeItemSectionsInInlineLayers(6, 40, 5, 13),
                        CreateControlTreeItemInlineLayersInInlineLayers()
                    }
                };

            }

            Rulebases =
            [
                MockReportRules.CreateRulebaseReport("No rules but section", 0),                // 1
                MockReportRules.CreateRulebaseReport("Section 1", 3),                           // 2
                MockReportRules.CreateRulebaseReport("Rules then section", 3),                  // 3
                MockReportRules.CreateRulebaseReport("Section 2", 3),                           // 4
                MockReportRules.CreateRulebaseReport("Inline layer first", 4),                  // 5
                MockReportRules.CreateRulebaseReport("Inline layer 1", 3),                      // 6
                MockReportRules.CreateRulebaseReport("Inline layer in the middle", 7),          // 7
                MockReportRules.CreateRulebaseReport("Inline layer 2", 3),                      // 8
                MockReportRules.CreateRulebaseReport("Inline layers in sections", 0),           // 9
                MockReportRules.CreateRulebaseReport("Section 3", 1),                           // 10
                MockReportRules.CreateRulebaseReport("Inline layer 3", 3),                      // 11
                MockReportRules.CreateRulebaseReport("Section 4", 7),                           // 12
                MockReportRules.CreateRulebaseReport("Inline layer 4", 3),                      // 13
                MockReportRules.CreateRulebaseReport("Sections in inline layers", 5),           // 14
                MockReportRules.CreateRulebaseReport("Inline layer 5", 0),                      // 15
                MockReportRules.CreateRulebaseReport("Section 5", 3),                           // 16
                MockReportRules.CreateRulebaseReport("Inline layer 6", 3),                      // 17
                MockReportRules.CreateRulebaseReport("Section 6", 3),                           // 18
                MockReportRules.CreateRulebaseReport("Inline layers in inline layers", 5),      // 19
                MockReportRules.CreateRulebaseReport("Inline layer 7", 1),                      // 20    
                MockReportRules.CreateRulebaseReport("Inline layer 8", 3),                      // 21
                MockReportRules.CreateRulebaseReport("Inline layer 9", 4),                      // 22
                MockReportRules.CreateRulebaseReport("Inline layer 10", 3)                      // 23
            ];

            RulebaseLinks =
            [
                new RulebaseLink{ NextRulebaseId = 1, LinkType = 2, IsInitial = true },
                new RulebaseLink{ NextRulebaseId = 2, FromRulebaseId = 1, LinkType = 4, IsSection = true },
                new RulebaseLink{ NextRulebaseId = 3, FromRulebaseId = 1, LinkType = 2 },
                new RulebaseLink{ FromRulebaseId = 3, NextRulebaseId = 4, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRulebaseId = 3, NextRulebaseId = 5, LinkType = 2 },
                new RulebaseLink{ FromRuleId = 10, FromRulebaseId = 5, NextRulebaseId = 6, LinkType = 3 },
                new RulebaseLink{ FromRulebaseId = 5, NextRulebaseId = 7, LinkType = 2 },
                new RulebaseLink{ FromRuleId = 20, FromRulebaseId = 7, NextRulebaseId = 8, LinkType = 3 },
                new RulebaseLink{ FromRulebaseId = 7, NextRulebaseId = 9, LinkType = 2 },
                new RulebaseLink{ FromRulebaseId = 9, NextRulebaseId = 10, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRuleId = 27, FromRulebaseId = 10, NextRulebaseId = 11, LinkType = 3 },
                new RulebaseLink{ FromRulebaseId = 10, NextRulebaseId = 12, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRuleId = 34, FromRulebaseId = 12, NextRulebaseId = 13, LinkType = 3 },
                new RulebaseLink{ FromRulebaseId = 9, NextRulebaseId = 14, LinkType = 2 },
                new RulebaseLink{ FromRuleId = 41, FromRulebaseId = 14, NextRulebaseId = 15, LinkType = 3 },
                new RulebaseLink{ FromRulebaseId = 15, NextRulebaseId = 16, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRuleId = 42, FromRulebaseId = 14, NextRulebaseId = 17, LinkType = 3  },
                new RulebaseLink{ FromRulebaseId = 17, NextRulebaseId = 18, LinkType = 4, IsSection = true },
                new RulebaseLink{ FromRulebaseId = 14, NextRulebaseId = 19, LinkType = 2 },
                new RulebaseLink{ FromRuleId = 55, FromRulebaseId = 19, NextRulebaseId = 20, LinkType = 3 },
                new RulebaseLink{ FromRuleId = 60, FromRulebaseId = 20, NextRulebaseId = 21, LinkType = 3 },
                new RulebaseLink{ FromRuleId = 56, FromRulebaseId = 19, NextRulebaseId = 22, LinkType = 3 },
                new RulebaseLink{ FromRuleId = 67, FromRulebaseId = 22, NextRulebaseId = 23, LinkType = 3  }
            ];
        }

        private RuleTreeItem CreateControlTreeItemNoRulesButSection()
        {
            return new RuleTreeItem
            {
                Header = "No rules but section",
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    new RuleTreeItem
                    {
                        Header = "Section 1",
                        Children = new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(1, 2, new List<int> { 1, 1 }),
                            MockReportRules.CreateRuleTreeItem(2, 2, new List<int> { 1, 2 }),
                            MockReportRules.CreateRuleTreeItem(3, 2, new List<int> { 1, 3 })
                        }
                    }
                }
            };
        }

        private RuleTreeItem CreateControlTreeItemRulesThenSection(int orderedLayerNr, int ruleIdOffset, int sectionNumber, int rulebaseIdOffset)
        {
            return new RuleTreeItem
            {
                Header = "Rules then section",
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 1, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 1 }),
                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 2, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 2 }),
                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 3, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 3 }),
                    new RuleTreeItem
                    {
                        Header = $"Section {sectionNumber}",
                        Children = new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(ruleIdOffset + 4, rulebaseIdOffset + 2, new List<int> { orderedLayerNr, 4 }),
                            MockReportRules.CreateRuleTreeItem(ruleIdOffset + 5, rulebaseIdOffset + 2, new List<int> { orderedLayerNr, 5 }),
                            MockReportRules.CreateRuleTreeItem(ruleIdOffset + 6, rulebaseIdOffset + 2, new List<int> { orderedLayerNr, 6 })
                        }
                    }
                }
            };
        }

        private RuleTreeItem CreateControlTreeItemInlineLayersFirst()
        {
            return new RuleTreeItem
            {
                Header = "Inline layer first",
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    MockReportRules.CreateRuleTreeItem(10, 5, new List<int> { 3, 1 },
                        new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(14, 6, new List<int> { 3, 1, 1 }),
                            MockReportRules.CreateRuleTreeItem(15, 6, new List<int> { 3, 1, 2 }),
                            MockReportRules.CreateRuleTreeItem(16, 6, new List<int> { 3, 1, 3 })
                        }
                    ),
                    MockReportRules.CreateRuleTreeItem(11, 5, new List<int> { 3, 2 }),
                    MockReportRules.CreateRuleTreeItem(12, 5, new List<int> { 3, 3 }),
                    MockReportRules.CreateRuleTreeItem(13, 5, new List<int> { 3, 4 }),
                }
            };
        }

        private RuleTreeItem CreateControlTreeItemInlineLayersInTheMiddle()
        {
            return new RuleTreeItem
            {
                Header = "Inline layer in the middle",
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    MockReportRules.CreateRuleTreeItem(17, 7, new List<int> { 4, 1 }),
                    MockReportRules.CreateRuleTreeItem(18, 7, new List<int> { 4, 2 }),
                    MockReportRules.CreateRuleTreeItem(19, 7, new List<int> { 4, 3 }),
                    MockReportRules.CreateRuleTreeItem(20, 7, new List<int> { 4, 4 },
                        new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(24, 8, new List<int> { 4, 4, 1 }),
                            MockReportRules.CreateRuleTreeItem(25, 8, new List<int> { 4, 4, 2 }),
                            MockReportRules.CreateRuleTreeItem(26, 8, new List<int> { 4, 4, 3 })
                        }
                    ),
                    MockReportRules.CreateRuleTreeItem(21, 7, new List<int> { 4, 5 }),
                    MockReportRules.CreateRuleTreeItem(22, 7, new List<int> { 4, 6 }),
                    MockReportRules.CreateRuleTreeItem(23, 7, new List<int> { 4, 7 }),
                }
            };
        }

        private RuleTreeItem CreateControlTreeItemInlineLayersInSections()
        {
            return new RuleTreeItem
            {
                Header = "Inline layers in sections",
                Children = new List<Basics.ITreeItem<Rule>>
                {
                    new RuleTreeItem
                    {
                        Header = "Section 3",
                        Children = new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(27, 10, new List<int> { 5, 1 },
                                new List<Basics.ITreeItem<Rule>>
                                {
                                    MockReportRules.CreateRuleTreeItem(28, 11, new List<int> { 5, 1, 1 }),
                                    MockReportRules.CreateRuleTreeItem(29, 11, new List<int> { 5, 1, 2 }),
                                    MockReportRules.CreateRuleTreeItem(30, 11, new List<int> { 5, 1, 3 })
                                }
                            )
                        }
                    },
                    new RuleTreeItem
                    {
                        Header = "Section 4",
                        Children = new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(31, 12, new List<int> { 5, 2 }),
                            MockReportRules.CreateRuleTreeItem(32, 12, new List<int> { 5, 3 }),
                            MockReportRules.CreateRuleTreeItem(33, 12, new List<int> { 5, 4 }),
                            MockReportRules.CreateRuleTreeItem(34, 12, new List<int> { 5, 5 },
                                new List<Basics.ITreeItem<Rule>>
                                {
                                    MockReportRules.CreateRuleTreeItem(38, 13, new List<int> { 5, 5, 1 }),
                                    MockReportRules.CreateRuleTreeItem(39, 13, new List<int> { 5, 5, 2 }),
                                    MockReportRules.CreateRuleTreeItem(40, 13, new List<int> { 5, 5, 3 })
                                }
                            ),
                            MockReportRules.CreateRuleTreeItem(35, 12, new List<int> { 5, 6 }),
                            MockReportRules.CreateRuleTreeItem(36, 12, new List<int> { 5, 7 }),
                            MockReportRules.CreateRuleTreeItem(37, 12, new List<int> { 5, 8 })
                        }
                    }
                }
            };
        }

        private RuleTreeItem CreateControlTreeItemSectionsInInlineLayers(int orderedLayerNr, int ruleIdOffset, int sectionNumber, int rulebaseIdOffset)
        {
            return new RuleTreeItem
            {
                Header = "Sections in inline layers",
                Children = new List<Basics.ITreeItem<Rule>>
                {

                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 1, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 1 },
                        new List<Basics.ITreeItem<Rule>>
                        {
                            new RuleTreeItem
                            {
                                Header = $"Section {sectionNumber}",
                                Children = new List<Basics.ITreeItem<Rule>>
                                {
                                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 6, rulebaseIdOffset + 3, new List<int> { orderedLayerNr, 1, 1 }),
                                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 7, rulebaseIdOffset + 3, new List<int> { orderedLayerNr, 1, 2 }),
                                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 8, rulebaseIdOffset + 3, new List<int> { orderedLayerNr, 1, 3 })
                                }
                            },
                        }
                    ),

                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 2, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 2 },
                        new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(ruleIdOffset + 9, rulebaseIdOffset + 4, new List<int> { orderedLayerNr, 2, 1 }),
                            MockReportRules.CreateRuleTreeItem(ruleIdOffset + 10, rulebaseIdOffset + 4, new List<int> { orderedLayerNr, 2, 2 }),
                            MockReportRules.CreateRuleTreeItem(ruleIdOffset + 11, rulebaseIdOffset + 4, new List<int> { orderedLayerNr, 2, 3 }),
                            new RuleTreeItem
                            {
                                Header = $"Section {sectionNumber + 1}",

                                Children = new List<Basics.ITreeItem<Rule>>
                                {
                                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 12, rulebaseIdOffset + 5, new List<int> { orderedLayerNr, 2, 4 }),
                                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 13, rulebaseIdOffset + 5, new List<int> { orderedLayerNr, 2, 5 }),
                                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 14, rulebaseIdOffset + 5, new List<int> { orderedLayerNr, 2, 6 })
                                }

                            }
                        }
                    ),
                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 3, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 3 }),
                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 4, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 4 }),
                    MockReportRules.CreateRuleTreeItem(ruleIdOffset + 5, rulebaseIdOffset + 1, new List<int> { orderedLayerNr, 5 })
                }
            };
        }

        private RuleTreeItem CreateControlTreeItemInlineLayersInInlineLayers()
        {
            return new RuleTreeItem
            {
                Header = "Inline layers in inline layers",
                Children = new List<Basics.ITreeItem<Rule>>
                {

                    MockReportRules.CreateRuleTreeItem(55, 19, new List<int> { 7, 1 },
                        new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(60, 20, new List<int> { 7, 1, 1 },
                                new List<Basics.ITreeItem<Rule>>
                                {
                                    MockReportRules.CreateRuleTreeItem(61, 21, new List<int> { 7, 1, 1, 1 }),
                                    MockReportRules.CreateRuleTreeItem(62, 21, new List<int> { 7, 1, 1, 2 }),
                                    MockReportRules.CreateRuleTreeItem(63, 21, new List<int> { 7, 1, 1, 3 })
                                }
                            ),
                        }
                    ),

                    MockReportRules.CreateRuleTreeItem(56, 19, new List<int> { 7, 2 },
                        new List<Basics.ITreeItem<Rule>>
                        {
                            MockReportRules.CreateRuleTreeItem(64, 22, new List<int> { 7, 2, 1 }),
                            MockReportRules.CreateRuleTreeItem(65, 22, new List<int> { 7, 2, 2 }),
                            MockReportRules.CreateRuleTreeItem(66, 22, new List<int> { 7, 2, 3 }),
                            MockReportRules.CreateRuleTreeItem(67, 22, new List<int> { 7, 2, 4 },
                                new List<Basics.ITreeItem<Rule>>
                                {
                                    MockReportRules.CreateRuleTreeItem(68, 23, new List<int> { 7, 2, 4, 1 }),
                                    MockReportRules.CreateRuleTreeItem(69, 23, new List<int> { 7, 2, 4, 2 }),
                                    MockReportRules.CreateRuleTreeItem(70, 23, new List<int> { 7, 2, 4, 3 })
                                }
                            ),
                        }
                    ),
                    MockReportRules.CreateRuleTreeItem(57, 19, new List<int> { 7, 3 }),
                    MockReportRules.CreateRuleTreeItem(58, 19, new List<int> { 7, 4 }),
                    MockReportRules.CreateRuleTreeItem(59, 19, new List<int> { 7, 5 })
                }
            };
        }

        private bool IsEqualTo(RulebaseReport[] rulebaseCollection1, IEnumerable<RulebaseReport> rulebaseCollection2)
        {
            for (int i = 0; i < rulebaseCollection1.Length; i++)
            {
                RulebaseReport rulebase1 = rulebaseCollection1[i];
                RulebaseReport rulebase2 = rulebaseCollection2.ElementAt(i);

                bool propertiesEqual =
                    rulebase1.Id == rulebase2.Id ||
                    rulebase1.Name == rulebase2.Name ||
                    rulebase1.RuleChanges == rulebase2.RuleChanges ||
                    rulebase1.RuleStatistics == rulebase2.RuleStatistics;
                
                if (!propertiesEqual || rulebase1.Rules.Length != rulebase2.Rules.Length)
                {
                    return false;
                }
                
                for (int j = 0; j < rulebase1.Rules.Length; j++)
                {
                    if(!(rulebase1.Rules[j] == rulebase2.Rules[j]))
                    {
                        return false;
                    }   
                }

            }

            return true;
        }
    }
}
