using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Services.RuleTreeBuilder;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using AppServiceProvider = FWO.Services.ServiceProvider;

namespace FWO.Test
{
    /// <summary>
    /// Covers the generation of rule reports that are not built from the standard rule queries, the fallbacks for
    /// building the rule tree, the object fetching for exports, and descriptions and unsupported exports.
    /// Not parallelizable: some tests replace the process wide service provider.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ReportRulesGenerationTest
    {
        private const string kFullQuery = "legacy-full-query";
        private const string kStructureQuery = "standard-rules-structure-query $import_id_start $import_id_end";
        private const string kPageQuery = "standard-rules-page-query";
        private const int kManagementId = 1;
        private const int kGatewayId = 11;
        private const int kRulebaseId = 10;
        private const int kElementsPerFetch = 2;
        private const long kRelevantImportId = 77;

        private readonly SimulatedUserConfig userConfig = new();
        private IServiceProvider? previousServices;

        [SetUp]
        public void RememberServiceProvider()
        {
            previousServices = AppServiceProvider.Services;
        }

        [TearDown]
        public void RestoreServiceProvider()
        {
            AppServiceProvider.Services = previousServices;
        }

        /// <summary>
        /// Answers the import id query with one management, the full rules query with pages of the given sizes,
        /// and the standard structure query with no management at all.
        /// </summary>
        private sealed class RulesPagingApiConnection(params int[] rulesPerPage) : SimulatedApiConnection
        {
            public List<int> FetchedOffsets { get; } = [];
            public int StructureQueries { get; private set; }
            public int PageQueries { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    ManagementReport management = new() { Id = kManagementId, Name = "management" };
                    management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = kRelevantImportId;
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { management });
                }
                if (query == kFullQuery && variables is Dictionary<string, object> queryVariables)
                {
                    int offset = (int)queryVariables[QueryVar.Offset];
                    FetchedOffsets.Add(offset);
                    int page = offset / kElementsPerFetch;
                    int ruleCount = page < rulesPerPage.Length ? rulesPerPage[page] : 0;
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { BuildPage(offset, ruleCount) });
                }
                if (query == kStructureQuery)
                {
                    StructureQueries++;
                    return Task.FromResult((QueryResponseType)(object)new List<ManagementReport>());
                }
                if (query == kPageQuery)
                {
                    PageQueries++;
                    return Task.FromResult((QueryResponseType)(object)new List<Rule>());
                }
                throw new NotImplementedException(query);
            }

            private static ManagementReport BuildPage(int offset, int ruleCount)
            {
                return new ManagementReport
                {
                    Id = kManagementId,
                    Name = "management",
                    Devices =
                    [
                        new DeviceReport
                        {
                            Id = kGatewayId,
                            Name = "gateway",
                            RulebaseLinks = [new RulebaseLink { GatewayId = kGatewayId, NextRulebaseId = kRulebaseId, LinkType = 2, IsInitial = true }]
                        }
                    ],
                    Rulebases =
                    [
                        new RulebaseReport
                        {
                            Id = kRulebaseId,
                            Name = "rulebase",
                            Rules = [.. Enumerable.Range(0, ruleCount).Select(index => new Rule { Id = offset + index + 1, RulebaseId = kRulebaseId, Name = $"rule {offset + index + 1}" })]
                        }
                    ]
                };
            }
        }

        /// <summary>
        /// Records the object queries of an export and answers them with one object of each kind.
        /// </summary>
        private sealed class ObjectQueryApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                ManagementReport objects = new()
                {
                    ReportObjects = [new NetworkObject { Id = 1 }],
                    ReportServices = [new NetworkService { Id = 2 }],
                    ReportUsers = [new NetworkUser { Id = 3 }]
                };
                return Task.FromResult((QueryResponseType)(object)new List<ManagementReport> { objects });
            }
        }

        private ReportRules CreateLegacyReport(ReportType reportType = ReportType.ResolvedRules, IRuleTreeBuilder? ruleTreeBuilder = null)
        {
            DynGraphqlQuery query = new("") { FullQuery = kFullQuery, RelevantManagementIds = [kManagementId] };
            return new ReportRules(query, userConfig, reportType, ruleTreeBuilder ?? new RuleTreeBuilder());
        }

        private static ManagementReport ManagementWithImport(int id, long? relevantImportId)
        {
            ManagementReport management = new() { Id = id, Name = $"management {id}" };
            management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = relevantImportId;
            return management;
        }

        [Test]
        public async Task ARulesReportWithoutStandardQueriesPagesThroughTheFullQuery()
        {
            RulesPagingApiConnection apiConnection = new(kElementsPerFetch, kElementsPerFetch, 1);
            RuleTreeBuilder ruleTreeBuilder = new();
            ReportRules report = CreateLegacyReport(ruleTreeBuilder: ruleTreeBuilder);
            int callbacks = 0;

            await report.Generate(kElementsPerFetch, apiConnection, _ => { callbacks++; return Task.CompletedTask; }, CancellationToken.None);

            ManagementReport management = report.ReportData.ManagementData.Single();
            Assert.That(apiConnection.FetchedOffsets, Is.EqualTo((List<int>)[0, 2, 4]));
            Assert.That(management.Rulebases.Single().Rules.Select(rule => rule.Id), Is.EqualTo((List<long>)[1, 2, 3, 4, 5]));
            Assert.That(management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId, Is.EqualTo(kRelevantImportId));
            Assert.That(callbacks, Is.EqualTo(2));
            Assert.That(report.ReportData.ElementsCount, Is.EqualTo(5));
            Assert.That(ruleTreeBuilder.RuleTreeCache.ContainsKey((kManagementId, kGatewayId)), Is.True);
        }

        [Test]
        public async Task ARulebaseReportIsNotBuiltAsRuleTree()
        {
            RuleTreeBuilder ruleTreeBuilder = new();
            ReportRules report = CreateLegacyReport(ReportType.Recertification, ruleTreeBuilder);

            await report.Generate(kElementsPerFetch, new RulesPagingApiConnection(1), _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.ReportData.ManagementData.Single().Rulebases.Single().Rules, Has.Length.EqualTo(1));
            Assert.That(ruleTreeBuilder.RuleTreeCache, Is.Empty);
        }

        [Test]
        public void ACancelledGenerationStopsBeforePaging()
        {
            RulesPagingApiConnection apiConnection = new(kElementsPerFetch, kElementsPerFetch);
            ReportRules report = CreateLegacyReport();
            using CancellationTokenSource tokenSource = new();
            tokenSource.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() => report.Generate(kElementsPerFetch, apiConnection, _ => Task.CompletedTask, tokenSource.Token));
            Assert.That(apiConnection.FetchedOffsets, Is.EqualTo((List<int>)[0]));
        }

        [Test]
        public async Task AManagementWithoutStructureIsSkippedInTheStandardRulesReport()
        {
            DynGraphqlQuery query = new("")
            {
                FullQuery = kFullQuery,
                StandardRulesStructureQuery = kStructureQuery,
                StandardRulesPageQuery = kPageQuery,
                RelevantManagementIds = [kManagementId]
            };
            ReportRules report = new(query, userConfig, ReportType.Rules, new RuleTreeBuilder());
            RulesPagingApiConnection apiConnection = new();

            await report.Generate(kElementsPerFetch, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.ReportData.ManagementData, Is.Empty);
            Assert.That(apiConnection.StructureQueries, Is.EqualTo(1));
            Assert.That(apiConnection.PageQueries, Is.Zero);
            Assert.That(apiConnection.FetchedOffsets, Is.Empty);
        }

        [Test]
        public async Task WithoutAnyRuleTreeBuilderTheRuleTreeIsSkipped()
        {
            AppServiceProvider.Services = null;
            DynGraphqlQuery query = new("") { FullQuery = kFullQuery, RelevantManagementIds = [kManagementId] };
            ReportRules report = new(query, userConfig, ReportType.Rules);

            await report.Generate(kElementsPerFetch, new RulesPagingApiConnection(1), _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.ReportData.ManagementData.Single().Rulebases.Single().Rules, Has.Length.EqualTo(1));
            Assert.That(report.ReportData.ElementsCount, Is.Zero);
        }

        [Test]
        public async Task AServiceProviderWithoutRuleTreeBuilderSkipsTheRuleTree()
        {
            AppServiceProvider.Services = new ServiceCollection().BuildServiceProvider();
            DynGraphqlQuery query = new("") { FullQuery = kFullQuery, RelevantManagementIds = [kManagementId] };
            ReportRules report = new(query, userConfig, ReportType.Rules);

            await report.Generate(kElementsPerFetch, new RulesPagingApiConnection(1), _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.ReportData.ElementsCount, Is.Zero);
        }

        [Test]
        public async Task TheRuleTreeBuilderOfTheServiceProviderIsUsedWhenTheCallerHasNone()
        {
            RuleTreeBuilder registeredBuilder = new();
            ServiceCollection services = new();
            services.AddSingleton<IRuleTreeBuilder>(registeredBuilder);
            AppServiceProvider.Services = services.BuildServiceProvider();
            DynGraphqlQuery query = new("") { FullQuery = kFullQuery, RelevantManagementIds = [kManagementId] };
            ReportRules report = new(query, userConfig, ReportType.Rules);

            await report.Generate(kElementsPerFetch, new RulesPagingApiConnection(1), _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(report.ReportData.ElementsCount, Is.EqualTo(1));
            Assert.That(registeredBuilder.RuleTreeCache.ContainsKey((kManagementId, kGatewayId)), Is.True);
        }

        [Test]
        public async Task TheObjectsOfAnExportAreFetchedOnceForManagementsWithAnImport()
        {
            ReportRules report = CreateLegacyReport();
            report.ReportData.ManagementData = [ManagementWithImport(kManagementId, kRelevantImportId), ManagementWithImport(2, null)];
            ObjectQueryApiConnection apiConnection = new();

            bool gotAllObjects = await report.GetObjectsInReport(10, apiConnection, _ => Task.CompletedTask);
            await report.GetObjectsInReport(10, apiConnection, _ => Task.CompletedTask);

            Assert.That(gotAllObjects, Is.True);
            Assert.That(report.GotObjectsInReport, Is.True);
            Assert.That(apiConnection.Queries, Is.EqualTo((List<string>)[ObjectQueries.getReportFilteredObjectDetails]));
            Assert.That(report.ReportData.ManagementData[0].ReportObjects, Has.Length.EqualTo(1));
            Assert.That(report.ReportData.ManagementData[1].ReportObjects, Is.Empty);
        }

        [TestCase(ObjCategory.nsrv)]
        [TestCase(ObjCategory.user)]
        public async Task EachObjectCategoryUsesItsOwnQueryAndFillsOnlyItsObjects(ObjCategory category)
        {
            ReportRules report = CreateLegacyReport();
            report.ReportData.ManagementData = [ManagementWithImport(kManagementId, kRelevantImportId)];
            ObjectQueryApiConnection apiConnection = new();
            Dictionary<string, object> variables = new() { [QueryVar.MgmIds] = kManagementId, [QueryVar.Limit] = 10, [QueryVar.Offset] = 0 };

            await report.GetObjectsForManagementInReport(variables, category, 1, apiConnection, _ => Task.CompletedTask);

            ManagementReport management = report.ReportData.ManagementData.Single();
            string expectedQuery = category == ObjCategory.nsrv ? ObjectQueries.getReportFilteredNetworkServiceDetails : ObjectQueries.getReportFilteredUserDetails;
            Assert.That(apiConnection.Queries, Is.EqualTo((List<string>)[expectedQuery]));
            Assert.That(management.ReportObjects, Is.Empty);
            Assert.That(management.ReportServices.Length + management.ReportUsers.Length, Is.EqualTo(1));
        }

        [Test]
        public void ObjectsCanOnlyBeFetchedWithManagementLimitAndOffset()
        {
            ReportRules report = CreateLegacyReport();

            Assert.ThrowsAsync<ArgumentException>(() => report.GetObjectsForManagementInReport(new Dictionary<string, object> { [QueryVar.MgmIds] = kManagementId },
                ObjCategory.all, 1, new ObjectQueryApiConnection(), _ => Task.CompletedTask));
        }

        [Test]
        public void TheRulesOfAnUnknownRulebaseAreEmpty()
        {
            ManagementReport management = new() { Rulebases = [new RulebaseReport { Id = kRulebaseId, Rules = [new Rule { Id = 1 }] }] };

            Assert.That(ReportRules.GetRulesByRulebaseId(99, management), Is.Empty);
            Assert.That(ReportRules.GetRulesByRulebaseId(kRulebaseId, management), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task TheDescriptionCountsManagementsGatewaysAndRulesOfTheGatewayView()
        {
            RulesPagingApiConnection apiConnection = new(kElementsPerFetch, 1);
            ReportRules report = CreateLegacyReport(ReportType.Rules);
            await report.Generate(kElementsPerFetch, apiConnection, _ => Task.CompletedTask, CancellationToken.None);
            report.ReportData.ManagementData.Add(new ManagementReport { Id = 2, Ignore = true });

            Assert.That(report.SetDescription(),
                Is.EqualTo($"1 {userConfig.GetText("managements")}, 1 {userConfig.GetText("gateways")}, 3 {userConfig.GetText("rules")}"));
        }

        [Test]
        public void TheDescriptionOfTheManagementRulebasesViewCountsManagementsWithRules()
        {
            DynGraphqlQuery query = new("") { ManagementRulebaseView = true };
            ReportRules report = new(query, userConfig, ReportType.Rules, new RuleTreeBuilder());
            report.ReportData.ManagementData =
            [
                new ManagementReport { Id = 1, Rulebases = [new RulebaseReport { Rules = [new Rule(), new Rule()] }, new RulebaseReport { Rules = [new Rule()] }] },
                new ManagementReport { Id = 2, Rulebases = [new RulebaseReport { Rules = [] }] },
                new ManagementReport { Id = 3, Ignore = true, Rulebases = [new RulebaseReport { Rules = [new Rule()] }] }
            ];

            Assert.That(report.SetDescription(), Is.EqualTo($"1 {userConfig.GetText("managements")}, 3 {userConfig.GetText("rules")}"));
        }

        [Test]
        public void OnlyResolvedRulesReportsCanBeExportedAsCsv()
        {
            ReportRules report = CreateLegacyReport(ReportType.Rules);

            Assert.Throws<NotImplementedException>(() => report.ExportToCsv());
        }
    }
}
