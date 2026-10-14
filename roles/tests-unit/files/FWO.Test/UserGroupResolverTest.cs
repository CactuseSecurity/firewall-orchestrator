using FWO.Middleware.Server;
using FWO.Middleware.Server.Services;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UserGroupResolverTest
    {
        private const string kUserDn = "cn=testuser,ou=users,ou=operator,dc=fworch,dc=internal";
        private const string kInternalUserPath = "ou=users,ou=operator,dc=fworch,dc=internal";
        private const string kExternalUserPath = "ou=users,dc=example,dc=com";
        private const string kInternalGroupPath = "ou=groups,ou=operator,dc=fworch,dc=internal";
        private const string kExternalGroupPath = "ou=groups,dc=example,dc=com";
        private const string kSearchUser = "cn=search,dc=example,dc=com";
        private const string kOtherInternalUserPath = "ou=users,ou=other,dc=fworch,dc=internal";
        private const string kOtherInternalGroupPath = "ou=groups,ou=other,dc=fworch,dc=internal";
        private const string kNestedUserPath = "ou=users,ou=nested,dc=example,dc=org";
        private const string kNestedGroupPath = "ou=nested,ou=groups,dc=example,dc=com";
        private const int kInternalLdapId = 1;
        private const int kExternalLdapId = 2;
        private const int kNestedLdapId = 3;
        private const string kOwnGroupDn = "cn=own,ou=groups,dc=example,dc=com";
        private const string kForeignGroupDn = "cn=foreign,ou=nested,ou=groups,dc=example,dc=com";
        private static readonly string[] kOwnAndForeignGroupDns = [kOwnGroupDn, kForeignGroupDn];

        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly List<Ldap> kNoLdaps = [];
        private static readonly string[] kMemberOfValues =
        [
            "cn=direct-group,ou=groups,ou=operator,dc=fworch,dc=internal",
            "cn=unrelated,ou=elsewhere,dc=example,dc=com"
        ];

        [Test]
        public async Task GetGroupsForUser_ReturnsEmptyForMissingDn()
        {
            UserGroupResolver resolver = new(kNoLdaps);

            List<string> groups = await resolver.GetGroupsForUser("", kInternalLdapId);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupsForUser_ReturnsEmptyForWhitespaceDn()
        {
            UserGroupResolver resolver = new(kNoLdaps);

            List<string> groups = await resolver.GetGroupsForUser("   ", kInternalLdapId);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupsForUser_ReturnsEmptyWhenTheLdapDoesNotKnowTheUser()
        {
            // ReadAsync returns null, so the user cannot be located in its ldap
            RecordingLdapClient connection = new();
            Ldap ldap = CreateLdap(connection, kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([ldap]);

            List<string> groups = await resolver.GetGroupsForUser(kUserDn, kInternalLdapId);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.Empty);
                Assert.That(connection.ReadCalls, Does.Contain(kUserDn));
            });
        }

        [Test]
        public async Task GetGroupsForUser_ResolvesMemberOfGroupsWithinTheConfiguredGroupPath()
        {
            RecordingLdapClient connection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues))
            };
            Ldap ldap = CreateLdap(connection, kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([ldap]);

            List<string> groups = await resolver.GetGroupsForUser(kUserDn, kInternalLdapId);

            // only the group inside the configured group path is kept, the unrelated one is dropped
            Assert.That(groups, Is.EqualTo(new List<string> { "cn=direct-group,ou=groups,ou=operator,dc=fworch,dc=internal" }));
        }

        [Test]
        public async Task GetGroupsForUser_ReturnsEmptyWhenTheUserHasNoMemberships()
        {
            RecordingLdapClient connection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn)
            };
            Ldap ldap = CreateLdap(connection, kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([ldap]);

            List<string> groups = await resolver.GetGroupsForUser(kUserDn, kInternalLdapId);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.Empty);
                Assert.That(connection.ReadCalls, Does.Contain(kUserDn));
            });
        }

        [Test]
        public async Task GetGroupsForUser_ResolvesOnlyInTheDirectoryOfTheUser()
        {
            RecordingLdapClient internalConnection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues))
            };
            Ldap internalLdap = CreateLdap(internalConnection, kInternalUserPath, kInternalGroupPath);
            RecordingLdapClient externalConnection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues))
            };
            Ldap externalLdap = CreateLdap(externalConnection, kExternalUserPath, kExternalGroupPath, kExternalLdapId);
            List<Ldap> configuredLdaps = [externalLdap, internalLdap];
            UserGroupResolver resolver = new(configuredLdaps);

            // the dn belongs to the internal ldap, so a local user of the external ldap with that dn gets no groups
            List<string> groups = await resolver.GetGroupsForUser(kUserDn, kExternalLdapId);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.Empty);
                Assert.That(externalConnection.ReadCalls, Is.Empty);
                Assert.That(internalConnection.ReadCalls, Is.Empty);
            });
        }

        [Test]
        public async Task GetGroupsForUser_ReturnsEmptyForAnAmbiguousDn()
        {
            RecordingLdapClient connection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues))
            };
            Ldap ldap = CreateLdap(connection, kInternalUserPath, kInternalGroupPath);
            Ldap overlappingLdap = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath, kExternalLdapId);
            List<Ldap> configuredLdaps = [ldap, overlappingLdap];
            UserGroupResolver resolver = new(configuredLdaps);

            List<string> groups = await resolver.GetGroupsForUser(kUserDn, kInternalLdapId);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.Empty);
                Assert.That(connection.ReadCalls, Is.Empty);
            });
        }

        [Test]
        public async Task GetGroupsForUser_IgnoresInactiveLdap()
        {
            RecordingLdapClient inactiveConnection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", $"cn=inactive,{kInternalGroupPath}"))
            };
            Ldap inactive = CreateLdap(inactiveConnection, kInternalUserPath, kInternalGroupPath);
            inactive.Active = false;
            List<Ldap> configuredLdaps = [inactive];
            UserGroupResolver resolver = new(configuredLdaps);

            List<string> groups = await resolver.GetGroupsForUser(kUserDn, kInternalLdapId);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.Empty);
                Assert.That(inactiveConnection.ReadCalls, Is.Empty);
            });
        }

        [Test]
        public async Task GetGroups_ResolvesMemberOfForAnInternalUserWithoutFanOut()
        {
            Ldap hosting = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            RecordingLdapClient otherInternalConnection = new();
            Ldap otherInternal = CreateLdap(otherInternalConnection, kOtherInternalUserPath, kOtherInternalGroupPath, kExternalLdapId);
            UserGroupResolver resolver = new([hosting, otherInternal]);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues));

            List<string> groups = await resolver.GetGroups(userEntry, hosting);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.EqualTo(new List<string> { "cn=direct-group,ou=groups,ou=operator,dc=fworch,dc=internal" }));
                // the hosting ldap is already internal, so no fan-out to the other internal ldaps happens
                Assert.That(otherInternalConnection.SearchCalls, Is.Empty);
            });
        }

        [Test]
        public void GetGroups_StopsBeforeConnecting_WhenCancelled()
        {
            TestableLdap hosting = CreateLdap(new RecordingLdapClient(), kExternalUserPath, kExternalGroupPath, kExternalLdapId);
            TestableLdap internalLdap = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new(new List<Ldap> { hosting, internalLdap });
            LdapEntry userEntry = LdapTestSupport.CreateEntry("cn=testuser,ou=users,dc=example,dc=com");
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => await resolver.GetGroups(userEntry, hosting, cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(hosting.ConnectCount, Is.Zero);
                Assert.That(internalLdap.ConnectCount, Is.Zero);
            });
        }

        [Test]
        public async Task GetGroups_FansOutToInternalLdapsForAnExternalUser()
        {
            string externalUserDn = "cn=testuser,ou=users,dc=example,dc=com";
            string externalGroupDn = $"cn=external-group,{kExternalGroupPath}";
            Ldap hosting = CreateLdap(new RecordingLdapClient(), kExternalUserPath, kExternalGroupPath, kExternalLdapId);
            Ldap internalLdap = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([hosting, internalLdap]);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(externalUserDn, new LdapAttribute("memberOf", externalGroupDn));

            List<string> groups = await resolver.GetGroups(userEntry, hosting);

            // the external hosting ldap triggers the internal fan-out; the memberOf groups survive it
            Assert.That(groups, Is.EqualTo(new List<string> { externalGroupDn }));
        }

        [Test]
        public async Task GetGroups_DoesNotFanOutToInactiveInternalLdap()
        {
            string externalUserDn = "cn=testuser,ou=users,dc=example,dc=com";
            string externalGroupDn = $"cn=external-group,{kExternalGroupPath}";
            Ldap hosting = CreateLdap(new RecordingLdapClient(), kExternalUserPath, kExternalGroupPath, kExternalLdapId);
            RecordingLdapClient inactiveConnection = new();
            Ldap inactiveInternal = CreateLdap(inactiveConnection, kInternalUserPath, kInternalGroupPath);
            inactiveInternal.Active = false;
            List<Ldap> configuredLdaps = new() { hosting, inactiveInternal };
            UserGroupResolver resolver = new(configuredLdaps);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(externalUserDn, new LdapAttribute("memberOf", externalGroupDn));

            List<string> groups = await resolver.GetGroups(userEntry, hosting);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.EqualTo(new List<string> { externalGroupDn }));
                Assert.That(inactiveConnection.SearchCalls, Is.Empty);
            });
        }

        [Test]
        public async Task GetGroups_DeduplicatesRepeatedMemberOfEntries()
        {
            string groupDn = $"cn=both,{kInternalGroupPath}";
            Ldap ldap = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([ldap]);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", new string[] { groupDn, groupDn.ToUpperInvariant() }));

            List<string> groups = await resolver.GetGroups(userEntry, ldap);

            // dn comparison is case insensitive, so the duplicate collapses into one entry
            Assert.That(groups, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetGroups_FallsBackToGroupWritePathWhenSearchPathIsEmpty()
        {
            string groupDn = $"cn=write-path-group,{kInternalGroupPath}";
            TestableLdap ldap = new(new RecordingLdapClient())
            {
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kInternalUserPath,
                GroupSearchPath = "",
                GroupWritePath = kInternalGroupPath
            };
            UserGroupResolver resolver = new([ldap]);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", groupDn));

            List<string> groups = await resolver.GetGroups(userEntry, ldap);

            Assert.That(groups, Is.EqualTo(new List<string> { groupDn }));
        }

        [Test]
        public async Task GetGroups_ReturnsEmptyWhenNoGroupPathIsConfigured()
        {
            TestableLdap ldap = new(new RecordingLdapClient())
            {
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kInternalUserPath,
                GroupSearchPath = "",
                GroupWritePath = ""
            };
            UserGroupResolver resolver = new([ldap]);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues));

            List<string> groups = await resolver.GetGroups(userEntry, ldap);

            // without a group path nothing can be matched, so no membership survives
            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroups_DropsGroupsOwnedByAnotherDirectory()
        {
            string externalUserDn = "cn=testuser,ou=users,dc=example,dc=com";
            Ldap hosting = CreateLdap(new RecordingLdapClient(), kExternalUserPath, kExternalGroupPath, kExternalLdapId);
            Ldap nested = CreateLdap(new RecordingLdapClient(), kNestedUserPath, kNestedGroupPath, kNestedLdapId);
            List<Ldap> configuredLdaps = [hosting, nested];
            UserGroupResolver resolver = new(configuredLdaps);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(externalUserDn, new LdapAttribute("memberOf", kOwnAndForeignGroupDns));

            List<string> groups = await resolver.GetGroups(userEntry, hosting);

            // the more specific group path of the nested connection owns the second group, so the hosting ldap cannot grant it
            Assert.That(groups, Is.EqualTo(new List<string> { kOwnGroupDn }));
        }

        private static TestableLdap CreateLdap(RecordingLdapClient connection, string userSearchPath, string groupSearchPath, int ldapId = kInternalLdapId)
        {
            return new TestableLdap(connection)
            {
                Id = ldapId,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = userSearchPath,
                GroupSearchPath = groupSearchPath
            };
        }

        private static FakeSearchResults GroupSearchResultFor(string groupName, string memberDn, string groupPath = kInternalGroupPath)
        {
            return LdapTestSupport.CreateSearchResults(
                LdapTestSupport.CreateEntry(
                    $"cn={groupName},{groupPath}",
                    new LdapAttribute("cn", groupName),
                    new LdapAttribute("uniqueMember", memberDn)));
        }
    }
}
