using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using MiddlewareLdap = FWO.Middleware.Server.Ldap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Reflection;

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

        /// <summary>
        /// SEC-04: the connection test reaches a server named by the caller and carries the
        /// credentials entered for it in the request body. A GET with a body is handled
        /// inconsistently by proxies and http clients, and its url and body risk being cached or
        /// written to an access log on the way, so the test has to be a POST even though it changes
        /// nothing. Asserted on the attributes because nothing but a live proxy would report it.
        /// </summary>
        [Test]
        public void TestConnection_IsAPostSoItsBodyIsNotSentOnAGet()
        {
            MethodInfo testConnection = GetTestConnectionMethod();

            Assert.Multiple(() =>
            {
                Assert.That(testConnection.GetCustomAttributes<HttpGetAttribute>(), Is.Empty,
                    "a body on a GET is dropped by some http clients and logged by some proxies");
                HttpPostAttribute? post = testConnection.GetCustomAttribute<HttpPostAttribute>();
                Assert.That(post, Is.Not.Null);
                Assert.That(post!.Template, Is.EqualTo("TestConnection"));
                Assert.That(testConnection.GetParameters()[0].GetCustomAttribute<FromBodyAttribute>(), Is.Not.Null,
                    "the credentials entered for the test belong in the body, not in the url");
            });
        }

        /// <summary>
        /// SEC-04: the test binds to a server and port chosen by the caller, so it must stay with
        /// the role that may already configure those. An auditor is read-only and has to be refused.
        /// </summary>
        [Test]
        public void TestConnection_IsRestrictedToAdmin()
        {
            AuthorizeAttribute? authorize = GetTestConnectionMethod().GetCustomAttribute<AuthorizeAttribute>();

            Assert.That(authorize, Is.Not.Null);
            List<string> roles = (authorize!.Roles ?? "")
                .Split(',')
                .Select(role => role.Trim())
                .Where(role => role.Length > 0)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(roles, Is.EqualTo(new List<string> { Roles.Admin }));
                Assert.That(roles, Has.No.Member(Roles.Auditor),
                    "an auditor may read the connections but must not make the product bind anywhere");
            });
        }

        /// <summary>
        /// Locates the connection test endpoint, so a rename cannot make the two checks above pass
        /// against a method that no longer exists.
        /// </summary>
        private static MethodInfo GetTestConnectionMethod()
        {
            MethodInfo? testConnection = typeof(AuthenticationServerController)
                .GetMethod(nameof(AuthenticationServerController.TestConnection));
            Assert.That(testConnection, Is.Not.Null, "the connection test endpoint was renamed or removed");
            return testConnection!;
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
            List<MiddlewareLdap> ldaps = [BuildLdap(7)];
            AuthenticationServerController controller = new(apiConnection, ldaps);

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
            List<MiddlewareLdap> ldaps = [BuildLdap(7)];
            AuthenticationServerController controller = new(apiConnection, ldaps);

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
            List<MiddlewareLdap> ldaps = [BuildLdap(7)];
            AuthenticationServerController controller = new(apiConnection, ldaps);

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
            List<MiddlewareLdap> ldaps = [BuildLdap(7)];
            AuthenticationServerController controller = new(apiConnection, ldaps);

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
