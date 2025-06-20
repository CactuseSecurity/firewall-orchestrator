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
        private List<RulebaseLink> _rulebaseLinks = new();
        private List<RulebaseReport> _rulebases = new();

        [OneTimeSetUp]
        public void SetUpTestClass()
        {
            _rulebaseLinks.Add(
                new RulebaseLink
                {
                    NextRulebaseId = 1,
                    LinkType = 2,
                    IsInitial = true,
                    IsGlobal = false,
                    IsSection = false
                }
            );

            _rulebases.Add(
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
            );
        }

        [OneTimeTearDown]
        public void TearDownTestClass()
        {
            _rulebaseLinks.Clear();
            _rulebases.Clear();
        }

        [SetUp]
        public void SetUpTestMethod()
        {
            _ruleTreeBuilder = new RuleTreeBuilder();
        }

        [Test]
        public void BuildRulebaseLinkQueue_WithEmptyArgs_ReturnsNull()
        {
            Assert.Fail();
        }

    }
}
