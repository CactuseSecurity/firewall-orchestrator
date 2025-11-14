using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Test.Mocks;
using Microsoft.AspNetCore.Routing;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
                .SetValue(null, new Dictionary<(int, int), Rule[]>());
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
                .SetValue(null, new Dictionary<(int, int), Rule[]>());

            var result = ReportRules.GetAllRulesOfGateway(DeviceReportController.FromDeviceReport(device), management);

            Assert.That(result, Is.Empty);
        }


        
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
            var rules = new Rule[] { new Rule { Id = 1, RulebaseId = 1 } };

            // Cache manuell setzen
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), Rule[]> { [(device.Id, management.Id)] = rules });

            var result = ReportRules.GetAllRulesOfGateway(DeviceReportController.FromDeviceReport(device), management);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(1));
        }


        
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
            var cache = (Dictionary<(int, int), Rule[]>)cacheField!.GetValue(null)!;

            Assert.That(cache.ContainsKey((device.Id, management.Id)), Is.True);
            Assert.That(cache[(device.Id, management.Id)].Count, Is.EqualTo(3));
        }


        [Test]
        public void Test_GetRulesByDeviceAndRulebase_ReturnsAllRules_WithCorrectStructureAndOrder()
        {
            // ARRANGE -------------------------------------------------------------
            var rbInitial = MockReportRules.CreateRulebaseReport("InitialRB", 2);
            var rbOther = MockReportRules.CreateRulebaseReport("OtherRB", 2);

            var device = MockReportRules.CreateDeviceReport(
                deviceId: 42,
                deviceName: "DeviceX",
                rulebaseLinks: new List<RulebaseLink>
                {
            new RulebaseLink
            {
                GatewayId = 42,
                IsInitial = true,
                ToRulebase = new Rulebase
                {
                    Id = rbInitial.Id,
                    Name = rbInitial.Name!,
                    Rules = rbInitial.Rules
                },
                NextRulebaseId = (int)rbInitial.Id
            },
            new RulebaseLink
            {
                GatewayId = 42,
                IsInitial = false,
                ToRulebase = new Rulebase
                {
                    Id = rbOther.Id,
                    Name = rbOther.Name!,
                    Rules = rbOther.Rules
                },
                NextRulebaseId = (int)rbOther.Id
            }
                }
            );

            List<ManagementReport> SetupData() => new()
    {
        new ManagementReport
        {
            Id = 1,
            Name = "ManagementX",
            Devices = new[] { device },
            Rulebases = new[] { rbInitial, rbOther }
        }
    };

            var mgmt = SetupData().First();

            // Cache manuell fill with order
            var allRulesOrdered = rbInitial.Rules.Concat(rbOther.Rules).ToArray();
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), Rule[]> { [(device.Id, mgmt.Id)] = allRulesOrdered });

            var devCtrl = DeviceReportController.FromDeviceReport(device);

            // ACT ------------------------------------------------------------------
            var initialRules = ReportRules.GetInitialRulesOfGateway(devCtrl, mgmt);
            var retrievedAllRules = ReportRules.GetAllRulesOfGateway(devCtrl, mgmt);

            // ASSERT ---------------------------------------------------------------

            // 1. Initial Rules check
            ClassicAssert.AreEqual(rbInitial.Rules.Length, initialRules.Length);
            ClassicAssert.IsTrue(initialRules.All(r => r.RulebaseId == rbInitial.Id));

            // 2. All Rules check
            ClassicAssert.AreEqual(allRulesOrdered.Count(), retrievedAllRules.Length);

            // 3. Order check (InitialRB first, then OtherRB)
            for (int i = 0; i < retrievedAllRules.Count(); i++)
            {
                if (i < rbInitial.Rules.Length)
                {
                    ClassicAssert.AreEqual(rbInitial.Id, retrievedAllRules[i].RulebaseId);
                }
                else
                {
                    ClassicAssert.AreEqual(rbOther.Id, retrievedAllRules[i].RulebaseId);
                }
            }

            ClassicAssert.AreEqual(1, mgmt.Devices.Length);
            ClassicAssert.AreEqual(2, mgmt.Rulebases.Length);
            ClassicAssert.AreEqual(rbInitial.Rules.Length + rbOther.Rules.Length, retrievedAllRules.Length);
        }

        [Test]
        public void Test_GetRulesByDeviceAndRulebase_WithThreeLinks_InitialSecond_ReturnsCorrectOrder()
        {
            // ARRANGE -------------------------------------------------------------
            var rb1 = MockReportRules.CreateRulebaseReport("RB1", 2);
            var rb2 = MockReportRules.CreateRulebaseReport("RB2", 3); // initial
            var rb3 = MockReportRules.CreateRulebaseReport("RB3", 1);

            var device = MockReportRules.CreateDeviceReport(
                deviceId: 42,
                deviceName: "DeviceX",
                rulebaseLinks: new List<RulebaseLink>
                {
            new RulebaseLink
            {
                GatewayId = 42,
                IsInitial = false,
                ToRulebase = new Rulebase
                {
                    Id = rb1.Id,
                    Name = rb1.Name!,
                    Rules = rb1.Rules
                },
                NextRulebaseId = (int)rb1.Id
            },
            new RulebaseLink
            {
                GatewayId = 42,
                IsInitial = true,
                ToRulebase = new Rulebase
                {
                    Id = rb2.Id,
                    Name = rb2.Name!,
                    Rules = rb2.Rules
                },
                NextRulebaseId = (int)rb2.Id
            },
            new RulebaseLink
            {
                GatewayId = 42,
                IsInitial = false,
                ToRulebase = new Rulebase
                {
                    Id = rb3.Id,
                    Name = rb3.Name!,
                    Rules = rb3.Rules
                },
                NextRulebaseId = (int)rb3.Id
            }
                }
            );

            List<ManagementReport> SetupData() => new()
    {
        new ManagementReport
        {
            Id = 1,
            Name = "ManagementX",
            Devices = new[] { device },
            Rulebases = new[] { rb1, rb2, rb3 }
        }
    };

            var mgmt = SetupData().First();

            // Cache manuell fill mit der Reihenfolge: RB2 (initial) zuerst
            var allRulesOrdered = rb2.Rules.Concat(rb1.Rules).Concat(rb3.Rules).ToArray();
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), Rule[]> { [(device.Id, mgmt.Id)] = allRulesOrdered });

            var devCtrl = DeviceReportController.FromDeviceReport(device);



            // ACT ------------------------------------------------------------------
            var initialRules = ReportRules.GetInitialRulesOfGateway(devCtrl, mgmt);
            var retrievedAllRules = ReportRules.GetAllRulesOfGateway(devCtrl, mgmt);

            // ASSERT ---------------------------------------------------------------

            // 1. Initial Rules check
            ClassicAssert.AreEqual(rb2.Rules.Length, initialRules.Length);
            ClassicAssert.IsTrue(initialRules.All(r => r.RulebaseId == rb2.Id));

            // 2. Alle Rules check
            ClassicAssert.AreEqual(allRulesOrdered.Count(), retrievedAllRules.Length);

            // 3. Order check: RB2 (initial), dann RB1, dann RB3
            for (int i = 0; i < retrievedAllRules.Count(); i++)
            {
                if (i < rb2.Rules.Length)
                {
                    ClassicAssert.AreEqual(rb2.Id, retrievedAllRules[i].RulebaseId);
                }
                else if (i < rb2.Rules.Length + rb1.Rules.Length)
                {
                    ClassicAssert.AreEqual(rb1.Id, retrievedAllRules[i].RulebaseId);
                }
                else
                {
                    ClassicAssert.AreEqual(rb3.Id, retrievedAllRules[i].RulebaseId);
                }
            }

            // Struktur prüfen
            ClassicAssert.AreEqual(1, mgmt.Devices.Length);
            ClassicAssert.AreEqual(3, mgmt.Rulebases.Length);
            ClassicAssert.AreEqual(rb1.Rules.Length + rb2.Rules.Length + rb3.Rules.Length, retrievedAllRules.Length);
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
