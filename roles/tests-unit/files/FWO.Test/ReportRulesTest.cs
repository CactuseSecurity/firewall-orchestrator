using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Test.Mocks;
using Microsoft.AspNetCore.Routing;
using Moq;
using NUnit.Framework;
using System.Reflection;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportRulesTest
    {
        private MockReportRules _mockReportRules = null!;
        private List<ManagementReport> _managementReports = new();


        [SetUp]
        public void setUp()
        {
            MockReportRules.RulebaseId = 0;
            MockReportRules.RuleId = 0;

            _mockReportRules = new MockReportRules(
                new Report.Filter.DynGraphqlQuery(""),
                new Config.Api.UserConfig(),
                ReportType.Rules,
                null!
            );
            _managementReports = _mockReportRules.SetupSingleManagementReportEmpty();
        }

        [TearDown]
        public void TearDown()
        {
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), List<Rule>>());
        }

        private (ManagementReport management, DeviceReport device) CreateBasicManagementSetup(int ruleCount = 3)
        {
            var rulebase = MockReportRules.CreateRulebaseReport("RB", ruleCount);
            var device = MockReportRules.CreateDeviceReport(1, "Device1", new List<RulebaseLink>
            {
                new RulebaseLink { NextRulebaseId = rulebase.Id }
            });
            var management = new ManagementReport
            {
                Id = 1,
                Rulebases = new[] { rulebase },
                Devices = new[] { device }
            };
            return (management, device);
        }


        [Test]
        public void Test_SetupSingleManagementReport_CreatesDeviceWithoutRulebases()
        {
            Assert.That(_managementReports!.Count, Is.EqualTo(1));
            Assert.That(_managementReports[0].Devices.Count, Is.EqualTo(1));
            Assert.That(_managementReports[0].Rulebases.Count, Is.EqualTo(0));
        }

        [Test]
        public void Test_CreateDeviceReport_HasConsistentId_And_Uid_For_Device1()
        {
            var device = MockReportRules.CreateDeviceReport(1, "Device1");

            Assert.That(device.Id, Is.EqualTo(1));
            Assert.That(device.Uid, Is.EqualTo("device-1"));
            Assert.That(device.Name, Is.EqualTo("Device1"));
        }

        [Test]
        public void Test_CreateDeviceReport_HasConsistentId_And_Uid_For_Device2()
        {
            var device = MockReportRules.CreateDeviceReport(2, "Firewall-01");

            Assert.That(device.Id, Is.EqualTo(2));
            Assert.That(device.Uid, Is.EqualTo("device-2"));
            Assert.That(device.Name, Is.EqualTo("Firewall-01"));
        }

        [Test]
        public void Test_RulebaseId_SetterGetter()
        {
            MockReportRules.RulebaseId = 10;
            Assert.That(MockReportRules.RulebaseId, Is.EqualTo(10));

            MockReportRules.RulebaseId = 20;
            Assert.That(MockReportRules.RulebaseId, Is.EqualTo(20));
        }

        [Test]
        public void Test_RuleId_SetterGetter()
        {
            MockReportRules.RuleId = 50;
            Assert.That(MockReportRules.RuleId, Is.EqualTo(50));

            MockReportRules.RuleId = 100;
            Assert.That(MockReportRules.RuleId, Is.EqualTo(100));
        }

        [Test]
        public void Test_ContainsRules_Scenarios()
        {
            var rulebaseReport = MockReportRules.CreateRulebaseReport();

            var managementReport = new ManagementReport
            {
                Rulebases = new[] { rulebaseReport }
            };

            Assert.That(managementReport.ContainsRules(), Is.False);

            var managementWithoutDevices = new ManagementReport { Devices = new DeviceReport[0] };
            Assert.That(managementWithoutDevices.ContainsRules(), Is.False);

            var managementWithDevicesWithoutRules = new ManagementReport
            {
                Devices = new DeviceReport[] { }
            };
            Assert.That(managementWithDevicesWithoutRules.ContainsRules(), Is.False);
        }

        [Test]
        public void Test_GetAllRulesOfGateway_ReturnsEmpty_WhenCacheEmpty()
        {
            var device = MockReportRules.CreateDeviceReport();
            var management = new ManagementReport();

            // Cache leeren
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), List<Rule>>());

            var result = ReportRules.GetAllRulesOfGateway(DeviceReportController.FromDeviceReport(device), management);

            Assert.That(result, Is.Empty);
        }


        //Mittlere Komplexität
        [Test]
        public void Test_CreateRulebaseReport_CreatesExpectedNumberOfRules()
        {
            int numberOfRules = 3;
            var (management, device) = CreateBasicManagementSetup(numberOfRules);

            var rulebase = management.Rulebases.First();
            var result = ReportRules.GetRulesByRulebaseId(rulebase.Id, management);

            var rulebaseLink = new RulebaseLink { NextRulebaseId = rulebase.Id };
            int count = ReportRules.GetRuleCount(management, rulebaseLink, new[] { rulebaseLink });

            Assert.That(count, Is.EqualTo(3));
            Assert.That(result.Length, Is.EqualTo(3));
            Assert.That(result[0].Uid, Does.StartWith($"rule-{rulebase.Id}."));
            Assert.That(rulebase.Rules.Length, Is.EqualTo(numberOfRules));
            Assert.That(rulebase.Name, Is.EqualTo("RB"));
            Assert.That(rulebase.Rules[0].Name, Does.StartWith("Mock Rule"));
        }

        [Test]
        public void Test_CreateRuleTreeItem_BuildsValidTree()
        {
            var ruleTreeItemChild = MockReportRules.CreateRuleTreeItem(2, 1, new List<int> { 1, 2 });
            var ruleTreeItemParent = MockReportRules.CreateRuleTreeItem(1, 1, new List<int> { 1 }, new List<ITreeItem<Rule>> { ruleTreeItemChild });

            Assert.That(ruleTreeItemParent.Children.Count, Is.EqualTo(1));
            Assert.That(ruleTreeItemParent.Children[0].Data!.Id, Is.EqualTo(2));
            Assert.That(ruleTreeItemParent.Data!.Id, Is.EqualTo(1));
        }

        [Test]
        public void Test_MultipleRulebases_InManagementReport()
        {
            var rulebase1 = MockReportRules.CreateRulebaseReport("Rulebase1", 2);
            var rulebase2 = MockReportRules.CreateRulebaseReport("Rulebase2", 3);
            var managementReport = new ManagementReport
            {
                Rulebases = new[] { rulebase1, rulebase2 }
            };

            Assert.That(managementReport.Rulebases.Length, Is.EqualTo(2));
            Assert.That(managementReport.Rulebases[0].Rules.Length, Is.EqualTo(2));
            Assert.That(managementReport.Rulebases[1].Rules.Length, Is.EqualTo(3));
            Assert.That(managementReport.Rulebases[0].Rules[0].Name, Is.EqualTo("Mock Rule 1"));
            Assert.That(managementReport.Rulebases[1].Rules[2].Name, Is.EqualTo("Mock Rule 5"));
        }

        [Test]
        public void Test_DeviceWithMultipleRulebaseLinks()
        {
            var rulebase1 = MockReportRules.CreateRulebaseReport("Rulebase1", 1);
            var rulebase2 = MockReportRules.CreateRulebaseReport("Rulebase2", 2);
            var device = MockReportRules.CreateDeviceReport(1, "Device1", new List<RulebaseLink>
            {
                new RulebaseLink { NextRulebaseId = rulebase1.Id },
                new RulebaseLink { NextRulebaseId = rulebase2.Id }
            });

            Assert.That(device.RulebaseLinks.Length, Is.EqualTo(2));
            Assert.That(device.RulebaseLinks[0].NextRulebaseId, Is.EqualTo(rulebase1.Id));
            Assert.That(device.RulebaseLinks[1].NextRulebaseId, Is.EqualTo(rulebase2.Id));
        }

        [Test]
        public void Test_GetAllRulesOfGateway_ReturnsRules_FromCache()
        {
            var device = MockReportRules.CreateDeviceReport();
            var management = new ManagementReport();
            var rules = new List<Rule> { new Rule { Id = 1, RulebaseId = 1 } };

            // Cache manuell setzen
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), List<Rule>> { [(device.Id, management.Id)] = rules });

            var result = ReportRules.GetAllRulesOfGateway(DeviceReportController.FromDeviceReport(device), management);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(1));
        }


        //Vielleicht schon Integration
        [Test]
        public void Test_TryBuildRuleTree_FallbackWithoutRuleTreeBuilder()
        {
            // Arrange
            var mockReportRules = new MockReportRules(
                new Report.Filter.DynGraphqlQuery(""),
                new Config.Api.UserConfig(),
                ReportType.Rules,
                null!
            );

            var (management, device) = CreateBasicManagementSetup(3);

            mockReportRules.ReportData.ManagementData.Add(management);

            // Act (via Reflection, da Methode private ist)
            var tryBuildRuleTreeMethod = typeof(ReportRules).GetMethod("TryBuildRuleTree",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            tryBuildRuleTreeMethod!.Invoke(mockReportRules, null);

            // Assert
            Assert.That(mockReportRules.ReportData.ElementsCount, Is.EqualTo(3)); // 3 Rules in Rulebase
            Assert.That(management.ReportedRuleIds.Count, Is.EqualTo(3));
            Assert.That(management.ReportedRuleIds, Is.EquivalentTo(management.Rulebases.First().Rules.Select(r => r.Id)));

            var cacheField = typeof(ReportRules).GetField("_rulesCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var cache = (Dictionary<(int, int), List<Rule>>)cacheField!.GetValue(null)!;

            Assert.That(cache.ContainsKey((device.Id, management.Id)), Is.True);
            Assert.That(cache[(device.Id, management.Id)].Count, Is.EqualTo(3));
        }

        [Test]
        public void Test_ConstructHtmlReport_GeneratesHtmlSections()
        {
            // Arrange
            var mockReportRules = new MockReportRules(
                new Report.Filter.DynGraphqlQuery(""),
                new Config.Api.UserConfig(),
                ReportType.Rules,
                null!
            );

            var (management, device) = CreateBasicManagementSetup(3);

            mockReportRules.ReportData.ManagementData.Add(management);

            management.Rulebases.First().Name = "Mock Rulebase";
            device.Name = "Mock Device1";

            var reportBuilder = new StringBuilder();
            int chapterNumber = 0;

            // Act
            mockReportRules.ConstructHtmlReport(ref reportBuilder, mockReportRules.ReportData.ManagementData, chapterNumber);

            // Assert
            string html = reportBuilder.ToString();
            Assert.That(html, Does.Contain("Mock Device1"));
            Assert.That(html, Does.Contain("<hr>"));
        }

        [Test]
        public void Test_GetInitialRulesOfGateway_ReturnsRules_WhenInitialRulebaseExists()
        {
            int numberOfRules = 2;
            var (management, device) = CreateBasicManagementSetup(numberOfRules);
            var deviceReportController = new DeviceReportController(DeviceReportController.FromDeviceReport(device));

            management.Devices.First().RulebaseLinks.First().GatewayId = device.Id;
            management.Devices.First().RulebaseLinks.First().IsInitial = true;
            var rulebase = management.Rulebases.First();
            var rulesFromRulebase = ReportRules.GetRulesByRulebaseId(rulebase.Id, management);

            var rules = ReportRules.GetInitialRulesOfGateway(deviceReportController, management);   //GetInitialRulebaseId - RulebaseLink not in Context 

            Assert.That(rulesFromRulebase.Length, Is.EqualTo(2));
            Assert.That(rules.Length, Is.EqualTo(2));
            Assert.That(rules.All(r => r.RulebaseId == 1));
        }

        [Test]
        public void Test_GetInitialRulesOfGateway_ReturnsEmpty_WhenNoInitialRulebase()
        {
            var management = new ManagementReport();
            var device = new DeviceReportController(DeviceReportController.FromDeviceReport(MockReportRules.CreateDeviceReport()));

            device.GetInitialRulebaseId(management);

            var rules = ReportRules.GetInitialRulesOfGateway(device, management);

            Assert.That(rules, Is.Empty);
        }
    }
}
