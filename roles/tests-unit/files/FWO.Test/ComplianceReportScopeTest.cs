using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Responses;
using FWO.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.Reflection;
using System.Security.Cryptography;

namespace FWO.Test
{
    /// <summary>
    /// Verifies that the compliance report never leaves the requested and visible management scope (SEC-22).
    /// </summary>
    [TestFixture]
    internal class ComplianceReportScopeTest
    {
        private const int kManagementA = 1;
        private const int kManagementB = 2;
        private const int kManagementC = 3;
        private static readonly List<int> kNoManagements = [];
        private static readonly List<int> kOnlyA = [kManagementA];
        private static readonly List<int> kOnlyB = [kManagementB];
        private static readonly List<int> kDuplicateA = [kManagementA, kManagementA];
        private static readonly List<int> kAAndB = [kManagementA, kManagementB];
        private static readonly List<int> kAAndC = [kManagementA, kManagementC];
        private static readonly List<int> kUnknownAndB = [99, kManagementB];
        private static readonly List<int> kAllManagements = [kManagementA, kManagementB, kManagementC];
        private static readonly List<string> kGlobalComplianceRoles = [Roles.Admin, Roles.Auditor];
        private static readonly List<Ldap> kNoLdaps = [];

        [Test]
        public async Task GenerateReport_EmptyScope_OnlyQueriesVisibleManagements()
        {
            ScopedComplianceApiConnection apiConnection = new(kOnlyA);

            ActionResult<string> result = await CreateController().GenerateReport(kNoManagements, apiConnection, CreateUserConfig(), CancellationToken.None);

            Assert.That(result.Result, Is.Null);
            AssertOnlyManagementQueried(apiConnection, kOnlyA);
        }

        [Test]
        public async Task GenerateReport_VisibleManagementRequested_OnlyQueriesThatManagement()
        {
            ScopedComplianceApiConnection apiConnection = new(kOnlyA);

            ActionResult<string> result = await CreateController().GenerateReport(kOnlyA, apiConnection, CreateUserConfig(), CancellationToken.None);

            Assert.That(result.Result, Is.Null);
            AssertOnlyManagementQueried(apiConnection, kOnlyA);
        }

        [Test]
        public async Task GenerateReport_DuplicateIds_AreIgnored()
        {
            ScopedComplianceApiConnection apiConnection = new(kOnlyA);

            ActionResult<string> result = await CreateController().GenerateReport(kDuplicateA, apiConnection, CreateUserConfig(), CancellationToken.None);

            Assert.That(result.Result, Is.Null);
            AssertOnlyManagementQueried(apiConnection, kOnlyA);
        }

        [Test]
        public async Task GenerateReport_InvisibleManagementRequested_IsRejected()
        {
            ScopedComplianceApiConnection apiConnection = new(kOnlyA);

            ActionResult<string> result = await CreateController().GenerateReport(kOnlyB, apiConnection, CreateUserConfig(), CancellationToken.None);

            AssertRejected(result, apiConnection, "managementIds[0]");
        }

        [Test]
        public async Task GenerateReport_MixedVisibleAndInvisibleManagements_IsRejected()
        {
            ScopedComplianceApiConnection apiConnection = new(kOnlyA);

            ActionResult<string> result = await CreateController().GenerateReport(kAAndB, apiConnection, CreateUserConfig(), CancellationToken.None);

            AssertRejected(result, apiConnection, "managementIds[1]");
        }

        [Test]
        public async Task GenerateReport_ReportsEveryInaccessibleManagement()
        {
            ScopedComplianceApiConnection apiConnection = new(kOnlyA);

            ActionResult<string> result = await CreateController().GenerateReport(kUnknownAndB, apiConnection, CreateUserConfig(), CancellationToken.None);

            AssertRejected(result, apiConnection, "managementIds[0]", "managementIds[1]");
        }

        [Test]
        public async Task GenerateReport_GlobalScope_SelectsRequestedSubset()
        {
            ScopedComplianceApiConnection apiConnection = new(kAllManagements);

            ActionResult<string> result = await CreateController().GenerateReport(kAAndC, apiConnection, CreateUserConfig(), CancellationToken.None);

            Assert.That(result.Result, Is.Null);
            Assert.That(apiConnection.QueriedManagementIds.SelectMany(ids => ids).Distinct(), Is.EquivalentTo(kAAndC));
        }

        [Test]
        public async Task GenerateReport_ManagementNotRelevantForCompliance_IsRejected()
        {
            ScopedComplianceApiConnection apiConnection = new(kAAndB);

            ActionResult<string> result = await CreateController().GenerateReport(kOnlyB, apiConnection,
                CreateUserConfig(kManagementA.ToString()), CancellationToken.None);

            AssertRejected(result, apiConnection, "managementIds[0]");
        }

