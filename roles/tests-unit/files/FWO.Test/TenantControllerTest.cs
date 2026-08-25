using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using MiddlewareLdap = FWO.Middleware.Server.Ldap;

namespace FWO.Test
{
    [TestFixture]
    internal class TenantControllerTest
    {
        private static readonly string kInternalUserSearchPath = "ou=users,dc=fworch,dc=internal";
        private static readonly string kSearchUser = "cn=search,dc=fworch,dc=internal";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string kWriteUser = "cn=write,dc=fworch,dc=internal";

        [Test]
        public async Task Get_ReturnsConvertedTenants()
        {
            TenantControllerTestApiConnection apiConnection = new()
            {
                Tenants =
                [
                    new Tenant
                    {
                        Id = 7,
                        Name = "tenant-1",
                        Comment = "comment",
                        Project = "project",
                        ViewAllDevices = true
                    }
                ]
            };
            TenantController controller = new(new List<MiddlewareLdap>(), apiConnection);

            List<TenantGetReturnParameters> result = await controller.Get();

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo(7));
            Assert.That(result[0].Name, Is.EqualTo("tenant-1"));
            Assert.That(result[0].Comment, Is.EqualTo("comment"));
            Assert.That(result[0].Project, Is.EqualTo("project"));
            Assert.That(result[0].ViewAllDevices, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.getTenants));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Post_ReturnsZeroWithoutWritableLdap()
        {
            TenantControllerTestApiConnection apiConnection = new();
            TenantController controller = new(new List<MiddlewareLdap>(), apiConnection);

            int result = await controller.Post(new TenantAddParameters
            {
                Name = "tenant-1",
                ViewAllDevices = true
            });

            Assert.That(result, Is.Zero);
            Assert.That(apiConnection.QueryCount, Is.Zero);
        }

        [Test]
        public async Task Post_ReturnsTenantIdWhenWritableInternalLdapAndDatabaseSucceed()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateWritableInternalTenantLdap(client);
            TenantControllerTestApiConnection apiConnection = new()
            {
                AddTenantResult = new ReturnIdWrapper
                {
                    ReturnIds = new ReturnId[] { new() { NewId = 77 } }
                }
            };
            TenantController controller = new(new List<MiddlewareLdap> { ldap }, apiConnection);

            int result = await controller.Post(new TenantAddParameters
            {
                Name = "tenant-1",
                Comment = "comment",
                Project = "project",
                ViewAllDevices = true
            });

            Assert.That(result, Is.EqualTo(77));
            Assert.That(client.AddedEntries, Has.Count.EqualTo(1));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.addTenant));
        }

        [Test]
        public async Task Post_ReturnsZeroWhenDatabaseInsertFailsAfterLdapSuccess()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateWritableInternalTenantLdap(client);
            TenantControllerTestApiConnection apiConnection = new()
            {
                ThrowOnAddTenant = true
            };
            TenantController controller = new(new List<MiddlewareLdap> { ldap }, apiConnection);

            int result = await controller.Post(new TenantAddParameters
            {
                Name = "tenant-1",
                ViewAllDevices = true
            });

            Assert.That(result, Is.Zero);
            Assert.That(client.AddedEntries, Has.Count.EqualTo(1));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Change_ReturnsTrueWhenDatabaseUpdateMatchesTenantId()
        {
            TenantControllerTestApiConnection apiConnection = new()
            {
                UpdateResult = new ReturnId { UpdatedId = 7 }
            };
            TenantController controller = new(new List<MiddlewareLdap>(), apiConnection);

            bool result = await controller.Change(new TenantEditParameters
            {
                Id = 7,
                Comment = "updated",
                Project = "project",
                ViewAllDevices = true
            });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.updateTenant));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Change_ReturnsFalseWhenDatabaseUpdateDoesNotMatchTenantId()
        {
            TenantControllerTestApiConnection apiConnection = new()
            {
                UpdateResult = new ReturnId { UpdatedId = 6 }
            };
            TenantController controller = new(new List<MiddlewareLdap>(), apiConnection);

            bool result = await controller.Change(new TenantEditParameters
            {
                Id = 7,
                Comment = "updated",
                Project = "project",
                ViewAllDevices = true
            });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Delete_ReturnsTrueWhenDatabaseDeleteMatchesTenantId()
        {
            TenantControllerTestApiConnection apiConnection = new()
            {
                DeleteResult = new ReturnId { DeletedId = 7 }
            };
            TenantController controller = new(new List<MiddlewareLdap>(), apiConnection);

            bool result = await controller.Delete(new TenantDeleteParameters
            {
                Id = 7,
                Name = "tenant-1"
            });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.deleteTenant));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Delete_ReturnsFalseWhenDatabaseDeleteDoesNotMatchTenantId()
        {
            TenantControllerTestApiConnection apiConnection = new()
            {
                DeleteResult = new ReturnId { DeletedId = 6 }
            };
            TenantController controller = new(new List<MiddlewareLdap>(), apiConnection);

            bool result = await controller.Delete(new TenantDeleteParameters
            {
                Id = 7,
                Name = "tenant-1"
            });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
        }

        private static TestableLdap CreateWritableInternalTenantLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 1,
                Address = "ldap.example.test",
                Port = 389,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                WriteUser = kWriteUser,
                WriteUserPwd = "writepwd",
                UserSearchPath = kInternalUserSearchPath
            };
        }

        private sealed class TenantControllerTestApiConnection : SimulatedApiConnection
        {
            public Tenant[] Tenants { get; set; } = [];
            public ReturnId UpdateResult { get; set; } = new();
            public ReturnId DeleteResult { get; set; } = new();
            public ReturnIdWrapper AddTenantResult { get; set; } = new();
            public bool ThrowOnAddTenant { get; set; }
            public string? LastQuery { get; private set; }
            public object? LastVariables { get; private set; }
            public int QueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                QueryCount++;

                if (typeof(QueryResponseType) == typeof(Tenant[]) && query == AuthQueries.getTenants)
                {
                    return Task.FromResult((QueryResponseType)(object)Tenants);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == AuthQueries.updateTenant)
                {
                    return Task.FromResult((QueryResponseType)(object)UpdateResult);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == AuthQueries.deleteTenant)
                {
                    return Task.FromResult((QueryResponseType)(object)DeleteResult);
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == AuthQueries.addTenant)
                {
                    if (ThrowOnAddTenant)
                    {
                        throw new InvalidOperationException("add tenant failed");
                    }
                    return Task.FromResult((QueryResponseType)(object)AddTenantResult);
                }

                throw new AssertionException($"Unexpected query: {query} for type {typeof(QueryResponseType).Name}");
            }
        }
    }
}
