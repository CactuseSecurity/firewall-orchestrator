using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class WorkflowRecipientResolverTest
    {
        private const string kUserSearchPath = "ou=users,dc=test";
        private const string kRecipientDn = "uid=alice,ou=users,dc=test";
        private const string kRecipientEmail = "alice@example.test";

        /// <summary>
        /// Caching an LDAP-resolved recipient in uiuser is a side effect. When the API drops
        /// out at that point, the address is already known from LDAP, so the recipient must
        /// still be returned instead of the whole resolution failing.
        /// </summary>
        [Test]
        public async Task ResolveUsers_WhenUpsertCannotReachApi_StillReturnsLdapRecipient()
        {
            RecipientApiConnection apiConnection = new();
            RecordingLdapClient ldapClient = new();
            ldapClient.ReadResultsByDn[kRecipientDn] = LdapTestSupport.CreateEntry(
                kRecipientDn,
                new LdapAttribute("uid", "alice"),
                new LdapAttribute("mail", kRecipientEmail));
            WorkflowRecipientResolver resolver = new(apiConnection, [CreateLdap(ldapClient)]);

            List<UiUser> recipients = await resolver.ResolveUsers([kRecipientDn]);

            Assert.Multiple(() =>
            {
                Assert.That(recipients.Select(user => user.Email), Is.EqualTo(new[] { kRecipientEmail }));
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.upsertUiUser),
                    "the upsert must have been attempted, otherwise the test does not cover the failure");
            });
        }

        private static TestableLdap CreateLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 3,
                Address = "ldap.example.test",
                Port = 389,
                SearchUser = "cn=search,dc=test",
                SearchUserPwd = LdapTestSupport.CreateEncryptedSecret("searchpwd"),
                UserSearchPath = kUserSearchPath
            };
        }

        /// <summary>
        /// Knows no cached users and loses the API as soon as a user is to be written.
        /// </summary>
        private sealed class RecipientApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == AuthQueries.getUserEmails)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiUser>());
                }

                if (query == AuthQueries.upsertUiUser)
                {
                    throw new HttpRequestException("connection reset by peer");
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
