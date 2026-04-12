using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class OwnerActiveRuleCheckTest
    {
        [Test]
        public async Task CheckActiveRulesSync_ReturnsZero_WhenOwnerIdIsInvalid()
        {
            OwnerActiveRuleCheckApiConn apiConnection = new();
            OwnerActiveRuleCheck service = new(apiConnection, new GlobalConfig());

            int sentNotifications = await service.CheckActiveRulesSync(new FwoOwner { Id = 0, Name = "Owner A", ExtAppId = "APP-0" });

            Assert.That(sentNotifications, Is.EqualTo(0));
            Assert.That(apiConnection.ActiveRuleQueryCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task CheckActiveRulesSync_ReturnsZero_WhenNoActiveRulesExist()
        {
            OwnerActiveRuleCheckApiConn apiConnection = new();
            OwnerActiveRuleCheck service = new(apiConnection, new GlobalConfig());

            int sentNotifications = await service.CheckActiveRulesSync(new FwoOwner { Id = 7, Name = "Owner A", ExtAppId = "APP-7" });

            Assert.That(sentNotifications, Is.EqualTo(0));
            Assert.That(apiConnection.ActiveRuleQueryCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task CheckActiveRules_ReturnsZero_WhenOwnerHasNoDecommDate()
        {
            OwnerActiveRuleCheckApiConn apiConnection = new();
            apiConnection.Owners =
            [
                new FwoOwner { Id = 7, Name = "Owner A", ExtAppId = "APP-7" }
            ];
            OwnerActiveRuleCheck service = new(apiConnection, new GlobalConfig());

            int sentNotifications = await service.CheckActiveRulesByScheduler();

            Assert.That(sentNotifications, Is.EqualTo(0));
            Assert.That(apiConnection.OwnerQueryCalls, Is.EqualTo(1));
            Assert.That(apiConnection.ActiveRuleQueryCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task CheckActiveRules_ReturnsZero_WhenNoActiveRulesExist()
        {
            OwnerActiveRuleCheckApiConn apiConnection = new();
            apiConnection.Owners =
            [
                new FwoOwner { Id = 7, Name = "Owner A", ExtAppId = "APP-7", DecommDate = DateTime.Now.AddDays(-2) }
            ];
            OwnerActiveRuleCheck service = new(apiConnection, new GlobalConfig());

            int sentNotifications = await service.CheckActiveRulesByScheduler();

            Assert.That(sentNotifications, Is.EqualTo(0));
            Assert.That(apiConnection.OwnerQueryCalls, Is.EqualTo(1));
            Assert.That(apiConnection.ActiveRuleQueryCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task CheckActiveRules_ReturnsZero_WhenOwnersAreLoadedForDailyCheck()
        {
            OwnerActiveRuleCheckApiConn apiConnection = new();
            apiConnection.Owners =
            [
                new FwoOwner { Id = 7, Name = "Owner A", ExtAppId = "APP-7", DecommDate = DateTime.Now.AddDays(-2) },
                new FwoOwner { Id = 8, Name = "Owner B", ExtAppId = "APP-8" }
            ];
            OwnerActiveRuleCheck service = new(apiConnection, new GlobalConfig());

            int sentNotifications = await service.CheckActiveRulesByScheduler();

            Assert.That(sentNotifications, Is.EqualTo(0));
            Assert.That(apiConnection.OwnerQueryCalls, Is.EqualTo(1));
            Assert.That(apiConnection.ActiveRuleQueryCalls, Is.EqualTo(1));
        }

        [Test]
        public void ExtractChangeId_ReturnsField2_ForValidJson()
        {
            string changeId = InvokeExtractChangeId("{\"field-2\":\"CHG-123\",\"Datum-Regelpruefung\":\"fallback\"}");

            Assert.That(changeId, Is.EqualTo("CHG-123"));
        }

        [Test]
        public void ExtractChangeId_ReturnsFallback_ForSingleQuotedLegacyPayload()
        {
            string changeId = InvokeExtractChangeId("{'Datum-Regelpruefung':'2026-03-19'}");

            Assert.That(changeId, Is.EqualTo("2026-03-19"));
        }

        [Test]
        public void ExtractChangeId_ReturnsEmpty_ForMalformedPayload()
        {
            string changeId = InvokeExtractChangeId("{'field-2':");

            Assert.That(changeId, Is.Empty);
        }

        [Test]
        public void CreateNotificationBodyHtml_WrapsBodyInHtmlFrame()
        {
            SimulatedGlobalConfig globalConfig = new();
            RuleNotificationBodyTestHelper service = new(globalConfig);
            string html = service.CreateNotificationBodyHtmlForTest("Owner Active Rules", "<table><tr><td>rule</td></tr></table>", "Owner A");

            Assert.That(html, Does.Contain("<html>"));
            Assert.That(html, Does.Contain("Owner A"));
            Assert.That(html, Does.Contain("<table><tr><td>rule</td></tr></table>"));
        }

        private static string InvokeExtractChangeId(string customFields)
        {
            MethodInfo extractChangeId = typeof(RuleNotificationBodyBase).GetMethod("ExtractChangeId", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ExtractChangeId method not found.");

            return (string)(extractChangeId.Invoke(null, [customFields])
                ?? throw new InvalidOperationException("ExtractChangeId returned null."));
        }

        private sealed class OwnerActiveRuleCheckApiConn : SimulatedApiConnection
        {
            public int ActiveRuleQueryCalls { get; private set; }
            public int OwnerQueryCalls { get; private set; }

            public Dictionary<int, List<Rule>> ActiveRulesByOwnerId { get; } = [];
            public List<FwoOwner> Owners { get; set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == OwnerQueries.getOwners)
                {
                    ++OwnerQueryCalls;
                    return Task.FromResult((QueryResponseType)(object)Owners);
                }

                if (query == RuleQueries.getActiveRulesByOwner)
                {
                    ++ActiveRuleQueryCalls;
                    int ownerId = GetOwnerId(variables);
                    List<Rule> result = ActiveRulesByOwnerId.TryGetValue(ownerId, out List<Rule>? activeRules)
                        ? activeRules
                        : [];
                    return Task.FromResult((QueryResponseType)(object)result);
                }

                throw new NotImplementedException($"Query not implemented in owner active rule check test api: {query}");
            }

            private static int GetOwnerId(object? variables)
            {
                if (variables == null)
                {
                    return 0;
                }

                var property = variables.GetType().GetProperty("ownerId");
                object? value = property?.GetValue(variables);
                return value is int ownerId ? ownerId : 0;
            }
        }

        private sealed class RuleNotificationBodyTestHelper(GlobalConfig globalConfig) : RuleNotificationBodyBase(globalConfig)
        {
            public string CreateNotificationBodyHtmlForTest(string title, string body, string ownerFilter)
            {
                return CreateNotificationBodyHtml(title, body, ownerFilter);
            }
        }
    }
}
