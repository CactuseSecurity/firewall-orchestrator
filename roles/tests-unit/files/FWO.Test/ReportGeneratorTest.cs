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
            private readonly List<ModellingConnection> ownerConnections;
            private readonly List<WfTicket> tickets;
            private readonly List<WfState> states;
            private readonly List<WfExtState> extStates;
            private readonly List<OwnerLifeCycleState> ownerLifeCycleStates;
            private readonly List<OwnerResponsibleType> ownerResponsibleTypes;
            private readonly bool emptyForUnknownLists;

            public ReportGeneratorApiConnection(
                List<FwoOwner>? owners = null,
                List<ManagementReport>? relevantImports = null,
                Dictionary<int, ManagementReport>? statisticsByManagementId = null,
                List<ModellingConnection>? commonServices = null,
                List<ModellingConnection>? ownerConnections = null,
                List<WfTicket>? tickets = null,
                List<WfState>? states = null,
                List<WfExtState>? extStates = null,
                List<OwnerLifeCycleState>? ownerLifeCycleStates = null,
                List<OwnerResponsibleType>? ownerResponsibleTypes = null,
                bool emptyForUnknownLists = false)
            {
                this.owners = owners ?? [];
                this.relevantImports = relevantImports ?? [];
                this.statisticsByManagementId = statisticsByManagementId ?? [];
                this.commonServices = commonServices ?? [];
                this.ownerConnections = ownerConnections ?? [];
                this.tickets = tickets ?? [];
                this.states = states ?? [];
                this.extStates = extStates ?? [];
                this.ownerLifeCycleStates = ownerLifeCycleStates ?? [];
                this.ownerResponsibleTypes = ownerResponsibleTypes ?? [];
                this.emptyForUnknownLists = emptyForUnknownLists;
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
                if (typeof(QueryResponseType) == typeof(List<WfExtState>) && query == RequestQueries.getExtStates)
                {
                    return Task.FromResult((QueryResponseType)(object)extStates);
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
                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>))
                {
                    // the connection query the connection related reports issue for the selected owner
                    return Task.FromResult((QueryResponseType)(object)ownerConnections);
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
                if (emptyForUnknownLists && typeof(QueryResponseType).IsGenericType
                    && typeof(QueryResponseType).GetGenericTypeDefinition() == typeof(List<>))
                {
                    // the variance analysis pulls in managements, areas and more on its way through.
                    // those are covered by their own tests - here an empty result is enough to let the
                    // generator walk the whole variance branch.
                    return Task.FromResult((QueryResponseType)Activator.CreateInstance(typeof(QueryResponseType))!);
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

        private static readonly List<ModellingConnection> SharedCommonServices =
        [
            new() { Id = 88, Name = "shared-service" }
        ];

        private static readonly List<FwoOwner> SelectedOwnersForConnections =
        [
            new() { Id = 41, Name = "owner-41" },
            new() { Id = 42, Name = "owner-42" }
        ];

        private static readonly List<ModellingConnection> OwnerConnections =
        [
            new() { Id = 501, Name = "regular-connection" },
            new() { Id = 502, Name = "interface-connection", IsInterface = true },
            new() { Id = 503, Name = "common-service-connection", IsCommonService = true }
        ];

        [Test]
        public async Task GenerateFromTemplate_ConnectionsBuildsOwnerDataForEverySelectedOwner()
        {
            ReportTemplate template = BuildTemplate(ReportType.Connections);
            template.ReportParams.ModellingFilter.SelectedOwners = SelectedOwnersForConnections;

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(ownerConnections: OwnerConnections),
                new SimulatedUserConfig(),
                DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.ReportData.OwnerData, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(report.ReportData.OwnerData[0].Name, Is.EqualTo("owner-41"));
                Assert.That(report.ReportData.OwnerData[1].Name, Is.EqualTo("owner-42"));
            });
        }

        [Test]
        public async Task GenerateFromTemplate_VarianceAnalysisRunsTheVarianceBranchPerOwner()
        {
            // the variance branch used to reach for a static field to find the report it was building,
            // so it has to keep working now that the report is handed to it explicitly
            ReportTemplate template = BuildTemplate(ReportType.VarianceAnalysis);
            template.ReportParams.ModellingFilter.SelectedOwners = [SelectedOwnersForConnections[0]];

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(emptyForUnknownLists: true),
                BuildVarianceUserConfig(),
                DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.That(report, Is.InstanceOf<ReportVariances>());
            Assert.That(report!.ReportData.OwnerData, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GenerateFromTemplate_VarianceAnalysisCountsElementsOnTheReportItIsBuilding()
        {
            // with the static field the count landed on whichever report was generated last on the
            // whole server, so two owners in one report had to be counted onto that same report
            ReportTemplate template = BuildTemplate(ReportType.VarianceAnalysis);
            template.ReportParams.ModellingFilter.SelectedOwners = SelectedOwnersForConnections;

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(emptyForUnknownLists: true),
                BuildVarianceUserConfig(),
                DisplayNothing);

            Assert.That(report, Is.Not.Null);
            Assert.That(report!.ReportData.ElementsCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(report.ReportData.OwnerData, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task GenerateFromTemplate_ConnectionsSplitsConnectionsIntoTheirCategories()
        {
            ReportTemplate template = BuildTemplate(ReportType.Connections);
            template.ReportParams.ModellingFilter.SelectedOwners = [SelectedOwnersForConnections[0]];

            ReportBase? report = await ReportGenerator.GenerateFromTemplate(
                template,
                new ReportGeneratorApiConnection(ownerConnections: OwnerConnections),
                new SimulatedUserConfig(),
                DisplayNothing);

            Assert.That(report, Is.Not.Null);
            OwnerConnectionReport ownerData = report!.ReportData.OwnerData[0];
            Assert.Multiple(() =>
            {
                Assert.That(ownerData.RegularConnections, Has.Count.EqualTo(1));
                Assert.That(ownerData.Interfaces, Has.Count.EqualTo(1));
                Assert.That(ownerData.CommonServices, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void GenerateFromTemplate_KeepsNoReportInStaticState()
        {
            // the generator used to park the report in a static field while building a connection
            // related report and never cleared it. that kept the last one generated on the whole
            // server alive for the lifetime of the process, and let two users generating a variance
            // report at the same time write into each other's report.
            List<FieldInfo> reportHoldingStatics = typeof(ReportGenerator)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => typeof(ReportBase).IsAssignableFrom(field.FieldType)
                             || typeof(ReportData).IsAssignableFrom(field.FieldType))
                .ToList();

            Assert.That(reportHoldingStatics, Is.Empty,
                $"ReportGenerator must stay stateless but holds: {string.Join(", ", reportHoldingStatics.Select(field => field.Name))}");
        }

        [Test]
        public async Task GenerateFromTemplate_ConnectionsReportsDoNotShareStateBetweenGenerations()
        {
            ReportTemplate firstTemplate = BuildTemplate(ReportType.Connections);
            ReportTemplate secondTemplate = BuildTemplate(ReportType.Connections);

            ReportBase? firstReport = await ReportGenerator.GenerateFromTemplate(
                firstTemplate,
                new ReportGeneratorApiConnection(commonServices: SharedCommonServices),
                new SimulatedUserConfig(),
                DisplayNothing);

            ReportBase? secondReport = await ReportGenerator.GenerateFromTemplate(
                secondTemplate,
                new ReportGeneratorApiConnection(),
                new SimulatedUserConfig(),
                DisplayNothing);

            Assert.That(firstReport, Is.Not.Null);
            Assert.That(secondReport, Is.Not.Null);
            Assert.That(secondReport, Is.Not.SameAs(firstReport));
            // the second generation must not have inherited the first one's common services
            Assert.That(firstReport!.ReportData.GlobalComSvc, Has.Exactly(1).Items);
            Assert.That(secondReport!.ReportData.GlobalComSvc, Is.Empty);
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

        /// <summary>
        /// The variance analysis deserializes the naming convention out of the user config, so it needs
        /// valid json there rather than the empty default.
        /// </summary>
        private static SimulatedUserConfig BuildVarianceUserConfig()
        {
            return new SimulatedUserConfig { ModNamingConvention = "{}" };
        }

        private static void DisplayNothing(Exception? exception, string title, string message, bool show)
        { }
    }
}
