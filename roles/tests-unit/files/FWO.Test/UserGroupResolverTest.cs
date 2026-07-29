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

        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly List<Ldap> kNoLdaps = [];
        private static readonly string[] kMemberOfValues =
        [
            "cn=direct-group,ou=groups,ou=operator,dc=fworch,dc=internal",
            "cn=unrelated,ou=elsewhere,dc=example,dc=com"
        ];

        [Test]
        public async Task GetGroupsForUserDn_ReturnsEmptyForMissingDn()
        {
            UserGroupResolver resolver = new(kNoLdaps);

            List<string> groups = await resolver.GetGroupsForUserDn("");

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupsForUserDn_ReturnsEmptyForWhitespaceDn()
        {
            UserGroupResolver resolver = new(kNoLdaps);

            List<string> groups = await resolver.GetGroupsForUserDn("   ");

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupsForUserDn_ReturnsEmptyWhenNoLdapKnowsTheUser()
        {
            // ReadAsync returns null, so the user cannot be located in any configured ldap
            Ldap ldap = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([ldap]);

            List<string> groups = await resolver.GetGroupsForUserDn(kUserDn);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroupsForUserDn_ResolvesMemberOfGroupsWithinTheConfiguredGroupPath()
        {
            RecordingLdapClient connection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues))
            };
            Ldap ldap = CreateLdap(connection, kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([ldap]);

            List<string> groups = await resolver.GetGroupsForUserDn(kUserDn);

            // only the group inside the configured group path is kept, the unrelated one is dropped
            Assert.That(groups, Is.EqualTo(new List<string> { "cn=direct-group,ou=groups,ou=operator,dc=fworch,dc=internal" }));
        }

        [Test]
        public async Task GetGroupsForUserDn_ReturnsEmptyWhenTheUserHasNoMemberships()
        {
            RecordingLdapClient connection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn)
            };
            Ldap ldap = CreateLdap(connection, kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([ldap]);

            List<string> groups = await resolver.GetGroupsForUserDn(kUserDn);

            Assert.Multiple(() =>
            {
                Assert.That(groups, Is.Empty);
                Assert.That(connection.ReadCalls, Does.Contain(kUserDn));
            });
        }

        [Test]
        public async Task GetGroupsForUserDn_SkipsLdapsThatDoNotKnowTheUser()
        {
            Ldap unknowing = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            RecordingLdapClient knowingConnection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("memberOf", kMemberOfValues))
            };
            Ldap knowing = CreateLdap(knowingConnection, kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([unknowing, knowing]);

            List<string> groups = await resolver.GetGroupsForUserDn(kUserDn);

            Assert.That(groups, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetGroups_ResolvesMemberOfForAnInternalUserWithoutFanOut()
        {
            Ldap hosting = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            RecordingLdapClient otherInternalConnection = new();
            Ldap otherInternal = CreateLdap(otherInternalConnection, kInternalUserPath, kInternalGroupPath);
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
        public async Task GetGroups_FansOutToInternalLdapsForAnExternalUser()
        {
            string externalUserDn = "cn=testuser,ou=users,dc=example,dc=com";
            string externalGroupDn = $"cn=external-group,{kExternalGroupPath}";
            Ldap hosting = CreateLdap(new RecordingLdapClient(), kExternalUserPath, kExternalGroupPath);
            Ldap internalLdap = CreateLdap(new RecordingLdapClient(), kInternalUserPath, kInternalGroupPath);
            UserGroupResolver resolver = new([hosting, internalLdap]);
            LdapEntry userEntry = LdapTestSupport.CreateEntry(externalUserDn, new LdapAttribute("memberOf", externalGroupDn));

            List<string> groups = await resolver.GetGroups(userEntry, hosting);

            // the external hosting ldap triggers the internal fan-out; the memberOf groups survive it
            Assert.That(groups, Is.EqualTo(new List<string> { externalGroupDn }));
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

        private static TestableLdap CreateLdap(RecordingLdapClient connection, string userSearchPath, string groupSearchPath)
        {
            return new TestableLdap(connection)
            {
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
