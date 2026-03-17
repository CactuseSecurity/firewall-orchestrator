using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class OwnerActiveRuleCheckTest
    {
        [Test]
        public async Task CheckActiveRules_ReturnsZero_WhenOwnerIdIsInvalid()
        {
            OwnerActiveRuleCheckApiConn apiConnection = new();
            OwnerActiveRuleCheck service = new(apiConnection, new GlobalConfig());

            int sentNotifications = await service.CheckActiveRules(new FwoOwner { Id = 0, Name = "Owner A", ExtAppId = "APP-0" });

            Assert.That(sentNotifications, Is.EqualTo(0));
            Assert.That(apiConnection.ActiveRuleQueryCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task CheckActiveRules_ReturnsZero_WhenNoActiveRulesExist()
        {
            OwnerActiveRuleCheckApiConn apiConnection = new();
            OwnerActiveRuleCheck service = new(apiConnection, new GlobalConfig());

            int sentNotifications = await service.CheckActiveRules(new FwoOwner { Id = 7, Name = "Owner A", ExtAppId = "APP-7" });

            Assert.That(sentNotifications, Is.EqualTo(0));
            Assert.That(apiConnection.ActiveRuleQueryCalls, Is.EqualTo(1));
        }

        private sealed class OwnerActiveRuleCheckApiConn : SimulatedApiConnection
        {
            public int ActiveRuleQueryCalls { get; private set; }

            public Dictionary<int, List<Rule>> ActiveRulesByOwnerId { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
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
    }
}
