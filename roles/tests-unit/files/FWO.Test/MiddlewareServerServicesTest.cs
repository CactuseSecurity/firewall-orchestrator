using System;
using System.Collections.Generic;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using NUnit.Framework;
using Novell.Directory.Ldap;

namespace FWO.Test
{
    [TestFixture]
    internal class MiddlewareServerServicesTest
    {
        private static readonly string kInternalUserSearchPath = "ou=users,dc=fworch,dc=internal";
        private static readonly string kGroupSearchPath = "ou=groups,dc=fworch,dc=internal";
        private static readonly string kSearchUser = "cn=search,dc=fworch,dc=internal";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string kGroupDn = "cn=AppOwners,ou=groups,dc=fworch,dc=internal";
        private static readonly string kMemberDn = "uid=user1,ou=users,dc=fworch,dc=internal";
        private static readonly string[] kOwnerGroupValues = ["ownergroup"];
        private static readonly string[] kMemberValues = [kMemberDn];

        [Test]
        public async Task GetInternalGroups_ReturnsOwnerGroupsFromInternalLdap()
        {
            MiddlewareServerServicesTestApiConnection apiConnection = new()
            {
                ConnectedLdaps = new List<Ldap>
                {
                    CreateInternalGroupLdap()
                }
            };

            List<UserGroup> groups = await MiddlewareServerServices.GetInternalGroups(apiConnection);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].Dn, Is.EqualTo(kGroupDn));
            Assert.That(groups[0].Name, Is.EqualTo("AppOwners"));
            Assert.That(groups[0].OwnerGroup, Is.True);
            Assert.That(groups[0].Users, Has.Count.EqualTo(1));
        }

        [Test]
        public void GetInternalGroups_ThrowsWhenNoInternalGroupLdapExists()
        {
            MiddlewareServerServicesTestApiConnection apiConnection = new()
            {
                ConnectedLdaps = new List<Ldap>
                {
                    new Ldap
                    {
                        UserSearchPath = "ou=users,dc=external,dc=example",
                        GroupSearchPath = kGroupSearchPath
                    }
                }
            };

            KeyNotFoundException? exception = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await MiddlewareServerServices.GetInternalGroups(apiConnection));

            Assert.That(exception?.Message, Is.EqualTo("No internal Ldap with group handling found."));
        }

        private static TestableLdap CreateInternalGroupLdap()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        kGroupDn,
                        new LdapAttribute("uniqueMember", kMemberValues),
                        new LdapAttribute("businessCategory", kOwnerGroupValues)))
            };

            return new TestableLdap(client)
            {
                Id = 1,
                Address = "ldap.example.test",
                Port = 389,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kInternalUserSearchPath,
                GroupSearchPath = kGroupSearchPath
            };
        }

        private sealed class MiddlewareServerServicesTestApiConnection : SimulatedApiConnection
        {
            public List<Ldap> ConnectedLdaps { get; set; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getLdapConnections && typeof(QueryResponseType) == typeof(List<Ldap>))
                {
                    return Task.FromResult((QueryResponseType)(object)ConnectedLdaps);
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
