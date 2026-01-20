using FWO.Data;
using FWO.Data.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ManagementReportTest
    {
        [Test]
        public void GetAllRuleIdsReturnsDistinctRuleIds()
        {
            ManagementReport managementReport = new()
            {
                Rulebases =
                [
                    new RulebaseReport
                    {
                        Rules =
                        [
                            new Rule { Id = 10 },
                            new Rule { Id = 20 }
                        ]
                    },
                    new RulebaseReport
                    {
                        Rules =
                        [
                            new Rule { Id = 20 },
                            new Rule { Id = 30 }
                        ]
                    }
                ]
            };

            var result = managementReport.GetAllRuleIds();

            Assert.That(result, Is.EquivalentTo(new[] { 10L, 20L, 30L }));
        }
    }
}
