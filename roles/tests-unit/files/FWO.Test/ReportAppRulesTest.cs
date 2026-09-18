using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class ReportAppRulesTest
    {
        [Test]
        [Parallelizable]
        public async Task PrepareQueryBeforeFetchAppliesNameFieldRuleOwnerPrefilter()
        {
            (TestReportAppRules report, AppRulesPrefilterApiConnection apiConnection) = CreateReport();

            await report.CallPrepareQueryBeforeFetch(CreateManagements(), apiConnection);

            StringAssert.Contains("rule_name: { _ilike: $appRulesRuleOwnerPrefilterMarker }", report.Query.FullQuery);
            StringAssert.Contains("rule_metadatum: { rule_owners: { owner_id: { _eq: 17 }, owner_mapping_source_id: { _eq: 3 }, removed: { _is_null: true } } }", report.Query.FullQuery);
            Assert.That(apiConnection.Queries, Does.Contain(ImportQueries.getPendingRuleOwnerImports));
            Assert.That(apiConnection.Queries, Does.Contain(ModellingQueries.getOwnersForRuleOwnerNameFieldFilteredByOwner));
            Assert.That(apiConnection.Queries, Does.Contain(RuleQueries.getNameFieldRuleOwnerPreFilterCompletenessRules));
        }

        [Test]
        [Parallelizable]
        public async Task PrepareQueryBeforeFetchSkipsPrefilterWhenNameFieldMappingIsNotActive()
        {
            UserConfig userConfig = CreateUserConfig();
            userConfig.OwnerSoruceMappingID = 0;
            (TestReportAppRules report, AppRulesPrefilterApiConnection apiConnection) = CreateReport(userConfig);

            await report.CallPrepareQueryBeforeFetch(CreateManagements(), apiConnection);

            StringAssert.DoesNotContain("rule_metadatum: { rule_owners:", report.Query.FullQuery);
            Assert.That(apiConnection.Queries, Is.Empty);
        }

        [Test]
        [Parallelizable]
        public async Task PrepareQueryBeforeFetchSkipsPrefilterWhenRuleOwnerMappingImportIsPending()
        {
            AppRulesPrefilterApiConnection apiConnection = new()
            {
                PendingImports = new List<ImportControl>
                {
                    new() { MgmId = 10 }
                }
            };
            (TestReportAppRules report, _) = CreateReport(apiConnection: apiConnection);

            await report.CallPrepareQueryBeforeFetch(CreateManagements(), apiConnection);

            StringAssert.DoesNotContain("rule_metadatum: { rule_owners:", report.Query.FullQuery);
            Assert.That(apiConnection.Queries, Does.Contain(ImportQueries.getPendingRuleOwnerImports));
            Assert.That(apiConnection.Queries, Does.Not.Contain(RuleQueries.getNameFieldRuleOwnerPreFilterCompletenessRules));
        }

        [Test]
        [Parallelizable]
        public async Task PrepareQueryBeforeFetchSkipsPrefilterWhenCompletenessCheckFindsMissingMapping()
        {
            AppRulesPrefilterApiConnection apiConnection = new()
            {
                OwnerConnections = new List<ModellingConnection>
                {
                    new() { Id = 42 }
                },
                MarkerRulesMissingMappings = new List<Rule>
                {
                    new() { Name = "FWOC42" }
                }
            };
            (TestReportAppRules report, _) = CreateReport(apiConnection: apiConnection);

            await report.CallPrepareQueryBeforeFetch(CreateManagements(), apiConnection);

            StringAssert.DoesNotContain("rule_metadatum: { rule_owners:", report.Query.FullQuery);
            Assert.That(apiConnection.Queries, Does.Contain(ModellingQueries.getOwnersForRuleOwnerNameFieldFilteredByOwner));
            Assert.That(apiConnection.Queries, Does.Contain(RuleQueries.getNameFieldRuleOwnerPreFilterCompletenessRules));
        }

        private static (TestReportAppRules report, AppRulesPrefilterApiConnection apiConnection) CreateReport(
            UserConfig? userConfig = null, AppRulesPrefilterApiConnection? apiConnection = null)
        {
            ReportTemplate template = new("", new()
            {
                ReportType = (int)ReportType.AppRules
            });
            template.ReportParams.ModellingFilter.SelectedOwner = new FwoOwner { Id = 17 };
            DynGraphqlQuery query = Compiler.Compile(template);
            TestReportAppRules report = new(query, userConfig ?? CreateUserConfig(), ReportType.AppRules,
                template.ReportParams.ModellingFilter, template);
            return (report, apiConnection ?? new AppRulesPrefilterApiConnection());
        }

        private static UserConfig CreateUserConfig()
        {
            return new()
            {
                OwnerSoruceMappingID = (int)OwnerMappingSourceStm.NameField,
                ModModelledMarker = "FWOC",
                ModModelledMarkerLocation = MarkerLocation.Rulename
            };
        }

        private static List<ManagementReport> CreateManagements()
        {
            return new List<ManagementReport>
            {
                new()
                {
                    Id = 10,
                    RelevantImportId = 100
                }
            };
        }

        private sealed class TestReportAppRules : ReportAppRules
        {
            public TestReportAppRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType,
                ModellingFilter modellingFilter, ReportTemplate reportTemplate) : base(query, userConfig, reportType, modellingFilter, reportTemplate)
            {
            }

            public Task CallPrepareQueryBeforeFetch(List<ManagementReport> managementsWithRelevantImportId, ApiConnection apiConnection)
            {
                return PrepareQueryBeforeFetch(managementsWithRelevantImportId, apiConnection);
            }
        }

        private sealed class AppRulesPrefilterApiConnection : ApiConnection
        {
            public List<string> Queries { get; } = new();
            public List<ImportControl> PendingImports { get; set; } = new();
            public List<ModellingConnection> OwnerConnections { get; set; } = new();
            public List<Rule> MarkerRulesMissingMappings { get; set; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (query == ImportQueries.getPendingRuleOwnerImports)
                {
                    return Task.FromResult((QueryResponseType)(object)PendingImports);
                }
                if (query == ModellingQueries.getOwnersForRuleOwnerNameFieldFilteredByOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)OwnerConnections);
                }
                if (query == RuleQueries.getNameFieldRuleOwnerPreFilterCompletenessRules)
                {
                    return Task.FromResult((QueryResponseType)(object)MarkerRulesMissingMappings);
                }
                throw new NotSupportedException(query);
            }

            public override void SetAuthHeader(string jwt)
            {
            }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override void SetRole(string role)
            {
            }

            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
            {
            }

            public override void SwitchBack()
            {
            }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null)
            {
                throw new NotSupportedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription,
                object? variables = null, string? operationName = null)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
            }

            public override void DisposeSubscriptions<T>()
            {
            }
        }
    }
}
