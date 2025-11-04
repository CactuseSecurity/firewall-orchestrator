using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Test.Mocks;
using Microsoft.AspNetCore.Routing;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportRulesTest
    {
        [Test]
        public void MockReportRules_SetupSingleManagementReportEmpty_CreatesDevice()
        {
            var mock = new MockReportRules(null!, null!, ReportType.Rules);
            var reports = mock.SetupSingleManagementReportEmpty();

            Assert.That(reports.Count, Is.EqualTo(1));
            Assert.That(reports[0].Devices.Count, Is.EqualTo(1));
        }

        [Test]
        public void Test_SetupSingleManagementReportEmpty_CreatesDeviceWithoutRulebases()
        {
            var mock = new MockReportRules(
                query: null!,        // kann hier null sein, da Mock
                userConfig: null!,
                reportType: ReportType.Rules  // oder ein beliebiger Enum-Wert
            );

            // Zugriff auf erzeugte ManagementData
            var mgmtData = mock.ReportData.ManagementData;

            Assert.That(mgmtData, Is.Not.Empty);
            Assert.That(mgmtData[0].Devices.Count, Is.EqualTo(1));
            Assert.That(mgmtData[0].Rulebases.Count, Is.EqualTo(0));
        }

        [Test]
        public void Test_CreateRulebaseReport_CreatesExpectedNumberOfRules()
        {
            int numberOfRules = 3;
            var rulebaseReport = MockReportRules.CreateRulebaseReport("TestRB", numberOfRules);

            var managementReport = new ManagementReport
            {
                Rulebases = new[] { rulebaseReport }
            };

            var rbLink = new RulebaseLink { NextRulebaseId = rulebaseReport.Id };


            // Act
            var result = ReportRules.GetRulesByRulebaseId(rulebaseReport.Id, managementReport);
            int count = ReportRules.GetRuleCount(managementReport, rbLink, new[] { rbLink });

            Assert.That(count, Is.EqualTo(2));
            Assert.That(result.Length, Is.EqualTo(3));
            Assert.That(result[0].Uid, Does.StartWith($"rule-{rulebaseReport.Id}."));
            Assert.That(rulebaseReport.Rules.Length, Is.EqualTo(numberOfRules));
            Assert.That(rulebaseReport.Name, Is.EqualTo("TestRB"));
            Assert.That(rulebaseReport.Rules[0].Name, Does.StartWith("Mock Rule"));
        }


        [Test]
        public void Test_CreateDeviceReport_HasConsistentIds()
        {
            var device = MockReportRules.CreateDeviceReport(2, "Firewall-01");

            Assert.That(device.Id, Is.EqualTo(2));
            Assert.That(device.Uid, Is.EqualTo("device-2"));
            Assert.That(device.Name, Is.EqualTo("Firewall-01"));
        }

        [Test]
        public void Test_CreateRuleTreeItem_BuildsValidTree()
        {
            var child = MockReportRules.CreateRuleTreeItem(2, 1, new List<int> { 1, 2 });
            var parent = MockReportRules.CreateRuleTreeItem(1, 1, new List<int> { 1 }, new List<ITreeItem<Rule>> { child });

            Assert.That(parent.Children.Count, Is.EqualTo(1));
            Assert.That(parent.Children[0].Data!.Id, Is.EqualTo(2));
            Assert.That(parent.Data!.Id, Is.EqualTo(1));
        }








    }
}
