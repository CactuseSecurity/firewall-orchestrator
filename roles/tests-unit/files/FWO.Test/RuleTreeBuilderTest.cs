using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using FWO.Services.RuleTreeBuilder;
using FWO.Data;
using FWO.Data.Report;
using FWO.Test.Mocks;
using FWO.Test.Tools.CustomAssert;
using NUnit.Framework.Legacy;
using FWO.Basics;

namespace FWO.Test
{
    [TestFixture]
    internal class RuleTreeBuilderTest
    {
        #region  Configuration

        private RulebaseReport[] _rulebases = default!;
        private RulebaseLink[] _rulebaseLinks = default!;
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
            _controlTree = new();
            _rulebaseLinks = CreateFullTestRulebaseLinks(gatewayId: 1).ToArray();
            _rulebases = CreateFullTestRulebaseReports();
        }

        #endregion

        #region TestInit

        [Test]
        public void BuildRuleTree_TestData_ReturnsRules()
        {
            // Arrange

            List<Rule> resultRules = default!;

            // Act

            resultRules = _ruleTreeBuilder.BuildRuleTree(_rulebases, _rulebaseLinks);

            // Assert

            Assert.That(resultRules.Any());
        }

        [Test]
        public void BuildRuleTree_TestData_ReturnsAllRules()
        {
            // Arrange

            List<Rule> resultRules = default!;

            // Act

            resultRules = _ruleTreeBuilder.BuildRuleTree(_rulebases, _rulebaseLinks);

            // Assert

            Assert.That(resultRules.Count == 68);
        }

