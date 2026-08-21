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
            Assert.That(result[0].SearchUserPwd, Is.Empty);
            Assert.That(result[0].WriteUserPwd, Is.Null.Or.Empty);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.getAllLdapConnectionsWithoutSecrets));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public void LdapQueriesForUiDoNotContainAnyPassword()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AuthQueries.getAllLdapConnectionsWithoutSecrets, Does.Not.Contain("ldap_search_user_pwd"));
                Assert.That(AuthQueries.getAllLdapConnectionsWithoutSecrets, Does.Not.Contain("ldap_write_user_pwd"));
                Assert.That(AuthQueries.getLdapConnectionsWithoutSecrets, Does.Not.Contain("ldap_search_user_pwd"));
                Assert.That(AuthQueries.getLdapConnectionsWithoutSecrets, Does.Not.Contain("ldap_write_user_pwd"));
            });
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
            Assert.That(apiConnection.QueryCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Update_KeepsStoredPasswordsWhenTheyAreNotProvided()
        {
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                UpdateResult = new ReturnId { UpdatedId = 7 },
                StoredSecrets =
                [
                    new UiLdapConnection
                    {
                        Id = 7,
                        SearchUserPwd = "storedSearchSecret",
                        WriteUserPwd = "storedWriteSecret"
                    }
                ]
            };
            AuthenticationServerController controller = new(apiConnection, [BuildLdap(7)]);

            LdapGetUpdateParameters updateParameters = BuildUpdateParameters(7);
            updateParameters.WriteUser = "cn=writer,dc=example,dc=com";
            await controller.Update(updateParameters);

            Assert.That(updateParameters.SearchUserPwd, Is.EqualTo("storedSearchSecret"));
            Assert.That(updateParameters.WriteUserPwd, Is.EqualTo("storedWriteSecret"));
            Assert.That(apiConnection.SecretsQueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Update_UsesTheProvidedPasswordsWithoutReadingTheStoredOnes()
        {
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                UpdateResult = new ReturnId { UpdatedId = 7 }
            };
            AuthenticationServerController controller = new(apiConnection, [BuildLdap(7)]);

            LdapGetUpdateParameters updateParameters = BuildUpdateParameters(7);
            updateParameters.SearchUserPwd = "newSearchSecret";
            await controller.Update(updateParameters);

            Assert.That(updateParameters.SearchUserPwd, Is.EqualTo("newSearchSecret"));
            Assert.That(apiConnection.SecretsQueryCount, Is.EqualTo(0));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Update_DoesNotKeepTheWritePasswordWhenTheWriteUserIsRemoved()
        {
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                UpdateResult = new ReturnId { UpdatedId = 7 },
                StoredSecrets =
                [
                    new UiLdapConnection
                    {
                        Id = 7,
                        SearchUserPwd = "storedSearchSecret",
                        WriteUserPwd = "storedWriteSecret"
                    }
                ]
            };
            AuthenticationServerController controller = new(apiConnection, [BuildLdap(7)]);

            LdapGetUpdateParameters updateParameters = BuildUpdateParameters(7);
            updateParameters.WriteUser = "";
            await controller.Update(updateParameters);

            Assert.That(updateParameters.SearchUserPwd, Is.EqualTo("storedSearchSecret"));
            Assert.That(updateParameters.WriteUserPwd, Is.Null.Or.Empty);
        }

        [Test]
        public async Task Update_LeavesPasswordsEmptyWhenTheConnectionIsUnknown()
        {
            AuthenticationServerControllerTestApiConnection apiConnection = new()
            {
                UpdateResult = new ReturnId { UpdatedId = 7 }
            };
            AuthenticationServerController controller = new(apiConnection, [BuildLdap(7)]);

            LdapGetUpdateParameters updateParameters = BuildUpdateParameters(7);
            await controller.Update(updateParameters);

            Assert.That(updateParameters.SearchUserPwd, Is.Null.Or.Empty);
            Assert.That(apiConnection.SecretsQueryCount, Is.EqualTo(1));
        }

        private static MiddlewareLdap BuildLdap(int id)
        {
            return new MiddlewareLdap(BuildUpdateParameters(id));
        }

        private static LdapGetUpdateParameters BuildUpdateParameters(int id)
        {
            return new LdapGetUpdateParameters
            {
                Id = id,
                Address = "ldap.example",
                Port = 636,
                Type = (int)LdapType.OpenLdap,
                PatternLength = 4,
                SearchUser = "cn=service,dc=example,dc=com",
                TenantLevel = 2,
                Active = true
            };
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
            public List<UiLdapConnection> StoredSecrets { get; set; } = [];
            public int SecretsQueryCount { get; private set; }
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

                if (typeof(T) == typeof(UiLdapConnection[]) && query == AuthQueries.getAllLdapConnectionsWithoutSecrets)
                {
                    return Task.FromResult((T)(object)LdapConnections);
                }

                if (typeof(T) == typeof(List<UiLdapConnection>) && query == AuthQueries.getLdapConnectionSecrets)
                {
                    SecretsQueryCount++;
                    return Task.FromResult((T)(object)StoredSecrets);
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
