using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using FWO.Report;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportRulesTest
    {
        [SetUp]
        public void SetUp()
        {
            SetRulesCache(new Dictionary<(int deviceId, int managementId), Rule[]>());
        }

        [Test]
        public void GetRulesByRulebaseId_ReturnsMatchingRules()
        {
            Rule expectedRule = new() { Id = 20 };
            ManagementReport managementReport = CreateManagementReport(
                new RulebaseReport { Id = 1, Rules = new[] { new Rule { Id = 10 } } },
                new RulebaseReport { Id = 2, Rules = new[] { expectedRule } });

            Rule[] rules = ReportRules.GetRulesByRulebaseId(2, managementReport);

            Assert.That(rules, Has.Length.EqualTo(1));
            Assert.That(rules[0].Id, Is.EqualTo(expectedRule.Id));
        }

        [Test]
        public void GetRulesByRulebaseId_ReturnsEmptyWhenIdUnknown()
        {
            ManagementReport managementReport = CreateManagementReport(
                new RulebaseReport { Id = 1, Rules = new[] { new Rule { Id = 10 } } });

            Rule[] rules = ReportRules.GetRulesByRulebaseId(42, managementReport);

            Assert.That(rules, Is.Empty);
        }

        [Test]
        public void GetInitialRulesOfGateway_ReturnsInitialRulebaseRules()
        {
            Rule expectedRule = new() { Id = 100 };
            ManagementReport managementReport = CreateManagementReport(
                new RulebaseReport { Id = 5, Rules = new[] { expectedRule } });
            DeviceReportController device = CreateDevice(1, new RulebaseLink { IsInitial = true, NextRulebaseId = 5 });

            Rule[] rules = ReportRules.GetInitialRulesOfGateway(device, managementReport);

            Assert.That(rules.Select(r => r.Id), Is.EqualTo(new[] { expectedRule.Id }));
        }

        [Test]
        public void GetAllRulesOfGateway_ReturnsCachedRules()
        {
            var cacheContent = new Dictionary<(int, int), Rule[]>
            {
                { (7, 11), new[] { new Rule { Id = 1 }, new Rule { Id = 2 } } }
            };
            SetRulesCache(cacheContent);
            DeviceReportController device = CreateDevice(7);
            ManagementReport managementReport = new() { Id = 11 };

            Rule[] rules = ReportRules.GetAllRulesOfGateway(device, managementReport);

            Assert.That(rules, Is.EqualTo(cacheContent[(7, 11)]));
        }

        [Test]
        public void GetAllRulesOfGateway_ReturnsEmptyWhenCacheEntryMissing()
        {
            DeviceReportController device = CreateDevice(3);
            ManagementReport managementReport = new() { Id = 4 };

            Rule[] rules = ReportRules.GetAllRulesOfGateway(device, managementReport);

            Assert.That(rules, Is.Empty);
        }

        [Test]
        public void GetRuleCount_CountsNestedRulebases()
        {
            Rule parentRule = new() { Id = 101 };
            Rule sectionRule = new() { Id = 102, SectionHeader = "header" };
            Rule childRule = new() { Id = 201 };
            ManagementReport managementReport = CreateManagementReport(
                new RulebaseReport { Id = 1, Rules = new[] { parentRule, sectionRule } },
                new RulebaseReport { Id = 2, Rules = new[] { childRule } });
            RulebaseLink[] links =
            {
                new RulebaseLink { IsInitial = true, NextRulebaseId = 1 },
                new RulebaseLink { FromRuleId = (int)parentRule.Id, NextRulebaseId = 2 }
            };

            int ruleCount = ReportRules.GetRuleCount(managementReport, links[0], links);

            Assert.That(ruleCount, Is.EqualTo(2));
        }

        [Test]
        public void GetRuleCount_ReturnsZeroWhenRulebaseMissing()
        {
            ManagementReport managementReport = CreateManagementReport();
            RulebaseLink missingLink = new() { IsInitial = true, NextRulebaseId = 99 };

            int ruleCount = ReportRules.GetRuleCount(managementReport, missingLink, Array.Empty<RulebaseLink>());

            Assert.That(ruleCount, Is.Zero);
        }

        private static void SetRulesCache(Dictionary<(int deviceId, int managementId), Rule[]> cache)
        {
            FieldInfo? cacheField = typeof(ReportRules).GetField("_rulesCache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(cacheField, Is.Not.Null, "Unable to access rules cache via reflection.");
            cacheField!.SetValue(null, cache);
        }

        private static ManagementReport CreateManagementReport(params RulebaseReport[] rulebases)
        {
            return new ManagementReport
            {
                Id = 1,
                Rulebases = rulebases.Length > 0 ? rulebases : Array.Empty<RulebaseReport>()
            };
        }

        private static DeviceReportController CreateDevice(int id, params RulebaseLink[] links)
        {
            return new DeviceReportController
            {
                Id = id,
                RulebaseLinks = links.Length > 0 ? links : Array.Empty<RulebaseLink>()
            };
        }
    }
}
