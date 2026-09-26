using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Verifies that the variance analysis loads production rules page by page (#5301), so a single
    /// response stays bounded by the page size instead of growing with the rulebase.
    /// </summary>
    [TestFixture]
    internal class ModellingVarianceAnalysisRulePagingTest
    {
        private const int kManagementId = 1;
        private const int kPageSize = 2;
        private const int kDefaultRulesPerFetch = 100;
        private static readonly List<int> kThreePageOffsets = [0, 2, 4];
        private static readonly List<int> kTwoPageOffsets = [0, 2];
        private static readonly List<int> kSingleOffset = [0];
        private static readonly List<long> kFiveRuleIds = [1, 2, 3, 4, 5];
        private static readonly List<ModellingConnection> kNoConnections = [];

        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1" };
        private readonly ExtStateHandler extStateHandler = new(new ExtStateTestApiConn());

        private static SimulatedUserConfig CreateUserConfig(int elementsPerFetch, int ownerMappingSource = 0)
        {
            return new()
            {
                ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}",
                RuleRecognitionOption = "{\"nwRegardIp\":true,\"nwSeparateGroupAnalysis\":false}",
                ModModelledMarker = "FWOC",
                ModModelledMarkerLocation = MarkerLocation.Rulename,
                OwnerSoruceMappingID = ownerMappingSource,
                ElementsPerFetch = elementsPerFetch
            };
        }

        [Test]
        public async Task RemainingRulesAreLoadedInPages()
        {
            RulePagingApiConn apiConnection = new(RulePagingApiConn.CreateRules("Rule", kFiveRuleIds.Count));
            ModellingVarianceAnalysis analysis = new(apiConnection, extStateHandler, CreateUserConfig(kPageSize), Application, DefaultInit.DoNothing);

            ModellingVarianceResult result = await analysis.AnalyseRulesVsModelledConnections(kNoConnections, new() { AnalyseRemainingRules = true }, false);

            Assert.That(apiConnection.OffsetsFor(RuleQueries.getRulesByManagement), Is.EqualTo(kThreePageOffsets));
            Assert.That(apiConnection.LimitsFor(RuleQueries.getRulesByManagement), Is.All.EqualTo(kPageSize));
            Assert.That(result.UnModelledRules[kManagementId].Select(rule => rule.Id), Is.EqualTo(kFiveRuleIds));
        }

        [Test]
        public async Task ExactMultipleOfPageSizeEndsOnEmptyPage()
        {
            RulePagingApiConn apiConnection = new(RulePagingApiConn.CreateRules("Rule", 4));
            ModellingVarianceAnalysis analysis = new(apiConnection, extStateHandler, CreateUserConfig(kPageSize), Application, DefaultInit.DoNothing);

            ModellingVarianceResult result = await analysis.AnalyseRulesVsModelledConnections(kNoConnections, new() { AnalyseRemainingRules = true }, false);

            Assert.That(apiConnection.OffsetsFor(RuleQueries.getRulesByManagement), Is.EqualTo(kThreePageOffsets));
            Assert.That(result.UnModelledRules[kManagementId], Has.Count.EqualTo(4));
        }

        [Test]
        public async Task MarkerQueryIsLoadedInPages()
        {
            RulePagingApiConn apiConnection = new(RulePagingApiConn.CreateRules("FWOC", 3));
            ModellingVarianceAnalysis analysis = new(apiConnection, extStateHandler, CreateUserConfig(kPageSize), Application, DefaultInit.DoNothing);

            await analysis.AnalyseRulesVsModelledConnections(kNoConnections, new(), false);

            Assert.That(apiConnection.OffsetsFor(RuleQueries.getModelledRulesByManagementName), Is.EqualTo(kTwoPageOffsets));
            Assert.That(apiConnection.LimitsFor(RuleQueries.getModelledRulesByManagementName), Is.All.EqualTo(kPageSize));
        }

        [Test]
        public async Task CommentMarkerQueryIsLoadedInPages()
        {
            SimulatedUserConfig config = CreateUserConfig(kPageSize);
            config.ModModelledMarkerLocation = MarkerLocation.Comment;
            RulePagingApiConn apiConnection = new(RulePagingApiConn.CreateRules("FWOC", 3));
            ModellingVarianceAnalysis analysis = new(apiConnection, extStateHandler, config, Application, DefaultInit.DoNothing);

            await analysis.AnalyseRulesVsModelledConnections(kNoConnections, new(), false);

            Assert.That(apiConnection.OffsetsFor(RuleQueries.getModelledRulesByManagementComment), Is.EqualTo(kTwoPageOffsets));
        }

        [Test]
        public async Task NameFieldPreFilterQueryIsLoadedInPages()
        {
            RulePagingApiConn apiConnection = new(RulePagingApiConn.CreateRules("FWOC", 3));
            SimulatedUserConfig config = CreateUserConfig(kPageSize, (int)OwnerMappingSourceStm.NameField);
            ModellingVarianceAnalysis analysis = new(apiConnection, extStateHandler, config, Application, DefaultInit.DoNothing);

            await analysis.AnalyseRulesVsModelledConnections(kNoConnections, new(), false);

            Assert.That(apiConnection.OffsetsFor(RuleQueries.getModelledRulesByRuleOwnerNameField), Is.EqualTo(kTwoPageOffsets));
            Assert.That(apiConnection.OffsetsFor(RuleQueries.getModelledRulesByManagementName), Is.Empty);
        }

        [Test]
        public async Task NonPositiveElementsPerFetchFallsBackToDefaultPageSize()
        {
            RulePagingApiConn apiConnection = new(RulePagingApiConn.CreateRules("FWOC", 3));
            ModellingVarianceAnalysis analysis = new(apiConnection, extStateHandler, CreateUserConfig(0), Application, DefaultInit.DoNothing);

            await analysis.AnalyseRulesVsModelledConnections(kNoConnections, new(), false);

            Assert.That(apiConnection.OffsetsFor(RuleQueries.getModelledRulesByManagementName), Is.EqualTo(kSingleOffset));
            Assert.That(apiConnection.LimitsFor(RuleQueries.getModelledRulesByManagementName), Is.All.EqualTo(kDefaultRulesPerFetch));
        }

        /// <summary>
        /// Serves one management and a fixed rule pool, honouring limit and offset of the rule queries
        /// like Hasura does, and records every requested page.
        /// </summary>
        private sealed class RulePagingApiConn(List<Rule> rulePool) : SimulatedApiConnection
        {
            private readonly List<(string Query, int Limit, int Offset)> pageRequests = [];

            public static List<Rule> CreateRules(string namePrefix, int count)
            {
                return [.. Enumerable.Range(1, count).Select(id => new Rule { Id = id, Name = $"{namePrefix}{id}", MgmtId = kManagementId })];
            }

            public List<int> OffsetsFor(string query)
            {
                return [.. pageRequests.Where(request => request.Query == query).Select(request => request.Offset)];
            }

            public List<int> LimitsFor(string query)
            {
                return [.. pageRequests.Where(request => request.Query == query).Select(request => request.Limit)];
            }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
                string query,
                object? variables = null,
                string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                await DefaultInit.DoNothing();
                Type responseType = typeof(QueryResponseType);

                if (responseType == typeof(List<Management>))
                {
                    List<Management> managements = [new() { Id = kManagementId, Name = "Mgmt1", ExtMgtData = "{\"id\":\"1\",\"name\":\"Ext1\"}" }];
                    return (QueryResponseType)(object)managements;
                }

                if (responseType == typeof(List<ManagementReport>) && query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    List<ManagementReport> reports =
                    [
                        new() { Id = kManagementId, Import = new() { ImportAggregate = new() { ImportAggregateMax = new() { RelevantImportId = 1 } } } }
                    ];
                    return (QueryResponseType)(object)reports;
                }

                if (responseType == typeof(List<RuleOwner>))
                {
                    List<RuleOwner> mapping = [new() { RuleId = 1 }];
                    return (QueryResponseType)(object)mapping;
                }

                if (responseType == typeof(List<Rule>))
                {
                    return (QueryResponseType)(object)GetRulePage(query, (Dictionary<string, object?>)variables!);
                }

                if (responseType == typeof(ReturnIdWrapper))
                {
                    return (QueryResponseType)(object)new ReturnIdWrapper();
                }

                // every other list the analysis reads is empty in this scenario
                return (QueryResponseType)Activator.CreateInstance(responseType)!;
            }

            private List<Rule> GetRulePage(string query, Dictionary<string, object?> variables)
            {
                int limit = (int)variables["limit"]!;
                int offset = (int)variables["offset"]!;
                pageRequests.Add((query, limit, offset));
                return [.. rulePool.Skip(offset).Take(limit)];
            }
        }
    }
}
