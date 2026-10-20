using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;
using Novell.Directory.Ldap;
using MiddlewareLdap = FWO.Middleware.Server.Ldap;

namespace FWO.Test
{
    [TestFixture]
    internal class UserSessionRevocationTest
    {
        private const string kUserSearchPath = "ou=users,dc=fworch,dc=internal";
        private const string kGroupSearchPath = "ou=groups,dc=fworch,dc=internal";
        private const string kGroupDn = "cn=AppOwners,ou=groups,dc=fworch,dc=internal";
        private const string kNestedGroupDn = "cn=Nested,ou=groups,dc=fworch,dc=internal";
        private const string kUserDn = "uid=user1,ou=users,dc=fworch,dc=internal";
        private const string kMemberDn = "uid=member,ou=users,dc=fworch,dc=internal";
        private const string kNestedMemberDn = "uid=nested,ou=users,dc=fworch,dc=internal";
        private const string kUnknownDn = "uid=unknown,ou=users,dc=fworch,dc=internal";
        private const string kReason = "test change";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string[] kGroupMembers = { kMemberDn, kNestedGroupDn };
        private static readonly string[] kNestedGroupMembers = { kNestedMemberDn };
        private static readonly List<string> kUserDnList = new() { kUserDn };
        private static readonly List<string> kGroupDnList = new() { kGroupDn };
        private static readonly List<string> kUnknownDnList = new() { kUnknownDn };
        private static readonly List<int> kUserIds = new() { 3, 5 };

        [Test]
        public async Task ResolveLocalUserIds_MatchesUserDnsIgnoringCase()
        {
            SessionRevocationApiConnection apiConnection = CreateApiConnection();
            UserSessionRevocation revocation = new(apiConnection, new List<MiddlewareLdap>());
            List<string> upperCaseDns = new() { kUserDn.ToUpperInvariant() };

            List<int> userIds = await revocation.ResolveLocalUserIds(upperCaseDns);

            Assert.That(userIds, Is.EqualTo(new List<int> { 1 }));
        }

        [Test]
        public async Task ResolveLocalUserIds_FollowsNestedGroups()
        {
            RecordingLdapClient client = new()
            {
                ReadResultsByDn =
                {
                    [kGroupDn] = LdapTestSupport.CreateEntry(kGroupDn, new LdapAttribute("uniqueMember", kGroupMembers)),
                    [kNestedGroupDn] = LdapTestSupport.CreateEntry(kNestedGroupDn, new LdapAttribute("uniqueMember", kNestedGroupMembers))
                }
            };
            UserSessionRevocation revocation = new(CreateApiConnection(), new List<MiddlewareLdap> { CreateGroupLdap(client) });

            List<int> userIds = await revocation.ResolveLocalUserIds(kGroupDnList);

            Assert.That(userIds, Is.EquivalentTo(new List<int> { 2, 4 }));
        }

        [Test]
        public async Task ResolveLocalUserIds_IgnoresDnsWithoutLocalUser()
        {
            UserSessionRevocation revocation = new(CreateApiConnection(), new List<MiddlewareLdap>());

            List<int> userIds = await revocation.ResolveLocalUserIds(kUnknownDnList);

            Assert.That(userIds, Is.Empty);
        }

        [Test]
        public async Task ResolveLocalUserIds_ReturnsNoUserWhenTheUsersCannotBeRead()
        {
            SessionRevocationApiConnection apiConnection = CreateApiConnection();
            apiConnection.UserQueryException = new InvalidOperationException("api down");
            UserSessionRevocation revocation = new(apiConnection, new List<MiddlewareLdap>());

            List<int> userIds = await revocation.ResolveLocalUserIds(kUserDnList);

            Assert.That(userIds, Is.Empty);
        }

        [Test]
        public async Task RevokeSessions_RevokesTheTokensOfAllGivenUsers()
        {
            SessionRevocationApiConnection apiConnection = CreateApiConnection();
            apiConnection.RevokedRows = 4;
            UserSessionRevocation revocation = new(apiConnection, new List<MiddlewareLdap>());

            int revoked = await revocation.RevokeSessions(kUserIds, kReason);

            Assert.That(revoked, Is.EqualTo(4));
            Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<List<int>> { kUserIds }));
        }

        [Test]
        public async Task RevokeSessions_SendsNothingWithoutUsers()
        {
            SessionRevocationApiConnection apiConnection = CreateApiConnection();
            UserSessionRevocation revocation = new(apiConnection, new List<MiddlewareLdap>());

            int revoked = await revocation.RevokeSessions(new List<int>(), kReason);

            Assert.That(revoked, Is.Zero);
            Assert.That(apiConnection.RevokedUserIds, Is.Empty);
        }

        [Test]
        public async Task RevokeSessions_DoesNotThrowWhenTheRevocationFails()
        {
            SessionRevocationApiConnection apiConnection = CreateApiConnection();
            apiConnection.RevocationException = new InvalidOperationException("api down");
            UserSessionRevocation revocation = new(apiConnection, new List<MiddlewareLdap>());

            int revoked = await revocation.RevokeSessions(kUserIds, kReason);

            Assert.That(revoked, Is.Zero);
        }

        [Test]
        public async Task RevokeSessionsOfUser_RevokesTheTokensOfTheUser()
        {
            SessionRevocationApiConnection apiConnection = CreateApiConnection();
            UserSessionRevocation revocation = new(apiConnection, new List<MiddlewareLdap>());

            await revocation.RevokeSessionsOfUser(7, kReason);

            Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<List<int>> { new() { 7 } }));
        }

        [Test]
        public async Task RevokeSessionsOfDns_RevokesTheTokensOfTheResolvedUsers()
        {
            SessionRevocationApiConnection apiConnection = CreateApiConnection();
            UserSessionRevocation revocation = new(apiConnection, new List<MiddlewareLdap>());

            await revocation.RevokeSessionsOfDns(kUserDnList, kReason);

            Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<List<int>> { new() { 1 } }));
        }

        internal static SessionRevocationApiConnection CreateApiConnection()
        {
            SessionRevocationApiConnection apiConnection = new();
            apiConnection.LocalUsers.Add(new UiUser { DbId = 1, Dn = kUserDn });
            apiConnection.LocalUsers.Add(new UiUser { DbId = 2, Dn = kMemberDn });
            apiConnection.LocalUsers.Add(new UiUser { DbId = 4, Dn = kNestedMemberDn });
            return apiConnection;
        }

        private static TestableLdap CreateGroupLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 1,
                Addresses = ["ldap.example.test"],
                Port = 389,
                SearchUser = "cn=search,dc=fworch,dc=internal",
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kUserSearchPath,
                GroupSearchPath = kGroupSearchPath,
                GroupWritePath = kGroupSearchPath
            };
        }
    }
}
