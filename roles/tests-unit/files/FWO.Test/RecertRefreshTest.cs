using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Recert;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class RecertRefreshTest
    {
        [Test]
        public async Task RecalcRecerts_RefreshesViewAndAddsRecertsForOwnersWithOpenEntries()
        {
            RecordingRecertRefreshApiConnection apiConnection = new();
            apiConnection.Owners.Add(new FwoOwner { Id = 1, Name = "Owner A" });
            apiConnection.Owners.Add(new FwoOwner { Id = 2, Name = "Owner B" });
            apiConnection.Managements.Add(new Management { Id = 10, Name = "Mgmt A" });
            apiConnection.Managements.Add(new Management { Id = 20, Name = "Mgmt B" });
            apiConnection.OpenRecertsByOwnerAndManagement[(1, 10)] = CreateOpenRecerts(1, 10);

            bool failed = await RecertRefresh.RecalcRecerts(apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(failed, Is.False);
                Assert.That(apiConnection.CountQuery(RecertQueries.addRecertEntries), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(RecertQueries.refreshViewRuleWithOwner), Is.EqualTo(1));
                Assert.That(apiConnection.CountQuery(RecertQueries.clearOpenRecerts), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task RecalcRecerts_ReturnsTrueWhenRefreshStatusIsUnexpected()
        {
            RecordingRecertRefreshApiConnection apiConnection = new()
            {
                RefreshStatus = "refresh failed"
            };
            apiConnection.Owners.Add(new FwoOwner { Id = 1, Name = "Owner A" });
            apiConnection.Managements.Add(new Management { Id = 10, Name = "Mgmt A" });

            bool failed = await RecertRefresh.RecalcRecerts(apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(failed, Is.True);
                Assert.That(apiConnection.CountQuery(RecertQueries.addRecertEntries), Is.EqualTo(0));
            });
        }

        private static List<RecertificationBase> CreateOpenRecerts(int ownerId, int managementId)
        {
            return new List<RecertificationBase>
            {
                new()
                {
                    OwnerId = ownerId,
                    RuleId = managementId * 100,
                    RuleMetadataId = managementId * 1000
                }
            };
        }

        private static OwnerRefresh CreateOwnerRefresh(string status)
        {
            OwnerRefresh refresh = new();
            PropertyInfo property = typeof(OwnerRefresh).GetProperty("Status", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(typeof(OwnerRefresh).FullName, "Status");
            property.SetValue(refresh, status);
            return refresh;
        }

        private sealed class RecordingRecertRefreshApiConnection : SimulatedApiConnection
        {
            public List<(string Query, object? Variables)> Queries { get; } = new();
            public List<FwoOwner> Owners { get; } = new();
            public List<Management> Managements { get; } = new();
            public Dictionary<(int OwnerId, int ManagementId), List<RecertificationBase>> OpenRecertsByOwnerAndManagement { get; } = new();
            public string RefreshStatus { get; set; } = "Materialized view refreshed successfully";

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add((query, variables));

                if (query == OwnerQueries.getOwners && typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)Owners);
                }

                if (query == DeviceQueries.getManagementDetailsWithoutSecrets && typeof(QueryResponseType) == typeof(List<Management>))
                {
                    return Task.FromResult((QueryResponseType)(object)Managements);
                }

                if (query == RecertQueries.clearOpenRecerts && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = new ReturnId[0] });
                }

                if (query == RecertQueries.refreshViewRuleWithOwner && typeof(QueryResponseType) == typeof(List<OwnerRefresh>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<OwnerRefresh> { CreateOwnerRefresh(RefreshStatus) });
                }

                if (query == RecertQueries.getOpenRecertsForOwners && typeof(QueryResponseType) == typeof(List<RecertificationBase>))
                {
                    int ownerId = GetVariable<int>(variables, "ownerId");
                    int mgmId = GetVariable<int>(variables, "mgmId");
                    if (OpenRecertsByOwnerAndManagement.TryGetValue((ownerId, mgmId), out List<RecertificationBase>? recerts))
                    {
                        return Task.FromResult((QueryResponseType)(object)recerts);
                    }
                    return Task.FromResult((QueryResponseType)(object)new List<RecertificationBase>());
                }

                if (query == RecertQueries.addRecertEntries && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = new ReturnId[0] });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            public int CountQuery(string query)
            {
                return Queries.Count(item => item.Query == query);
            }

            private static TValue GetVariable<TValue>(object? variables, string propertyName)
            {
                PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
                return property != null ? (TValue)property.GetValue(variables)! : default!;
            }
        }
    }
}
