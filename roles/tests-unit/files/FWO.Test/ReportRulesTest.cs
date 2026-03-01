using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Test.Mocks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportRulesTest
    {
        private List<ManagementReport> _managementReports = new();
        private DeviceReport? _deviceReport;
        private ManagementReport? _managementReport;

        private RulebaseReport? _rb1;
        private RulebaseReport? _rb2;
        private RulebaseReport? _rb3;

        private Rule[] _rules = new Rule[0];

        private RuleTreeBuilder _ruleTreeBuilder = new();

        [SetUp]
        public void SetUp()
        {
            MockReportRules.RulebaseId = 0;
            MockReportRules.RuleId = 0;

            // ARRANGE -------------------------------------------------------------
            _rb1 = MockReportRules.CreateRulebaseReport("RB1", 2);
            _rb2 = MockReportRules.CreateRulebaseReport("RB2", 3);
            _rb3 = MockReportRules.CreateRulebaseReport("RB3", 1);

            _deviceReport = MockReportRules.CreateDeviceReport(
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
                            Id = _rb2.Id,
                            Name = _rb2.Name!,
                            Rules = _rb2.Rules
                        },
                        FromRulebaseId = 0,         //before
                        NextRulebaseId = _rb2.Id,   //myself
                        LinkType = 2
                    },
                    new RulebaseLink
                    {
                            GatewayId = 42,
                            IsInitial = false,
                            ToRulebase = new Rulebase
                            {
                                Id = _rb1.Id,
                                Name = _rb1.Name!,
                                Rules = _rb1.Rules
                            },
                            NextRulebaseId = _rb1.Id,
                            FromRulebaseId = _rb2.Id,
                            FromRuleId = 5,         // Last Rule from _rb2
                            IsSection = true,
                            LinkType = 4
                    },
                    new RulebaseLink
                    {
                            GatewayId = 42,
                            IsInitial = false,
                            ToRulebase = new Rulebase
                            {
                                Id = _rb3.Id,
                                Name = _rb3.Name!,
                                Rules = _rb3.Rules
                            },
                            NextRulebaseId = _rb3.Id,
                            FromRulebaseId = _rb1.Id,
                            FromRuleId = 3,     // Last Rule from _rb1
                            LinkType = 2
                    }
                }
            );

            _managementReports = new()
            {
                new ManagementReport
                {
                    Id = 1,
                    Name = "ManagementX",
                    Devices = [_deviceReport],
                    Rulebases = [_rb1, _rb2, _rb3]
                }
            };

            _managementReport = _managementReports.First();

            // Cache manuell fill mit der Reihenfolge: RB2 (initial) zuerst
            _rules = _rb2.Rules.Concat(_rb1.Rules).Concat(_rb3.Rules).ToArray();
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), Rule[]> { [(_deviceReport.Id, _managementReport.Id)] = _rules });

            _ruleTreeBuilder.Reset(_managementReport.Rulebases, _deviceReport.RulebaseLinks);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), Rule[]>());
        }

        [Test]
        public void Test_SetupSingleManagementReport_CreatesDeviceWithoutRulebases()
        {
            Assert.That(_managementReports!.Count, Is.EqualTo(1));
            Assert.That(_managementReports[0].Devices.Count, Is.EqualTo(1));
            Assert.That(_managementReports[0].Rulebases.Count, Is.EqualTo(3));
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

            var rb = MockReportRules.CreateRulebaseReport("RB101", 2);
            Assert.That(rb.Rules[0].Id, Is.EqualTo(101));
        }

        [Test]
        public void Test_ContainsRules_ReturnsTrue_WhenRulesExist()
        {
            var rulebaseReport = MockReportRules.CreateRulebaseReport();

            var managementReport = new ManagementReport
            {
                Rulebases = new[] { rulebaseReport }
            };

            Assert.That(managementReport.ContainsRules(), Is.False);
            Assert.That(_deviceReport!.ContainsRules(), Is.True);
            Assert.That(_managementReport!.ContainsRules(), Is.True);
        }

        [Test]
        public void Test_GetAllRulesOfGateway_ReturnsEmpty_WhenCacheEmpty()
        {
            // ARRANGE

            _ruleTreeBuilder.RuleTreeCache.Clear();

            // ACT

            var result = ReportRules.GetAllRulesOfGateway(_deviceReport!, _managementReport!, _ruleTreeBuilder);

            // ASSERT

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Test_CreateRulebaseReport_CreatesExpectedNumberOfRules()
        {
            var rulebase = _managementReport!.Rulebases.First();
            var result = ReportRules.GetRulesByRulebaseId(rulebase.Id, _managementReport);

            var rulebaseLink = new RulebaseLink { NextRulebaseId = rulebase.Id };
            int count = ReportRules.GetRuleCount(_managementReport, rulebaseLink, new[] { rulebaseLink });

            Assert.That(count, Is.EqualTo(2));
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].Uid, Does.StartWith($"rule-{rulebase.Id}."));
            Assert.That(rulebase.Rules.Length, Is.EqualTo(2));
            Assert.That(rulebase.Name, Is.EqualTo("RB1"));
            Assert.That(rulebase.Rules[0].Name, Does.StartWith("Mock Rule"));
        }

        [Test]
        public void Test_MultipleRulebases_InManagementReport()
        {
            MockReportRules.RulebaseId = 0;
            MockReportRules.RuleId = 0;
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
            device.Id = 1;
            var management = new ManagementReport();
            management.Id = 1;
            var rules = new Rule[] { new Rule { Id = 1, RulebaseId = 1 } };

            // Cache manuell setzen
            typeof(ReportRules)
                .GetField("_rulesCache", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new Dictionary<(int, int), Rule[]> { [(device.Id, management.Id)] = rules });

            RuleTreeBuilder ruleTreeBuilder = new RuleTreeBuilder();
            ruleTreeBuilder.Reset(management.Rulebases, device.RulebaseLinks);
            ruleTreeBuilder.RuleTreeCache[(management.Id, device.Id)] = ruleTreeBuilder.RuleTree;
            ruleTreeBuilder.FlattedRules[ruleTreeBuilder.RuleTree] = rules;

            var result = ReportRules.GetAllRulesOfGateway(device, management, ruleTreeBuilder);

            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(1));
        }

        [Test]
        public void Test_GetRulesByDeviceAndRulebase_WithThreeLinks_InitialSecond_ReturnsCorrectOrder()
        {
            // ARRANGE

            _ruleTreeBuilder.BuildRuleTree(_managementReport!.Rulebases, _deviceReport!.RulebaseLinks, _managementReport.Id, _deviceReport.Id);


            // ACT

            var initialRules = ReportRules.GetInitialRulesOfGateway(_deviceReport!, _managementReport!);
            var retrievedAllRules = ReportRules.GetAllRulesOfGateway(_deviceReport!, _managementReport!, _ruleTreeBuilder);
            var retrieveRulesWithoutDummies = retrievedAllRules.Where(rule => rule.SectionHeader == "").ToArray(); // rulebases get their dummy rules to be displayable in blazor table, so we have to exclude these here

            // ASSERT

            // Initial rules correct

            ClassicAssert.AreEqual(_rb2!.Rules.Length, initialRules.Length);
            ClassicAssert.IsTrue(initialRules.All(r => r.RulebaseId == _rb2!.Id));

            // All rules of gateway correct

            ClassicAssert.AreEqual(_rules.Count(), retrieveRulesWithoutDummies.Length);

            // Order correct

            for (int i = 0; i < retrieveRulesWithoutDummies.Count(); i++)
            {
                if (i < _rb2.Rules.Length)
                {
                    ClassicAssert.AreEqual(_rb2.Id, retrieveRulesWithoutDummies[i].RulebaseId);
                }
                else if (i < _rb2.Rules.Length + _rb1!.Rules.Length)
                {
                    ClassicAssert.AreEqual(_rb1.Id, retrieveRulesWithoutDummies[i].RulebaseId);
                }
                else
                {
                    ClassicAssert.AreEqual(_rb3!.Id, retrieveRulesWithoutDummies[i].RulebaseId);
                }
            }

            // Structure correct

            ClassicAssert.AreEqual(1, _managementReport!.Devices.Length);
            ClassicAssert.AreEqual(3, _managementReport.Rulebases.Length);
            ClassicAssert.AreEqual(_rb1!.Rules.Length + _rb2.Rules.Length + _rb3!.Rules.Length, retrieveRulesWithoutDummies.Length);
        }

        [Test]
        public void Test_GetInitialRulesOfGateway_ReturnsEmpty_WhenNoInitialRulebase()
        {
            var management = new ManagementReport();
            var device = MockReportRules.CreateDeviceReport();

            device.GetInitialRulebaseId(management);

            var rules = ReportRules.GetInitialRulesOfGateway(device, management);

            Assert.That(rules, Is.Empty);
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
        public void Test_GetRuleCount_FollowsCorrectRulebaseTraversal()
        {
            var count = ReportRules.GetRuleCount(
                _managementReport!,
                _deviceReport!.RulebaseLinks.First(l => l.IsInitial),
                [.. _deviceReport!.RulebaseLinks]
            );

            ClassicAssert.AreEqual(6, count);
        }
    }
}