        [Test]
        public void BuildRuleTree_TestData_ElementsFlatContainsAllRules()
        {
            // Arrange

            List<Rule> resultRules = default!;

            // Act

            resultRules = _ruleTreeBuilder.BuildRuleTree(_rulebases, _rulebaseLinks);

            // Assert

            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem) element).IsRule) == 68);
        }

        [Test]
        public void BuildRuleTree_TestData_ElementsFlatContainsAllOrderedLayers()
        {
            // Arrange

            List<Rule> resultRules = default!;

            // Act

            resultRules = _ruleTreeBuilder.BuildRuleTree(_rulebases, _rulebaseLinks);

            // Assert

            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem) element).IsOrderedLayerHeader) == 3);
        }

        [Test]
        public void BuildRuleTree_TestData_ElementsFlatContainsAllSections()
        {
            // Arrange

            List<Rule> resultRules = default!;

            // Act

            resultRules = _ruleTreeBuilder.BuildRuleTree(_rulebases, _rulebaseLinks);

            // Assert

            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem) element).IsSectionHeader) == 12);
        }

                [Test]
        public void BuildRuleTree_TestData_ElementsFlatContainsAllInlineLayers()
        {
            // Arrange

            List<Rule> resultRules = default!;

            // Act

            resultRules = _ruleTreeBuilder.BuildRuleTree(_rulebases, _rulebaseLinks);

            // Assert

            Assert.That(_ruleTreeBuilder.RuleTree.ElementsFlat.Count(element => ((RuleTreeItem) element).IsInlineLayerRoot) == 9);
        }



        #endregion

        #region Test Data

        public static List<RulebaseLink> CreateFullTestRulebaseLinks(int gatewayId)
        {
            return new List<RulebaseLink>
            {
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = null,   FromRuleId = null,   NextRulebaseId = 59375, LinkType = 2, IsInitial = true,  IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59375,  FromRuleId = 32034,  NextRulebaseId = 59376, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59375,  FromRuleId = null,   NextRulebaseId = 59377, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59377,  FromRuleId = null,   NextRulebaseId = 59378, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59378,  FromRuleId = 32038,  NextRulebaseId = 59379, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59378,  FromRuleId = null,   NextRulebaseId = 59380, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59380,  FromRuleId = 32046,  NextRulebaseId = 59381, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59380,  FromRuleId = null,   NextRulebaseId = 59382, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59382,  FromRuleId = null,   NextRulebaseId = 59383, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59384,  FromRuleId = null,   NextRulebaseId = 59385, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59383,  FromRuleId = null,   NextRulebaseId = 59386, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59375,  FromRuleId = null,   NextRulebaseId = 59387, LinkType = 2, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59387,  FromRuleId = null,   NextRulebaseId = 59388, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59387,  FromRuleId = null,   NextRulebaseId = 59389, LinkType = 2, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59389,  FromRuleId = 32078,  NextRulebaseId = 59390, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59390,  FromRuleId = null,   NextRulebaseId = 59391, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59391,  FromRuleId = 32083,  NextRulebaseId = 59392, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59389,  FromRuleId = 32079,  NextRulebaseId = 59393, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59393,  FromRuleId = null,   NextRulebaseId = 59394, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59376,  FromRuleId = 32164,  NextRulebaseId = 59839, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59379,  FromRuleId = null,   NextRulebaseId = 59843, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59380,  FromRuleId = 32168,  NextRulebaseId = 59384, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59380,  FromRuleId = 32155,  NextRulebaseId = 59847, LinkType = 3, IsInitial = false, IsGlobal = false, IsSection = false },
                new RulebaseLink { GatewayId = gatewayId, FromRulebaseId = 59847,  FromRuleId = null,   NextRulebaseId = 59848, LinkType = 4, IsInitial = false, IsGlobal = false, IsSection = true  },
            };
        }

        public static RulebaseReport[] CreateFullTestRulebaseReports()
        {
            return
            [
                new RulebaseReport { Id = 59375, Name = "Network", Rules = CreateFullTestRules(59375) },
                new RulebaseReport { Id = 59376, Name = "inline2", Rules = CreateFullTestRules(59376) },
                new RulebaseReport { Id = 59377, Name = "Test section 1", Rules = CreateFullTestRules(59377) },
                new RulebaseReport { Id = 59378, Name = "Section Xy", Rules = CreateFullTestRules(59378) },
                new RulebaseReport { Id = 59379, Name = "layer2 Clone", Rules = CreateFullTestRules(59379) },
                new RulebaseReport { Id = 59380, Name = "Test section 2", Rules = CreateFullTestRules(59380) },
                new RulebaseReport { Id = 59381, Name = "layer2", Rules = CreateFullTestRules(59381) },
                new RulebaseReport { Id = 59382, Name = "2nd new sect", Rules = CreateFullTestRules(59382) },
                new RulebaseReport { Id = 59383, Name = "3rd new sect", Rules = CreateFullTestRules(59383) },
                new RulebaseReport { Id = 59384, Name = "new inline layer", Rules = CreateFullTestRules(59384) },
                new RulebaseReport { Id = 59385, Name = "Section in inline layer", Rules = CreateFullTestRules(59385) },
                new RulebaseReport { Id = 59386, Name = "Changed Section Header", Rules = CreateFullTestRules(59386) },
                new RulebaseReport { Id = 59387, Name = "Second Layer", Rules = CreateFullTestRules(59387) },
                new RulebaseReport { Id = 59388, Name = "Section in second layer", Rules = CreateFullTestRules(59388) },
                new RulebaseReport { Id = 59389, Name = "Third Layer", Rules = CreateFullTestRules(59389) },
                new RulebaseReport { Id = 59390, Name = "InlineLayerWithSection", Rules = CreateFullTestRules(59390) },
                new RulebaseReport { Id = 59391, Name = "Section 1", Rules = CreateFullTestRules(59391) },
                new RulebaseReport { Id = 59392, Name = "Empty Layer With Section", Rules = CreateFullTestRules(59392) },
                new RulebaseReport { Id = 59393, Name = "InlineLayerWithRulesAndSection", Rules = CreateFullTestRules(59393) },
                new RulebaseReport { Id = 59394, Name = "Section 3", Rules = CreateFullTestRules(59394) },
                new RulebaseReport { Id = 59839, Name = "test3", Rules = CreateFullTestRules(59839) },
                new RulebaseReport { Id = 59843, Name = "section without name", Rules = CreateFullTestRules(59843) },
                new RulebaseReport { Id = 59847, Name = "new inline layer test", Rules = CreateFullTestRules(59847) },
                new RulebaseReport { Id = 59848, Name = "TestRuleNum", Rules = CreateFullTestRules(59848) }
            ];
        }

        public static Rule[] CreateFullTestRules(int rulebaseId)
        {
            return rulebaseId switch
            {
                59375 => new[]
                {
                    new Rule { Id = 32031, RulebaseId = 59375, Name = null },
                    new Rule { Id = 32162, RulebaseId = 59375, Name = null },
                    new Rule { Id = 32179, RulebaseId = 59375, Name = "sdfgsdf asddfasdf" },
                    new Rule { Id = 32354, RulebaseId = 59375, Name = "empty-svc-TestWEB" },
                },

                59376 => new[]
                {
                    new Rule { Id = 32037, RulebaseId = 59376, Name = "Cleanup rule" },
                    new Rule { Id = 32163, RulebaseId = 59376, Name = null },
                    new Rule { Id = 32164, RulebaseId = 59376, Name = null },
                },

                59378 => new[]
                {
                    new Rule { Id = 32038, RulebaseId = 59378, Name = null },
                    new Rule { Id = 32041, RulebaseId = 59378, Name = "All Internet to Local Host" },
                    new Rule { Id = 32166, RulebaseId = 59378, Name = "FWOC7" },
                    new Rule { Id = 32180, RulebaseId = 59378, Name = "FWOC21" },
                },

                59379 => new[]
                {
                    new Rule { Id = 32042, RulebaseId = 59379, Name = "Cleanup rule" },
                },

                59380 => new[]
                {
                    new Rule { Id = 32044, RulebaseId = 59380, Name = "Negation tests" },
                    new Rule { Id = 32046, RulebaseId = 59380, Name = null },
                    new Rule { Id = 32047, RulebaseId = 59380, Name = "All Internet to Local Host" },
                    new Rule { Id = 32155, RulebaseId = 59380, Name = "testrule 1.30.c" },
                    new Rule { Id = 32167, RulebaseId = 59380, Name = "FWOC4" },
                    new Rule { Id = 32168, RulebaseId = 59380, Name = "testrule 1.30" },
                    new Rule { Id = 32169, RulebaseId = 59380, Name = "New Rule2" },
                },

                59381 => new[]
                {
                    new Rule { Id = 32048, RulebaseId = 59381, Name = "Cleanup rule" },
                },

                59382 => new[]
                {
                    new Rule { Id = 32049, RulebaseId = 59382, Name = "testrule 1.14" },
                    new Rule { Id = 32050, RulebaseId = 59382, Name = "testrule 1.15" },
                    new Rule { Id = 32051, RulebaseId = 59382, Name = "testrule 1.16" },
                    new Rule { Id = 32052, RulebaseId = 59382, Name = "testrule 1.17" },
                    new Rule { Id = 32053, RulebaseId = 59382, Name = "testrule 1.18" },
                    new Rule { Id = 32054, RulebaseId = 59382, Name = "testrule 1.19" },
                    new Rule { Id = 32055, RulebaseId = 59382, Name = "testrule 1.21" },
                },

                59383 => new[]
                {
                    new Rule { Id = 32056, RulebaseId = 59383, Name = "testrule 1.23" },
                    new Rule { Id = 32058, RulebaseId = 59383, Name = "testrule 1.24" },
                    new Rule { Id = 32059, RulebaseId = 59383, Name = "testrule 1.25" },
                },

                59386 => new[]
                {
                    new Rule { Id = 32064, RulebaseId = 59386, Name = "testrule 1.26" },
                    new Rule { Id = 32065, RulebaseId = 59386, Name = "testrule 1.27" },
                    new Rule { Id = 32066, RulebaseId = 59386, Name = "testrule 1.28" },
                    new Rule { Id = 32067, RulebaseId = 59386, Name = "testrule 1.29" },
                    new Rule { Id = 32068, RulebaseId = 59386, Name = "testrule 1.31" },
                    new Rule { Id = 32069, RulebaseId = 59386, Name = "testrule 1.36 backup / restore test" },
                    new Rule { Id = 32070, RulebaseId = 59386, Name = "testrule 1.33" },
                    new Rule { Id = 32071, RulebaseId = 59386, Name = "testrule 1.34" },
                    new Rule { Id = 32072, RulebaseId = 59386, Name = "testrule 1.35" },
                    new Rule { Id = 32073, RulebaseId = 59386, Name = "testrule 1.37 backup / restore test" },
                    new Rule { Id = 32074, RulebaseId = 59386, Name = "testrule 1.38 backup / restore test" },
                    new Rule { Id = 32076, RulebaseId = 59386, Name = "Cleanup rule" },
                    new Rule { Id = 32174, RulebaseId = 59386, Name = null },
                },

                59388 => new[]
                {
                    new Rule { Id = 32077, RulebaseId = 59388, Name = "Cleanup rule" },
                },

                59389 => new[]
                {
                    new Rule { Id = 32078, RulebaseId = 59389, Name = "Rule 3.1" },
                    new Rule { Id = 32079, RulebaseId = 59389, Name = "change" },
                    new Rule { Id = 32175, RulebaseId = 59389, Name = "Rule 3.5" },
                    new Rule { Id = 32176, RulebaseId = 59389, Name = "Rule 3.4" },
                },

                59391 => new[]
                {
                    new Rule { Id = 32082, RulebaseId = 59391, Name = "Rule 3.1.1" },
                    new Rule { Id = 32083, RulebaseId = 59391, Name = "Rule 3.3" },
                    new Rule { Id = 32085, RulebaseId = 59391, Name = "Another change b" },
                    new Rule { Id = 32086, RulebaseId = 59391, Name = "Change Rule" },
                    new Rule { Id = 32177, RulebaseId = 59391, Name = "Yet another change" },
                },

                59392 => new[]
                {
                    new Rule { Id = 32087, RulebaseId = 59392, Name = "Cleanup rule" },
                },

                59393 => new[]
                {
                    new Rule { Id = 32089, RulebaseId = 59393, Name = "Rule 3.3.1" },
                    new Rule { Id = 32178, RulebaseId = 59393, Name = "rule with group of domain objs" },
                },

                59394 => new[]
                {
                    new Rule { Id = 32090, RulebaseId = 59394, Name = "Rule 3.3.4" },
                    new Rule { Id = 32091, RulebaseId = 59394, Name = "Rule 3.3.5" },
                    new Rule { Id = 32190, RulebaseId = 59394, Name = "test-initial-violations" },
                },

                59839 => new[]
                {
                    new Rule { Id = 32395, RulebaseId = 59839, Name = "Cleanup rule" },
                },

                59847 => new[]
                {
                    new Rule { Id = 32396, RulebaseId = 59847, Name = "testrule 1.30.1c" },
                    new Rule { Id = 32397, RulebaseId = 59847, Name = "changed" },
                },

                59848 => new[]
                {
                    new Rule { Id = 32398, RulebaseId = 59848, Name = "test 1.30.3c" },
                    new Rule { Id = 32399, RulebaseId = 59848, Name = "Cleanup rule 2c" },
                },

                _ => Array.Empty<Rule>()
            };
        }

        #endregion
    }
}
