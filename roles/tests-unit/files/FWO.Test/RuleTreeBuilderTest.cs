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

    }
}
