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
                    IsInitial = true,
                    IsGlobal = false,
                    IsSection = false
                }
            ];

            _rulebases =
            [
                new RulebaseReport
                {
                    Id = 1,
                    Name = "Ordered Layer",
                    Rules = new[]
                    {
                        new Rule { Id = 1, Uid = "rule-1.1", RulebaseId = 1 },
                        new Rule { Id = 2, Uid = "rule-1.2", RulebaseId = 1 },
                        new Rule { Id = 3, Uid = "rule-1.3", RulebaseId = 1 }
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
                        Header = "Ordered Layer",
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
