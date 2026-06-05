using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Server;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class UiUserHandlerTest
    {
        [Test]
        public async Task GetExpirationTime_WhenConfigExistsInDatabase_ReturnsDatabaseValue()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime> { new() { ExpirationValue = 11 } });

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.AccessTokenLifetimeHours));

            Assert.That(expirationTime, Is.EqualTo(11));
        }

        [Test]
        public async Task GetExpirationTime_WhenConfigMissingInDatabase_ReturnsConfiguredDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime>());

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.RefreshTokenLifetimeDays));

            Assert.That(expirationTime, Is.EqualTo(7));
        }

        [Test]
        public async Task GetExpirationTime_WhenLifetimeKeyIsUnknown_ReturnsHardcodedDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, "UnknownLifetimeKey");

            Assert.That(expirationTime, Is.EqualTo(720));
        }

        [Test]
        public async Task GetOwnershipsFromOwnerLdap_UsesOwnerResponsibleQueries()
        {
            OwnershipApiConnection apiConnection = new();
            UiUser user = new()
            {
                Dn = "uid=user1,ou=users,dc=example,dc=com",
                Groups = ["cn=group1,ou=groups,dc=example,dc=com"]
            };

            await UiUserHandler.GetOwnershipsFromOwnerLdap(apiConnection, user);

            Assert.That(user.Ownerships, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(user.RecertOwnerships, Is.EquivalentTo(new[] { 3 }));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersForUser));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersFromGroups));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersForDnsWithRecertification));
            Assert.That(apiConnection.Queries, Does.Not.Contain(OwnerQueries.getOwners));
            Assert.That(apiConnection.Queries, Does.Not.Contain(ConfigQueries.getConfigItemByKey));
        }

        private sealed class OwnershipApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                object result = query switch
                {
                    string value when value == OwnerQueries.getOwnersForUser => new List<FwoOwner> { new() { Id = 1 } },
                    string value when value == OwnerQueries.getOwnersFromGroups => new List<FwoOwner> { new() { Id = 2 } },
                    string value when value == OwnerQueries.getOwnersForDnsWithRecertification => new List<FwoOwner> { new() { Id = 3 } },
                    _ => throw new AssertionException($"Unexpected query: {query}")
                };
                return Task.FromResult((QueryResponseType)result);
            }
        }
    }
}