        [Test]
        public async Task Generate_RelevantManagementsConfigured_DoesNotWidenVisibleScope()
        {
            ScopedComplianceApiConnection apiConnection = new(kOnlyA);
            ReportCompliance report = new(new(""), CreateUserConfig($"{kManagementA},{kManagementB}"), ReportType.ComplianceReport);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            AssertOnlyManagementQueried(apiConnection, kOnlyA);
            Assert.That(report.Rules.Select(rule => rule.MgmtId), Is.All.EqualTo(kManagementA));
        }

        [Test]
        public async Task Generate_NoVisibleManagement_QueriesNoManagement()
        {
            ScopedComplianceApiConnection apiConnection = new(kNoManagements);
            ReportCompliance report = new(new(""), CreateUserConfig(kManagementB.ToString()), ReportType.ComplianceReport);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(apiConnection.QueriedManagementIds, Is.Not.Empty);
            Assert.That(apiConnection.QueriedManagementIds, Is.All.Empty);
            Assert.That(report.Rules, Is.Empty);
        }

        [Test]
        public void Get_IsRestrictedToGlobalComplianceRoles()
        {
            MethodInfo method = typeof(ComplianceController).GetMethod(nameof(ComplianceController.Get))!;
            AuthorizeAttribute authorize = method.GetCustomAttribute<AuthorizeAttribute>()!;

            List<string> roles = [.. authorize.Roles!.Split(',').Select(role => role.Trim())];

            Assert.That(roles, Is.EquivalentTo(kGlobalComplianceRoles));
        }

        private static void AssertOnlyManagementQueried(ScopedComplianceApiConnection apiConnection, List<int> expectedManagementIds)
        {
            Assert.That(apiConnection.QueriedManagementIds, Is.Not.Empty);
            Assert.That(apiConnection.QueriedManagementIds, Has.All.EqualTo(expectedManagementIds));
        }

        private static void AssertRejected(ActionResult<string> result, ScopedComplianceApiConnection apiConnection, params string[] expectedPaths)
        {
            Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
            RequestValidationErrorResponse errors = (RequestValidationErrorResponse)((BadRequestObjectResult)result.Result!).Value!;
            Assert.That(errors.Errors.Select(error => error.Path), Is.EqualTo(expectedPaths));
            Assert.That(apiConnection.QueriedManagementIds, Is.Empty, "no rule must be fetched for a rejected scope");
        }

        private static ComplianceController CreateController()
        {
            return new ComplianceController(new ScopedComplianceApiConnection(kNoManagements), new JwtWriter(new RsaSecurityKey(RSA.Create(2048))), kNoLdaps);
        }

        private static UserConfig CreateUserConfig(string relevantManagements = "")
        {
            return UserConfig.ForTextOnly(new SimulatedGlobalConfig { ComplianceCheckRelevantManagements = relevantManagements });
        }

        /// <summary>
        /// Emulates the api for a caller who sees only the given managements. The rule queries deliberately ignore
        /// visibility, so any rule outside the visible scope can only be excluded by the requested management ids.
        /// </summary>
        private sealed class ScopedComplianceApiConnection(List<int> visibleManagementIds) : SimulatedApiConnection
        {
            public List<List<int>> QueriedManagementIds { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == DeviceQueries.getManagementNames)
                {
                    List<Management> managements = [.. visibleManagementIds.Select(id => new Management { Id = id, Name = $"mgmt-{id}", Uid = $"uid-{id}" })];
                    return Task.FromResult((QueryResponseType)(object)managements);
                }
                if (query == RuleQueries.countActiveRules)
                {
                    List<int> managementIds = (List<int>)variables!.GetType().GetProperty("mgm_ids")!.GetValue(variables)!;
                    QueriedManagementIds.Add(managementIds);
                    return Task.FromResult((QueryResponseType)(object)new AggregateCount { Aggregate = new Aggregate { Count = GetRules(managementIds).Count } });
                }
                if (query == RuleQueries.getRulesWithCurrentViolationsByChunk)
                {
                    List<int> managementIds = (List<int>)((Dictionary<string, object>)variables!)["mgm_ids"];
                    QueriedManagementIds.Add(managementIds);
                    return Task.FromResult((QueryResponseType)(object)GetRules(managementIds));
                }
                throw new NotSupportedException($"Unexpected query: {query}");
            }

            private static List<Rule> GetRules(List<int> managementIds)
            {
                return [.. kAllManagements.Where(managementIds.Contains)
                    .Select(id => new Rule { Id = id, Uid = $"marker-rule-{id}", Name = $"marker-rule-{id}", MgmtId = id })];
            }
        }
    }
}
