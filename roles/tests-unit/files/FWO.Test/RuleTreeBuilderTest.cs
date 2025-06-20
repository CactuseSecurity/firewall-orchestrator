using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using FWO.Services.RuleTreeBuilder;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleTreeBuilderTest
    {
        private RuleTreeBuilder _ruleTreeBuilder = default!;
        private RulebaseLink[] _rulebaseLinks = default!;
        private RulebaseReport[] _rulebases = default!;
        private RuleTreeItem _control_tree = default!;

        [OneTimeSetUp]
        public void SetUpTestClass()
        {
            _rulebaseLinks =
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
            ];

            _rulebases =
            [
                new RulebaseReport
                {
                    Id = 1,
                    Name = "Ordered layer with rules",
                    Rules = new[]
                    {
                        new Rule { Id = 1, Uid = "rule-1.1", RulebaseId = 1 },
                        new Rule { Id = 2, Uid = "rule-1.2", RulebaseId = 1 },
                        new Rule { Id = 3, Uid = "rule-1.3", RulebaseId = 1 }
                    }
                },
                new RulebaseReport
                {
                    Id = 2,
                    Name = "Ordered layer with sections",
                },
                new RulebaseReport
                {
                    Id = 3,
                    Name = "First section in ordered layer",
                    Rules = new[]
                    {
                        new Rule { Id = 4, Uid = "rule-2.1", RulebaseId = 3 },
                        new Rule { Id = 5, Uid = "rule-2.2", RulebaseId = 3 },
                        new Rule { Id = 6, Uid = "rule-2.3", RulebaseId = 3 }
                    }
                },
                new RulebaseReport
                {
                    Id = 4,
                    Name = "Section with inline layer",
                    Rules = new[]
                    {
                        new Rule { Id = 7, Uid = "rule-2.4", RulebaseId = 4 },
                        new Rule { Id = 8, Uid = "rule-2.5", RulebaseId = 4 },
                        new Rule { Id = 9, Uid = "rule-2.6", RulebaseId = 4 }
                    }
                },
                new RulebaseReport
                {
                    Id = 5,
                    Name = "Inline layer",
                    Rules = new[]
                    {
                        new Rule { Id = 10, Uid = "rule-2.5.1", RulebaseId = 5 },
                        new Rule { Id = 11, Uid = "rule-2.5.2", RulebaseId = 5 },
                        new Rule { Id = 12, Uid = "rule-2.5.3", RulebaseId = 5 }
                    }
                }
            ];

            _control_tree = new RuleTreeItem
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
        }

        [SetUp]
        public void SetUpTestMethod()
        {
            _ruleTreeBuilder = new RuleTreeBuilder();
        }

        [Test]
        public void BuildRulebaseLinkQueue_WithEmptyArraysAsArgs_ReturnsNull()
        {
            // Arrange

            Queue<(RulebaseLink, RulebaseReport)>? queue;

            // Act

            queue = _ruleTreeBuilder.BuildRulebaseLinkQueue([], []);

            // Assert

            Assert.That(queue is null);
        }

        [Test]
        public void BuildRulebaseLinkQueue_WithArraysFromSetUp_ReturnsQueue()
        {
            // Arrange

            Queue<(RulebaseLink, RulebaseReport)>? queue;

            // Act

            queue = _ruleTreeBuilder.BuildRulebaseLinkQueue(_rulebaseLinks, _rulebases);

            // Assert

            Assert.That(queue is Queue<(RulebaseLink, RulebaseReport)>);
        }
        
        [Test]
        public void BuildRuleTree_WithArraysFromSetUp_CreatesCorrectStructure()
        {
            // Act

            _ruleTreeBuilder.BuildRulebaseLinkQueue(_rulebaseLinks, _rulebases);
            _ruleTreeBuilder.BuildRuleTree();

            // Assert
            Assert.That(_ruleTreeBuilder.RuleTree.ToJson(), Is.EqualTo(_control_tree.ToJson()));
        }

    }
}
