using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using MiddlewareLdap = FWO.Middleware.Server.Ldap;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class AuthenticationServerControllerTest
    {
        [Test]
        public async Task Get_ReturnsConvertedLdapConnections()
        {
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                LdapConnections =
                [
                    new UiLdapConnection(new LdapGetUpdateParameters
                    {
                        Id = 7,
                        Name = "ldap-one",
                        Address = "ldap.example",
                        Port = 636,
                        Type = (int)LdapType.OpenLdap,
                        PatternLength = 4,
                        SearchUser = "cn=service,dc=example,dc=com",
                        TenantLevel = 2,
                        Active = true
                    })
                ]
            };
            AuthenticationServerController controller = new(apiConnection, []);

            List<LdapGetUpdateParameters> result = await controller.Get();

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(7));
            Assert.That(result[0].Name, Is.EqualTo("ldap-one"));
            Assert.That(result[0].Address, Is.EqualTo("ldap.example"));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.getAllLdapConnections));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PostAsync_AddsLdapToLocalList()
        {
            List<MiddlewareLdap> ldaps = [];
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                NewConnectionResult = new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { NewId = 42 }]
                }
            };
            AuthenticationServerController controller = new(apiConnection, ldaps);

            int result = await controller.PostAsync(new LdapAddParameters
            {
                Address = "ldap.example",
                Port = 636,
                Type = (int)LdapType.OpenLdap,
                PatternLength = 4,
                SearchUser = "cn=service,dc=example,dc=com",
                TenantLevel = 2,
                Active = true
            });

            Assert.That(result, Is.EqualTo(42));
            Assert.That(ldaps, Has.Count.EqualTo(1));
            Assert.That(ldaps[0].Id, Is.EqualTo(42));
            Assert.That(ldaps[0].Address, Is.EqualTo("ldap.example"));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.newLdapConnection));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Update_ReplacesMatchingLdapInLocalList()
        {
            List<MiddlewareLdap> ldaps =
            [
                new MiddlewareLdap(new LdapGetUpdateParameters
                {
                    Id = 7,
                    Address = "ldap-old.example",
                    Port = 636,
                    Type = (int)LdapType.OpenLdap,
                    PatternLength = 4,
                    SearchUser = "cn=service,dc=example,dc=com",
                    TenantLevel = 2,
                    Active = true
                })
            ];
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                UpdateResult = new ReturnId { UpdatedId = 7 }
            };
            AuthenticationServerController controller = new(apiConnection, ldaps);

            int result = await controller.Update(new LdapGetUpdateParameters
            {
                Id = 7,
                Address = "ldap-new.example",
                Port = 636,
                Type = (int)LdapType.OpenLdap,
                PatternLength = 4,
                SearchUser = "cn=service,dc=example,dc=com",
                TenantLevel = 2,
                Active = true
            });

            Assert.That(result, Is.EqualTo(7));
            Assert.That(ldaps, Has.Count.EqualTo(1));
            Assert.That(ldaps[0].Address, Is.EqualTo("ldap-new.example"));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.updateLdapConnection));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Delete_RemovesMatchingLdapFromLocalList()
        {
            List<MiddlewareLdap> ldaps =
            [
                new MiddlewareLdap(new LdapGetUpdateParameters
                {
                    Id = 7,
                    Address = "ldap.example",
                    Port = 636,
                    Type = (int)LdapType.OpenLdap,
                    PatternLength = 4,
                    SearchUser = "cn=service,dc=example,dc=com",
                    TenantLevel = 2,
                    Active = true
                })
            ];
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                DeleteResult = new ReturnId { DeletedId = 7 }
            };
            AuthenticationServerController controller = new(apiConnection, ldaps);

            int result = await controller.Delete(new LdapDeleteParameters { Id = 7 });

            Assert.That(result, Is.EqualTo(7));
            Assert.That(ldaps, Is.Empty);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.deleteLdapConnection));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        private sealed class AuthenticationServerControllerTestApiConnection : SimulatedApiConnection
        {
            public UiLdapConnection[] LdapConnections { get; set; } = [];
            public ReturnIdWrapper NewConnectionResult { get; set; } = new();
            public ReturnId UpdateResult { get; set; } = new();
            public ReturnId DeleteResult { get; set; } = new();
            public string? LastQuery { get; private set; }
            public object? LastVariables { get; private set; }
            public int QueryCount { get; private set; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                QueryCount++;

                if (typeof(T) == typeof(UiLdapConnection[]) && query == AuthQueries.getAllLdapConnections)
                {
                    return Task.FromResult((T)(object)LdapConnections);
                }

                if (typeof(T) == typeof(ReturnIdWrapper) && query == AuthQueries.newLdapConnection)
                {
                    return Task.FromResult((T)(object)NewConnectionResult);
                }

                if (typeof(T) == typeof(ReturnId) && query == AuthQueries.updateLdapConnection)
                {
                    return Task.FromResult((T)(object)UpdateResult);
                }

                if (typeof(T) == typeof(ReturnId) && query == AuthQueries.deleteLdapConnection)
                {
                    return Task.FromResult((T)(object)DeleteResult);
                }

                throw new AssertionException($"Unexpected query: {query} for type {typeof(T).Name}");
            }
        }
    }
}
