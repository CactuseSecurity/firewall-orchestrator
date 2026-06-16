using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Report;
using NUnit.Framework;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class ReportGeneratorTest
    {
        private sealed class ReportGeneratorApiConnection : SimulatedApiConnection
        {
            private readonly List<FwoOwner> owners;
            private readonly List<ManagementReport> relevantImports;
            private readonly Dictionary<int, ManagementReport> statisticsByManagementId;
            private readonly List<ModellingConnection> commonServices;
            private readonly List<WfTicket> tickets;
            private readonly List<WfState> states;
            private readonly List<OwnerLifeCycleState> ownerLifeCycleStates;
            private readonly List<OwnerResponsibleType> ownerResponsibleTypes;

            public ReportGeneratorApiConnection(
                List<FwoOwner>? owners = null,
                List<ManagementReport>? relevantImports = null,
                Dictionary<int, ManagementReport>? statisticsByManagementId = null,
                List<ModellingConnection>? commonServices = null,
                List<WfTicket>? tickets = null,
                List<WfState>? states = null,
                List<OwnerLifeCycleState>? ownerLifeCycleStates = null,
                List<OwnerResponsibleType>? ownerResponsibleTypes = null)
            {
                this.owners = owners ?? [];
                this.relevantImports = relevantImports ?? [];
                this.statisticsByManagementId = statisticsByManagementId ?? [];
                this.commonServices = commonServices ?? [];
                this.tickets = tickets ?? [];
                this.states = states ?? [];
                this.ownerLifeCycleStates = ownerLifeCycleStates ?? [];
                this.ownerResponsibleTypes = ownerResponsibleTypes ?? [];
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<WfTicket>))
                {
                    return Task.FromResult((QueryResponseType)(object)tickets);
                }
                if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
                {
                    return Task.FromResult((QueryResponseType)(object)states);
                }
                if (typeof(QueryResponseType) == typeof(List<OwnerLifeCycleState>) && query == OwnerQueries.getOwnerLifeCycleStates)
                {
                    return Task.FromResult((QueryResponseType)(object)ownerLifeCycleStates);
                }
                if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
                {
                    return Task.FromResult((QueryResponseType)(object)ownerResponsibleTypes);
                }
                if (typeof(QueryResponseType) == typeof(List<ModellingAppRole>) && query == ModellingQueries.getDummyAppRole)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppRole>());
                }
                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getCommonServices)
                {
                    return Task.FromResult((QueryResponseType)(object)commonServices);
                }
                if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)owners);
                }
                if (typeof(QueryResponseType) == typeof(List<ManagementReport>) && query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    return Task.FromResult((QueryResponseType)(object)relevantImports);
                }
                if (typeof(QueryResponseType) == typeof(List<ManagementReport>))
                {
                    int mgmId = GetVariable<int>(variables, QueryVar.MgmId);
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { statisticsByManagementId[mgmId] });
                }
                throw new NotImplementedException($"Unexpected query type {typeof(QueryResponseType).Name}.");
            }

            private static T GetVariable<T>(object? variables, string name)
            {
                if (variables is Dictionary<string, object> dict && dict.TryGetValue(name, out object? value))
                {
                    return (T)value;
                }
                throw new KeyNotFoundException(name);
            }
        }

        [Test]
        public async Task GenerateFromTemplate_ConnectionsLoadsGlobalCommonServices()
        {
            ReportTemplate template = BuildTemplate(ReportType.Connections);
            ModellingConnection commonService = new() { Id = 77, Name = "common-service" };

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(commonServices: [commonService]),
                new SimulatedUserConfig(),
                DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.ReportData.GlobalComSvc, Has.Count.EqualTo(1));
            Assert.That(report.ReportData.GlobalComSvc[0].GlobalComSvcs, Is.EqualTo(new List<ModellingConnection> { commonService }));
        }

        [Test]
        public async Task GenerateFromTemplate_TicketReportUsesGenericBranch()
        {
            ReportTemplate template = BuildTemplate(ReportType.TicketReport);
            List<WfTicket> tickets = [new() { Id = 1001, Title = "ticket" }];
            List<WfState> states = [new() { Id = 49, Name = "open" }];
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Admin];

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(tickets: tickets, states: states),
                userConfig,
                DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(report!.ReportData.Tickets, Is.EqualTo(tickets));
                Assert.That(report.ReportData.ElementsCount, Is.EqualTo(1));
                Assert.That(report.ReportData.WorkflowStateNames[49], Is.EqualTo("open"));
            });
        }

        [Test]
        public async Task GenerateFromTemplate_OwnersGeneratesOwnerOverviewData()
        {
            FwoOwner owner = BuildOwner(200, DateTime.Now.AddDays(20));
            owner.ExtAppId = "app-200";
            ReportTemplate template = BuildTemplate(ReportType.Owners);

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(
                    owners: [owner],
                    ownerLifeCycleStates: [new() { Id = 1, Name = "active", ActiveState = true }],
                    ownerResponsibleTypes: [new() { Id = 1, Name = "Main", Active = true, SortOrder = 1 }]),
                new SimulatedUserConfig(),
                DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.ReportData.OwnerData.Single().Owner, Is.EqualTo(owner));
        }

        [Test]
        public async Task GenerateFromTemplate_OwnerRecertificationClassifiesEffectiveRecertDates()
        {
            DateTime now = DateTime.Now;
            FwoOwner overdue = BuildOwner(1, now.AddDays(-1));
            FwoOwner upcoming = BuildOwner(2, now.AddDays(5));
            FwoOwner future = BuildOwner(3, now.AddDays(30));
            FwoOwner inactive = BuildOwner(4, now.AddDays(-10), recertActive: false);
            ReportTemplate template = BuildTemplate(ReportType.OwnerRecertification);
            template.ReportParams.RecertFilter.RecertificationDisplayPeriod = 10;

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(template, new ReportGeneratorApiConnection([overdue, upcoming, future, inactive]), new SimulatedUserConfig(), DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(overdue.RecertOverdue, Is.True);
                Assert.That(upcoming.RecertUpcoming, Is.True);
                Assert.That(future.RecertOverdue, Is.False);
                Assert.That(future.RecertUpcoming, Is.False);
                Assert.That(inactive.RecertOverdue, Is.False);
                Assert.That(report!.ReportData.RecertificationDisplayPeriod, Is.EqualTo(10));
            });
        }

        [Test]
        public async Task GenerateFromTemplate_OwnerRecertificationUsesCreationDateFallbackForClassification()
        {
            FwoOwner owner = BuildOwner(10, nextRecertDate: null);
            owner.RecertInterval = 14;
            owner.ChangelogOwners =
            [
                new()
                {
                    ChangeAction = ChangelogActionType.INSERT,
                    ChangeImport = new ChangeImport { Time = DateTime.Now.AddDays(-20) }
                }
            ];
            ReportTemplate template = BuildTemplate(ReportType.OwnerRecertification);
            template.ReportParams.RecertFilter.RecertificationDisplayPeriod = 3;

            await ReportGenerator.GenerateFromTemplate(template, new ReportGeneratorApiConnection([owner]), new SimulatedUserConfig(), DisplayNothing);

            Assert.That(owner.RecertOverdue, Is.True);
        }

        [Test]
        public async Task GenerateFromTemplate_StatisticsAggregatesOnlyRelevantManagements()
        {
            ReportTemplate template = BuildTemplate(ReportType.Statistics);
            template.ReportParams.DeviceFilter = new DeviceFilter(
            [
                new ManagementSelect { Id = 1, Devices = [new() { Id = 11, Selected = true }] }
            ]);
            ReportGeneratorApiConnection apiConnection = new(
                relevantImports:
                [
                    BuildRelevantImport(1, 101),
                    BuildRelevantImport(2, 202)
                ],
                statisticsByManagementId: new()
                {
                    [1] = BuildStatisticsManagement(1, ruleCount: 11, objectCount: 12, serviceCount: 13, userCount: 14, unusedRuleCount: 1),
                    [2] = BuildStatisticsManagement(2, ruleCount: 21, objectCount: 22, serviceCount: 23, userCount: 24, unusedRuleCount: 2)
                });

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(template, apiConnection, new SimulatedUserConfig(), DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(report!.ReportData.ManagementData.Single(mgm => mgm.Id == 1).Ignore, Is.False);
                Assert.That(report.ReportData.ManagementData.Single(mgm => mgm.Id == 2).Ignore, Is.True);
                Assert.That(report.ReportData.GlobalStats.RuleStatistics.ObjectAggregate.ObjectCount, Is.EqualTo(11));
                Assert.That(report.ReportData.GlobalStats.NetworkObjectStatistics.ObjectAggregate.ObjectCount, Is.EqualTo(12));
                Assert.That(report.ReportData.GlobalStats.ServiceObjectStatistics.ObjectAggregate.ObjectCount, Is.EqualTo(13));
                Assert.That(report.ReportData.GlobalStats.UserObjectStatistics.ObjectAggregate.ObjectCount, Is.EqualTo(14));
                Assert.That(report.ReportData.GlobalStats.UnusedRulesStatistics.ObjectAggregate.ObjectCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task GenerateFromTemplate_CatchesCancellationDuringGeneration()
        {
            ReportTemplate template = BuildTemplate(ReportType.Statistics);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(relevantImports: [BuildRelevantImport(1, 101)]),
                new SimulatedUserConfig(),
                DisplayNothing,
                cancellation.Token);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.ReportData.ManagementData, Is.Empty);
        }

        [Test]
        public async Task GenerateFromTemplate_ReturnsNullForUnsupportedReportType()
        {
            ReportTemplate template = BuildTemplate(ReportType.Undefined);

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(template, new ReportGeneratorApiConnection(), new SimulatedUserConfig(), DisplayNothing);

            Assert.That(report, Is.Null);
        }

        [Test]
        public void SetRelevantManagements_MarksUnselectedManagementsIgnored()
        {
            List<ManagementReport> managements = [new() { Id = 1 }, new() { Id = 2 }];
            DeviceFilter deviceFilter = new(
            [
                new ManagementSelect { Id = 2, Devices = [new() { Id = 22, Selected = true }] }
            ]);
            MethodInfo method = GetSetRelevantManagementsMethod();

            method.Invoke(null, new object?[] { managements, deviceFilter });

            Assert.Multiple(() =>
            {
                Assert.That(managements.Single(management => management.Id == 1).Ignore, Is.True);
                Assert.That(managements.Single(management => management.Id == 2).Ignore, Is.False);
            });
        }

        [Test]
        public void SetRelevantManagements_LeavesAllManagementsActiveWhenNoDeviceFilterIsSet()
        {
            List<ManagementReport> managements = [new() { Id = 1 }, new() { Id = 2 }];
            MethodInfo method = GetSetRelevantManagementsMethod();

            method.Invoke(null, new object?[] { managements, new DeviceFilter() });

            Assert.That(managements.All(management => !management.Ignore), Is.True);
        }

        private static MethodInfo GetSetRelevantManagementsMethod()
        {
            return typeof(ReportGenerator).GetMethod("SetRelevantManagements", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(nameof(ReportGenerator), "SetRelevantManagements");
        }

        private static ReportTemplate BuildTemplate(ReportType reportType)
        {
            return new()
            {
                ReportParams = new()
                {
                    ReportType = (int)reportType
                }
            };
        }

        private static FwoOwner BuildOwner(int id, DateTime? nextRecertDate, bool recertActive = true)
        {
            return new()
            {
                Id = id,
                Name = $"owner-{id}",
                RecertActive = recertActive,
                NextRecertDate = nextRecertDate,
                RecertInterval = 30
            };
        }

        private static ManagementReport BuildRelevantImport(int managementId, long relevantImportId)
        {
            return new()
            {
                Id = managementId,
                Import = new()
                {
                    ImportAggregate = new()
                    {
                        ImportAggregateMax = new() { RelevantImportId = relevantImportId }
                    }
                }
            };
        }

        private static ManagementReport BuildStatisticsManagement(int managementId, int ruleCount, int objectCount, int serviceCount, int userCount, int unusedRuleCount)
        {
            return new()
            {
                Id = managementId,
                RuleStatistics = BuildStatistics(ruleCount),
                NetworkObjectStatistics = BuildStatistics(objectCount),
                ServiceObjectStatistics = BuildStatistics(serviceCount),
                UserObjectStatistics = BuildStatistics(userCount),
                UnusedRulesStatistics = BuildStatistics(unusedRuleCount)
            };
        }

        private static ObjectStatistics BuildStatistics(int count)
        {
            return new() { ObjectAggregate = new() { ObjectCount = count } };
        }

        private static void DisplayNothing(Exception? exception, string title, string message, bool show)
        { }
    }
}
