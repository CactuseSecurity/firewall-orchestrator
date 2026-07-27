using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Services.RuleTreeBuilder;
using FWO.Test.Mocks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportRulesTest
    {
        private static readonly int[] ExpectedStandardRulePageOffsets = [0, 2];
        private static readonly string[] ExpectedStandardStructureVariableKeys = [QueryVar.MgmId, QueryVar.ImportIdStart, QueryVar.ImportIdEnd];
        private static readonly int[] ExpectedStandardManagementIds = [1];
        private static readonly int[] ExpectedStandardRulebaseIds = [10, 20];
        private static readonly long[] ExpectedFirstStandardRulebaseRuleIds = [100, 101];
        private static readonly long[] ExpectedSecondStandardRulebaseRuleIds = [200];
        private static readonly long[] ExpectedSingleAttachedFirstRulebaseRuleIds = [100];
        private static readonly long[] ExpectedExistingAndAttachedRuleIds = [50, 100, 101];
        private static readonly long[] ExpectedSingleReportObjectIds = [1];
        private static readonly long[] ExpectedSingleReportServiceIds = [2];
        private static readonly long[] ExpectedSingleReportUserIds = [3];
        private static readonly long[] ExpectedMergedReportObjectIds = [1, 2];
        private static readonly long[] ExpectedMergedReportServiceIds = [2, 3];

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
                            FromRulebaseId = _rb2.Id,
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

            _rules = _rb2.Rules.Concat(_rb1.Rules).Concat(_rb3.Rules).ToArray();
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

            RuleTreeBuilder ruleTreeBuilder = new RuleTreeBuilder();
            ruleTreeBuilder.RuleTreeCache[(management.Id, device.Id)] = ruleTreeBuilder.RuleTree;
            ruleTreeBuilder.FlattenedRules[ruleTreeBuilder.RuleTree] = rules;

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
        public void Test_BuildRuleTree_RealRuleCountMatchesExpectedTraversalResult()
        {
            Rule[] allFlattenedRules = [.. _ruleTreeBuilder.BuildRuleTree(_managementReport!.Rulebases, _deviceReport!.RulebaseLinks, _managementReport.Id, _deviceReport.Id)];
            int count = allFlattenedRules.Count(rule => string.IsNullOrEmpty(rule.SectionHeader));

            ClassicAssert.AreEqual(6, count);
        }

        [Test]
        public void Test_TryBuildRuleTree_AccumulatesOnlyRealRulesAcrossDevices()
        {
            MockReportRules.RulebaseId = 0;
            MockReportRules.RuleId = 0;

            RulebaseReport rulebase1 = MockReportRules.CreateRulebaseReport("RB1", 2);
            RulebaseReport rulebase2 = MockReportRules.CreateRulebaseReport("RB2", 3);
            RulebaseReport rulebase3 = MockReportRules.CreateRulebaseReport("RB3", 1);

            DeviceReport firstDevice = MockReportRules.CreateDeviceReport(1, "Device1",
            [
                new RulebaseLink
                {
                    GatewayId = 1,
                    IsInitial = true,
                    ToRulebase = new Rulebase { Id = (long)rulebase2.Id, Name = rulebase2.Name!, Rules = rulebase2.Rules },
                    FromRulebaseId = 0,
                    NextRulebaseId = rulebase2.Id,
                    LinkType = 2
                },
                new RulebaseLink
                {
                    GatewayId = 1,
                    IsInitial = false,
                    ToRulebase = new Rulebase { Id = (long)rulebase1.Id, Name = rulebase1.Name!, Rules = rulebase1.Rules },
                    FromRulebaseId = rulebase2.Id,
                    NextRulebaseId = rulebase1.Id,
                    FromRuleId = (int)rulebase2.Rules.Last().Id,
                    IsSection = true,
                    LinkType = 4
                }
            ]);

            DeviceReport secondDevice = MockReportRules.CreateDeviceReport(2, "Device2",
            [
                new RulebaseLink
                {
                    GatewayId = 2,
                    IsInitial = true,
                    ToRulebase = new Rulebase { Id = (long)rulebase3.Id, Name = rulebase3.Name!, Rules = rulebase3.Rules },
                    FromRulebaseId = 0,
                    NextRulebaseId = rulebase3.Id,
                    LinkType = 2
                }
            ]);

            MockReportRules reportRules = new MockReportRules(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.ResolvedRules, () =>
            [
                new ManagementReport
                {
                    Id = 1,
                    Name = "Management1",
                    Devices = [firstDevice, secondDevice],
                    Rulebases = [rulebase1, rulebase2, rulebase3]
                }
            ]);

            reportRules.TryBuildMockRuleTree();

            Assert.That(reportRules.ReportData.ElementsCount, Is.EqualTo(6));
        }

        [Test]
        public void Test_TryBuildRuleTree_WithRawFilter_SuppressesEmptySectionHeaders()
        {
            MockReportRules.RulebaseId = 0;
            MockReportRules.RuleId = 0;

            RulebaseReport layerRulebase = MockReportRules.CreateRulebaseReport("Layer", 1);
            RulebaseReport emptySection = MockReportRules.CreateRulebaseReport("Empty Section", 0);
            RulebaseReport matchingSection = MockReportRules.CreateRulebaseReport("Matching Section", 1);

            DeviceReport device = MockReportRules.CreateDeviceReport(1, "Device1",
            [
                new RulebaseLink
                {
                    GatewayId = 1,
                    IsInitial = true,
                    FromRulebaseId = 0,
                    NextRulebaseId = layerRulebase.Id,
                    LinkType = 2
                },
                new RulebaseLink
                {
                    GatewayId = 1,
                    FromRulebaseId = layerRulebase.Id,
                    NextRulebaseId = emptySection.Id,
                    IsSection = true,
                    LinkType = 4
                },
                new RulebaseLink
                {
                    GatewayId = 1,
                    FromRulebaseId = emptySection.Id,
                    NextRulebaseId = matchingSection.Id,
                    IsSection = true,
                    LinkType = 4
                }
            ]);

            ManagementReport management = new()
            {
                Id = 1,
                Name = "Management1",
                Devices = [device],
                Rulebases = [layerRulebase, emptySection, matchingSection]
            };

            IServiceProvider? originalServices = FWO.Services.ServiceProvider.Services;
            ServiceCollection services = new();
            services.AddSingleton<IRuleTreeBuilder>(_ruleTreeBuilder);
            FWO.Services.ServiceProvider.Services = services.BuildServiceProvider();

            try
            {
                MockReportRules reportRules = new(new DynGraphqlQuery("name contains match"), new SimulatedUserConfig(), ReportType.ResolvedRules, () => [management]);

                reportRules.TryBuildMockRuleTree();

                Rule[] flattenedRules = ReportRules.GetAllRulesOfGateway(device, management, _ruleTreeBuilder);
                Assert.That(flattenedRules.Select(rule => rule.SectionHeader), Does.Not.Contain("Empty Section"));
                Assert.That(flattenedRules.Select(rule => rule.SectionHeader), Does.Contain("Matching Section"));
            }
            finally
            {
                FWO.Services.ServiceProvider.Services = originalServices;
            }
        }

        [Test]
        public void Test_TryBuildRuleTree_ReplacesScopedRuleTreeCacheBetweenReports()
        {
            ManagementReport CreateManagement(int managementId, int firstDeviceId, int deviceCount)
            {
                RulebaseReport rulebase = MockReportRules.CreateRulebaseReport($"RB{managementId}", 1);
                DeviceReport[] devices = [.. Enumerable.Range(firstDeviceId, deviceCount).Select(deviceId =>
                    MockReportRules.CreateDeviceReport(deviceId, $"Device{deviceId}",
                    [
                        new RulebaseLink
                        {
                            GatewayId = deviceId,
                            IsInitial = true,
                            FromRulebaseId = 0,
                            NextRulebaseId = rulebase.Id,
                            LinkType = 2
                        }
                    ]))];

                return new ManagementReport
                {
                    Id = managementId,
                    Name = $"Management{managementId}",
                    Devices = devices,
                    Rulebases = [rulebase]
                };
            }

            IServiceProvider? originalServices = FWO.Services.ServiceProvider.Services;
            ServiceCollection services = new();
            services.AddSingleton<IRuleTreeBuilder>(_ruleTreeBuilder);
            using var scopedServices = services.BuildServiceProvider();
            FWO.Services.ServiceProvider.Services = scopedServices;

            try
            {
                MockReportRules firstReport = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.ResolvedRules, () =>
                [
                    CreateManagement(1, 1, 2)
                ]);
                firstReport.TryBuildMockRuleTree();

                Assert.That(_ruleTreeBuilder.RuleTreeCache, Has.Count.EqualTo(2));
                Assert.That(_ruleTreeBuilder.FlattenedRules, Has.Count.EqualTo(2));

                MockReportRules secondReport = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.ResolvedRules, () =>
                [
                    CreateManagement(2, 10, 1)
                ]);
                secondReport.TryBuildMockRuleTree();

                Assert.That(_ruleTreeBuilder.RuleTreeCache, Has.Count.EqualTo(1));
                Assert.That(_ruleTreeBuilder.FlattenedRules, Has.Count.EqualTo(1));
                Assert.That(_ruleTreeBuilder.RuleTreeCache.ContainsKey((1, 1)), Is.False);
                Assert.That(_ruleTreeBuilder.RuleTreeCache.ContainsKey((1, 2)), Is.False);
                Assert.That(_ruleTreeBuilder.RuleTreeCache.ContainsKey((2, 10)), Is.True);
            }
            finally
            {
                FWO.Services.ServiceProvider.Services = originalServices;
            }
        }

        [TestCase(PreferredCollapseState.Collapsed, false)]
        [TestCase(PreferredCollapseState.Expanded, true)]
        [TestCase(PreferredCollapseState.Intermediate, true)]
        public void Test_TryBuildRuleTree_AppliesPreferredCollapseState(PreferredCollapseState preferredCollapseState, bool expectedExpandedState)
        {
            IServiceProvider? originalServices = FWO.Services.ServiceProvider.Services;
            ServiceCollection services = new();
            services.AddSingleton<IRuleTreeBuilder>(_ruleTreeBuilder);
            FWO.Services.ServiceProvider.Services = services.BuildServiceProvider();

            SimulatedUserConfig userConfig = new()
            {
                ReportingPersonalPreferredCollapseState = preferredCollapseState
            };
            try
            {
                MockReportRules reportRules = new(new DynGraphqlQuery(""), userConfig, ReportType.ResolvedRules, () => _managementReports);

                reportRules.TryBuildMockRuleTree();

                RuleTreeItem ruleTree = _ruleTreeBuilder.RuleTreeCache[(_managementReport!.Id, _deviceReport!.Id)];
                Assert.That(ruleTree.Children, Is.Not.Empty);
                RuleTreeItem expandableRule = ruleTree.Children.First();

                Assert.That(expandableRule.IsExpanded, Is.EqualTo(expectedExpandedState));
            }
            finally
            {
                FWO.Services.ServiceProvider.Services = originalServices;
            }
        }

        [Test]
        public async Task Test_GetObjectsForManagementInReport_UsesScopedFetchVariablesAndStopsAfterShortFirstPage()
        {
            ManagementReport management = new()
            {
                Id = 1,
                Import = new Import
                {
                    ImportAggregate = new ImportAggregate
                    {
                        ImportAggregateMax = new ImportAggregateMax { RelevantImportId = 77 }
                    }
                },
                Rulebases =
                [
                    new RulebaseReport
                    {
                        Id = 10,
                        Rules = [new Rule { Id = 100, RulebaseId = 10 }]
                    }
                ]
            };
            MockReportRules reportRules = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.ResolvedRules, () => [management]);
            Dictionary<string, object> queryVariables = new()
            {
                { QueryVar.MgmIds, management.Id },
                { QueryVar.Limit, 10 },
                { QueryVar.Offset, 0 }
            };
            RecordingObjectFetchApiConnection apiConnection = new(
                new ManagementReport
                {
                    ReportObjects = [new NetworkObject { Id = 1 }],
                    ReportServices = [new NetworkService { Id = 2 }],
                    ReportUsers = [new NetworkUser { Id = 3 }]
                });

            bool gotAllObjects = await reportRules.GetObjectsForManagementInReport(queryVariables, ObjCategory.all, 5, apiConnection, _ => Task.CompletedTask);

            Assert.That(gotAllObjects, Is.True);
            Assert.That(apiConnection.SentVariables, Has.Count.EqualTo(1));
            Assert.That(apiConnection.SentVariables[0][QueryVar.RuleIds], Is.EqualTo("{100}"));
            Assert.That(apiConnection.SentVariables[0][QueryVar.ImportIdStart], Is.EqualTo(77));
            Assert.That(apiConnection.SentVariables[0][QueryVar.ImportIdEnd], Is.EqualTo(77));
            Assert.That(queryVariables[QueryVar.Offset], Is.EqualTo(0));
            Assert.That(queryVariables.ContainsKey(QueryVar.RuleIds), Is.False);
            Assert.That(queryVariables.ContainsKey(QueryVar.ImportIdStart), Is.False);
            Assert.That(management.ReportObjects.Select(reportObject => reportObject.Id), Is.EqualTo(ExpectedSingleReportObjectIds));
            Assert.That(management.ReportServices.Select(reportService => reportService.Id), Is.EqualTo(ExpectedSingleReportServiceIds));
            Assert.That(management.ReportUsers.Select(reportUser => reportUser.Id), Is.EqualTo(ExpectedSingleReportUserIds));
        }

        [Test]
        public async Task Test_GetObjectsForManagementInReport_MergesFullFirstPageWithSecondPage()
        {
            ManagementReport management = new()
            {
                Id = 1,
                Import = new Import
                {
                    ImportAggregate = new ImportAggregate
                    {
                        ImportAggregateMax = new ImportAggregateMax { RelevantImportId = 77 }
                    }
                },
                Rulebases =
                [
                    new RulebaseReport
                    {
                        Id = 10,
                        Rules = [new Rule { Id = 100, RulebaseId = 10 }]
                    }
                ]
            };
            MockReportRules reportRules = new(new DynGraphqlQuery(""), new SimulatedUserConfig(), ReportType.ResolvedRules, () => [management]);
            Dictionary<string, object> queryVariables = new()
            {
                { QueryVar.MgmIds, management.Id },
                { QueryVar.Limit, 1 },
                { QueryVar.Offset, 0 },
                { QueryVar.ImportIdStart, 77 },
                { QueryVar.ImportIdEnd, 77 }
            };
            RecordingObjectFetchApiConnection apiConnection = new(
                new ManagementReport
                {
                    ReportObjects = [new NetworkObject { Id = 1 }],
                    ReportServices = [new NetworkService { Id = 2 }]
                },
                new ManagementReport
                {
                    ReportObjects = [new NetworkObject { Id = 2 }],
                    ReportServices = [new NetworkService { Id = 3 }]
                },
                new ManagementReport());

            bool gotAllObjects = await reportRules.GetObjectsForManagementInReport(queryVariables, ObjCategory.all, 5, apiConnection, _ => Task.CompletedTask);

            Assert.That(gotAllObjects, Is.True);
            Assert.That(apiConnection.SentVariables, Has.Count.EqualTo(3));
            Assert.That(apiConnection.SentVariables[0][QueryVar.Offset], Is.EqualTo(0));
            Assert.That(apiConnection.SentVariables[1][QueryVar.Offset], Is.EqualTo(1));
            Assert.That(apiConnection.SentVariables[2][QueryVar.Offset], Is.EqualTo(2));
            Assert.That(queryVariables[QueryVar.Offset], Is.EqualTo(0));
            Assert.That(management.ReportObjects.Select(reportObject => reportObject.Id), Is.EqualTo(ExpectedMergedReportObjectIds));
            Assert.That(management.ReportServices.Select(reportService => reportService.Id), Is.EqualTo(ExpectedMergedReportServiceIds));
        }

        [Test]
        public async Task Test_Generate_StandardRules_FetchesStructureOnceAndAttachesFlatRulePages()
        {
            DynGraphqlQuery query = new("")
            {
                FullQuery = "legacy-full-query",
                StandardRulesStructureQuery = "standard-rules-structure-query $import_id_start $import_id_end",
                StandardRulesPageQuery = "standard-rules-page-query",
                RelevantManagementIds = [1],
                QueryVariables = { ["fullTextFilter0"] = "%accept%" }
            };
            RuleTreeBuilder ruleTreeBuilder = new();
            ReportRules reportRules = new(query, new SimulatedUserConfig(), ReportType.Rules, ruleTreeBuilder);
            StandardRulesSplitApiConnection apiConnection = new();
            int callbackCount = 0;

            await reportRules.Generate(2, apiConnection, _ =>
            {
                callbackCount++;
                return Task.CompletedTask;
            }, CancellationToken.None);

            ManagementReport managementReport = reportRules.ReportData.ManagementData.Single();
            Assert.That(apiConnection.StructureQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.StructureQueryVariables, Has.Count.EqualTo(1));
            Assert.That(apiConnection.StructureQueryVariables[0].Keys, Is.EquivalentTo(ExpectedStandardStructureVariableKeys));
            Assert.That(apiConnection.StructureQueryVariables[0][QueryVar.MgmId], Is.EqualTo(ExpectedStandardManagementIds));
            Assert.That(apiConnection.StructureQueryVariables[0][QueryVar.ImportIdStart], Is.EqualTo(77));
            Assert.That(apiConnection.StructureQueryVariables[0][QueryVar.ImportIdEnd], Is.EqualTo(77));
            Assert.That(apiConnection.RulePageOffsets, Is.EqualTo(ExpectedStandardRulePageOffsets));
            Assert.That(apiConnection.RulePageRulebaseIds, Has.Count.EqualTo(2));
            Assert.That(apiConnection.RulePageRulebaseIds[0], Is.EqualTo(ExpectedStandardRulebaseIds));
            Assert.That(apiConnection.RulePageRulebaseIds[1], Is.EqualTo(ExpectedStandardRulebaseIds));
            Assert.That(apiConnection.LegacyFullQueryCount, Is.EqualTo(0));
            Assert.That(callbackCount, Is.EqualTo(2));
            Assert.That(managementReport.Rulebases.Single(rulebase => rulebase.Id == 10).Rules.Select(rule => rule.Id), Is.EqualTo(ExpectedFirstStandardRulebaseRuleIds));
            Assert.That(managementReport.Rulebases.Single(rulebase => rulebase.Id == 20).Rules.Select(rule => rule.Id), Is.EqualTo(ExpectedSecondStandardRulebaseRuleIds));
            Assert.That(reportRules.ReportData.ElementsCount, Is.EqualTo(3));
            Assert.That(ruleTreeBuilder.RuleTreeCache.ContainsKey((1, 1)), Is.True);
        }

        [Test]
        public async Task Test_Generate_StandardRules_SkipsRulePaging_WhenNoSelectedRulebases()
        {
            DynGraphqlQuery query = new("")
            {
                FullQuery = "legacy-full-query",
                StandardRulesStructureQuery = "standard-rules-structure-query $import_id_start $import_id_end",
                StandardRulesPageQuery = "standard-rules-page-query",
                RelevantManagementIds = [1]
            };
            ReportRules reportRules = new(query, new SimulatedUserConfig(), ReportType.Rules, new RuleTreeBuilder());
            StandardRulesSplitApiConnection apiConnection = new(includeSelectedRulebaseLinks: false);
            int callbackCount = 0;

            await reportRules.Generate(2, apiConnection, _ =>
            {
                callbackCount++;
                return Task.CompletedTask;
            }, CancellationToken.None);

            Assert.That(apiConnection.StructureQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.RulePageOffsets, Is.Empty);
            Assert.That(callbackCount, Is.EqualTo(0));
            Assert.That(reportRules.ReportData.ElementsCount, Is.EqualTo(0));
        }

        [Test]
        public void Test_GetRulebaseIdsForSelectedDevices_ReturnsOnlyLinkedKnownRulebases()
        {
            ManagementReport managementReport = new()
            {
                Rulebases =
                [
                    new RulebaseReport { Id = 10 },
                    new RulebaseReport { Id = 20 },
                    new RulebaseReport { Id = 30 }
                ],
                Devices =
                [
                    new DeviceReport
                    {
                        Id = 1,
                        RulebaseLinks =
                        [
                            new RulebaseLink { GatewayId = 1, NextRulebaseId = 20, FromRulebaseId = 10 },
                            new RulebaseLink { GatewayId = 1, NextRulebaseId = 999, FromRulebaseId = 0 },
                            new RulebaseLink { GatewayId = 1, NextRulebaseId = 20, FromRulebaseId = 10 }
                        ]
                    }
                ]
            };

            int[] rulebaseIds = ReportRules.GetRulebaseIdsForSelectedDevices(managementReport);

            Assert.That(rulebaseIds, Is.EqualTo(ExpectedStandardRulebaseIds));
        }

        [Test]
        public void Test_AttachRulesToRulebases_ReturnsAttachedAndSkippedCounts()
        {
            ManagementReport managementReport = new()
            {
                Rulebases =
                [
                    new RulebaseReport { Id = 10 },
                    new RulebaseReport { Id = 20 }
                ]
            };
            List<Rule> rules =
            [
                new() { Id = 100, RulebaseId = 10 },
                new() { Id = 200, RulebaseId = 20 },
                new() { Id = 999, RulebaseId = 999 }
            ];

            ReportRules.RuleAttachCounts counts = ReportRules.AttachRulesToRulebases(managementReport, rules);

            Assert.That(counts.Attached, Is.EqualTo(2));
            Assert.That(counts.Skipped, Is.EqualTo(1));
            Assert.That(managementReport.Rulebases.Single(rulebase => rulebase.Id == 10).Rules.Select(rule => rule.Id), Is.EqualTo(ExpectedSingleAttachedFirstRulebaseRuleIds));
            Assert.That(managementReport.Rulebases.Single(rulebase => rulebase.Id == 20).Rules.Select(rule => rule.Id), Is.EqualTo(ExpectedSecondStandardRulebaseRuleIds));
        }

        [Test]
        public void Test_AttachRulesToRulebases_AppendsToExistingRules()
        {
            ManagementReport managementReport = new()
            {
                Rulebases =
                [
                    new RulebaseReport
                    {
                        Id = 10,
                        Rules = [new Rule { Id = 50, RulebaseId = 10 }]
                    }
                ]
            };

            ReportRules.RuleAttachCounts counts = ReportRules.AttachRulesToRulebases(managementReport,
            [
                new Rule { Id = 100, RulebaseId = 10 },
                new Rule { Id = 101, RulebaseId = 10 }
            ]);

            Assert.That(counts.Attached, Is.EqualTo(2));
            Assert.That(counts.Skipped, Is.EqualTo(0));
            Assert.That(managementReport.Rulebases[0].Rules.Select(rule => rule.Id), Is.EqualTo(ExpectedExistingAndAttachedRuleIds));
        }

        [Test]
        public void Test_AttachRulesToRulebases_ReturnsZeroCounts_ForEmptyPage()
        {
            ReportRules.RuleAttachCounts counts = ReportRules.AttachRulesToRulebases(new ManagementReport(), []);

            Assert.That(counts.Attached, Is.EqualTo(0));
            Assert.That(counts.Skipped, Is.EqualTo(0));
        }

        private sealed class RecordingObjectFetchApiConnection(params ManagementReport[] pages) : SimulatedApiConnection
        {
            public List<Dictionary<string, object>> SentVariables { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                int pageIndex = SentVariables.Count;
                if (variables is Dictionary<string, object> queryVariables)
                {
                    SentVariables.Add(new Dictionary<string, object>(queryVariables));
                }

                object result = new List<ManagementReport> { pages[Math.Min(pageIndex, pages.Length - 1)] };
                return Task.FromResult((QueryResponseType)result);
            }
        }

        private sealed class StandardRulesSplitApiConnection(bool includeSelectedRulebaseLinks = true) : SimulatedApiConnection
        {
            public int StructureQueryCount { get; private set; }
            public int LegacyFullQueryCount { get; private set; }
            public List<int> RulePageOffsets { get; } = [];
            public List<int[]> RulePageRulebaseIds { get; } = [];
            public List<Dictionary<string, object>> StructureQueryVariables { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                object result;
                if (query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    result = new List<ManagementReport>
                    {
                        new()
                        {
                            Id = 1,
                            Name = "Management",
                            Import = new Import
                            {
                                ImportAggregate = new ImportAggregate
                                {
                                    ImportAggregateMax = new ImportAggregateMax { RelevantImportId = 77 }
                                }
                            }
                        }
                    };
                }
                else if (query == "standard-rules-structure-query $import_id_start $import_id_end")
                {
                    StructureQueryCount++;
                    if (variables is Dictionary<string, object> queryVariables)
                    {
                        StructureQueryVariables.Add(new Dictionary<string, object>(queryVariables));
                    }
                    DeviceReport[] devices = includeSelectedRulebaseLinks
                        ?
                        [
                            new DeviceReport
                            {
                                Id = 1,
                                Name = "Gateway",
                                RulebaseLinks =
                                [
                                    new RulebaseLink
                                    {
                                        GatewayId = 1,
                                        IsInitial = true,
                                        LinkType = 2,
                                        NextRulebaseId = 10
                                    },
                                    new RulebaseLink
                                    {
                                        GatewayId = 1,
                                        LinkType = 2,
                                        FromRulebaseId = 10,
                                        NextRulebaseId = 20
                                    }
                                ]
                            }
                        ]
                        : [];
                    result = new List<ManagementReport>
                    {
                        new()
                        {
                            Id = 1,
                            Name = "Management",
                            Devices = devices,
                            Rulebases =
                            [
                                new RulebaseReport { Id = 10, Name = "Layer 1" },
                                new RulebaseReport { Id = 20, Name = "Layer 2" }
                            ]
                        }
                    };
                }
                else if (query == "standard-rules-page-query" && variables is Dictionary<string, object> queryVariables)
                {
                    int offset = (int)queryVariables[QueryVar.Offset];
                    RulePageOffsets.Add(offset);
                    RulePageRulebaseIds.Add((int[])queryVariables[QueryVar.RulebaseIds]);
                    result = offset switch
                    {
                        0 => new List<Rule>
                        {
                            new() { Id = 100, RulebaseId = 10, RuleNumNumeric = 1, Name = "Rule 100" },
                            new() { Id = 101, RulebaseId = 10, RuleNumNumeric = 2, Name = "Rule 101" }
                        },
                        2 => new List<Rule>
                        {
                            new() { Id = 200, RulebaseId = 20, RuleNumNumeric = 1, Name = "Rule 200" }
                        },
                        _ => []
                    };
                }
                else if (query == "legacy-full-query")
                {
                    LegacyFullQueryCount++;
                    result = new List<ManagementReport>();
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected query: {query}");
                }

                return Task.FromResult((QueryResponseType)result);
            }
        }
    }
}
