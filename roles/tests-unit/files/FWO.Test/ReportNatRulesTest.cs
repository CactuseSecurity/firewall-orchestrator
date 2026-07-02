using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportNatRulesTest
    {
        private NatRuleDisplayHtml _display = null!;
        private ReportNatRules _report = null!;

        [SetUp]
        public void SetUp()
        {
            var userConfig = new SimulatedUserConfig();
            _display = new NatRuleDisplayHtml(userConfig);
            _report = new ReportNatRules(new DynGraphqlQuery(""), userConfig, ReportType.NatRules);
        }

        [Test]
        public void ExportSingleRulebaseToHtml_EmptyRuleArray_ReturnsEmptyString()
        {
            var result = _report.ExportSingleRulebaseToHtml([], _display, chapterNumber: 1);

            Assert.That(result.Trim(), Is.Empty);
        }

        [Test]
        public void ExportSingleRulebaseToHtml_NormalRule_ContainsTableRow()
        {
            var rules = new[]
            {
                new Rule
                {
                    Id = 1,
                    Uid = "rule-uid-1",
                    Name = "TestRule",
                    SectionHeader = "",
                    NatData = new NatData()
                }
            };

            var result = _report.ExportSingleRulebaseToHtml(rules, _display, chapterNumber: 1);

            Assert.That(result, Does.Contain("<tr>"));
            Assert.That(result, Does.Contain("</tr>"));
        }

        [Test]
        public void ExportSingleRulebaseToHtml_NormalRule_DoesNotContainColspan()
        {
            var rules = new[]
            {
                new Rule
                {
                    Id = 1,
                    Uid = "rule-uid-1",
                    SectionHeader = "",
                    NatData = new NatData()
                }
            };

            var result = _report.ExportSingleRulebaseToHtml(rules, _display, chapterNumber: 1);

            Assert.That(result, Does.Not.Contain("colspan"));
        }

        [Test]
        public void ExportSingleRulebaseToHtml_SectionHeaderRule_ContainsColspan()
        {
            var rules = new[]
            {
                new Rule
                {
                    Id = 2,
                    SectionHeader = "My Section",
                    NatData = new NatData()
                }
            };

            var result = _report.ExportSingleRulebaseToHtml(rules, _display, chapterNumber: 1);

            Assert.That(result, Does.Contain("colspan"));
            Assert.That(result, Does.Contain("My Section"));
        }

        [Test]
        public void ExportSingleRulebaseToHtml_SectionHeaderRule_DoesNotContainRegularRuleCells()
        {
            var rules = new[]
            {
                new Rule
                {
                    Id = 3,
                    SectionHeader = "Section A",
                    NatData = new NatData()
                }
            };

            var result = _report.ExportSingleRulebaseToHtml(rules, _display, chapterNumber: 1);

            // A section row gets a single merged cell, no individual <td> for each column
            Assert.That(result, Does.Not.Contain("<td><td>"));
            var tdCount = result.Split("<td").Length - 1;
            Assert.That(tdCount, Is.EqualTo(1));
        }

        [Test]
        public void ExportSingleRulebaseToHtml_MultipleRules_ReturnsRowPerRule()
        {
            var rules = new[]
            {
                new Rule { Id = 1, SectionHeader = "", NatData = new NatData() },
                new Rule { Id = 2, SectionHeader = "", NatData = new NatData() },
                new Rule { Id = 3, SectionHeader = "", NatData = new NatData() }
            };

            var result = _report.ExportSingleRulebaseToHtml(rules, _display, chapterNumber: 1);

            int openTrCount = result.Split("<tr>").Length - 1;
            Assert.That(openTrCount, Is.EqualTo(3));
        }
    }
}
