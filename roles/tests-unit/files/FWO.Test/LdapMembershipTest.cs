using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class LdapMembershipTest
    {
        private static readonly string[] kWritePathMembers = ["cn=AppOwners,ou=write,dc=example,dc=com"];
        private static readonly string[] kSearchPathMembers = ["cn=AppOwners,ou=search,dc=example,dc=com"];
        private static readonly string[] kUnrelatedMembers = ["cn=OtherGroup,ou=other,dc=example,dc=com"];
        private static readonly string[] kGroupNames = ["AppOwners", "SecTeam"];
        private static readonly string[] kMixedGroupNames = ["AppOwners", "cn=AppOwners,ou=groups,dc=example,dc=com", "APPOWNERS"];
        private static readonly string[] kSingleGroupName = ["AppOwners"];
        private static readonly string[] kResolvedDns = ["uid=user,ou=users,dc=example,dc=com", "cn=group,ou=groups,dc=example,dc=com"];
        private static readonly string[] kDirectUserDns = ["uid=user,ou=users,dc=example,dc=com", "UID=USER,ou=users,dc=example,dc=com", "", "cn=group,ou=groups,dc=example,dc=com"];
        private static readonly string[] kMemberOfDns = ["cn=AppOwners,ou=groups,dc=example,dc=com", "cn=SecTeam,ou=groups,dc=example,dc=com"];
        private static readonly string kGroupSearchPath = "ou=groups,dc=example,dc=com";
        private static readonly string kUserSearchPath = "ou=users,dc=example,dc=com";
        private static readonly string kNestedGroupDn = "cn=Nested,ou=groups,dc=example,dc=com";
        private static readonly string kRootGroupDn = "cn=Root,ou=groups,dc=example,dc=com";
        private static readonly string kResolvedUserDn = "uid=resolved,ou=users,dc=example,dc=com";
        private static readonly string kAnotherResolvedUserDn = "uid=other,ou=users,dc=example,dc=com";
        private static readonly string kEscapedCommaUserDn = "uid=last\\,first,ou=users,dc=example,dc=com";
        private static readonly string kEscapedCommaMemberDn = "uid=last\\2cfirst,ou=users,dc=example,dc=com";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly List<string> kSingleResolvedUserDns = [kResolvedUserDn];
        private static readonly string[] kAppOwnerCn = ["AppOwners"];
        private static readonly List<string> kEscapedCommaUserDnsList = [kEscapedCommaUserDn];
        private static readonly string[] kEscapedCommaMemberValues = [kEscapedCommaMemberDn];
        private static readonly string[] kEscapedGroupCn = ["Escaped"];
        private static readonly string[] kRootGroupDns = [kRootGroupDn, kResolvedUserDn];
        private static readonly string[] kResolvedUserValues = [kNestedGroupDn, kResolvedUserDn];
        private static readonly string[] kAnotherResolvedUserValues = [kAnotherResolvedUserDn];
        private static readonly string[] kMemberWithBlankValues = [kResolvedUserDn, ""];
        private static readonly string[] kOwnerGroupValues = ["ownergroup"];
        private static readonly string[] kPlainDescriptionValues = ["plain"];
        private static readonly string[] kRoleDescriptionValues = ["Application owners"];
        private static readonly string[] kResolvedUsersExpected = [kResolvedUserDn, kAnotherResolvedUserDn];

        [Test]
        public void GetGroupsIncludesWritePathMemberships()
        {
            Ldap ldap = new()
            {
                GroupSearchPath = "ou=search,dc=example,dc=com",
                GroupWritePath = "ou=write,dc=example,dc=com"
            };

            LdapAttributeSet attrs = new();
            attrs.Add(new LdapAttribute("memberOf", kWritePathMembers));
            LdapEntry user = new("cn=test,dc=example,dc=com", attrs);

            var groups = ldap.GetGroups(user);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0], Is.EqualTo("cn=AppOwners,ou=write,dc=example,dc=com"));
        }

        [Test]
        public void GetGroupsIncludesSearchPathMemberships()
        {
            Ldap ldap = new()
            {
                GroupSearchPath = "ou=search,dc=example,dc=com",
                GroupWritePath = "ou=write,dc=example,dc=com"
            };

            LdapAttributeSet attrs = new();
            attrs.Add(new LdapAttribute("memberOf", kSearchPathMembers));
            LdapEntry user = new("cn=test,dc=example,dc=com", attrs);

            var groups = ldap.GetGroups(user);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0], Is.EqualTo("cn=AppOwners,ou=search,dc=example,dc=com"));
        }

        [Test]
        public void GetGroupsIgnoresUnrelatedMemberships()
        {
            Ldap ldap = new()
            {
                GroupSearchPath = "ou=search,dc=example,dc=com",
                GroupWritePath = "ou=write,dc=example,dc=com"
            };

            LdapAttributeSet attrs = new();
            attrs.Add(new LdapAttribute("memberOf", kUnrelatedMembers));
            LdapEntry user = new("cn=test,dc=example,dc=com", attrs);

            var groups = ldap.GetGroups(user);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public void HasGroupHandlingUsesWritePathFallback()
        {
            Ldap ldap = new()
            {
                GroupSearchPath = "",
                GroupWritePath = "ou=write,dc=example,dc=com"
            };

            Assert.That(ldap.HasGroupHandling(), Is.True);
        }

        [Test]
        public void BuildGroupDnsCreatesDnsFromNames()
        {
            var dns = Ldap.BuildGroupDns(kGroupNames, "ou=groups,dc=example,dc=com");

            Assert.That(dns, Has.Count.EqualTo(2));
            Assert.That(dns, Does.Contain("cn=AppOwners,ou=groups,dc=example,dc=com"));
            Assert.That(dns, Does.Contain("cn=SecTeam,ou=groups,dc=example,dc=com"));
        }

        [Test]
        public void BuildGroupDnsKeepsExistingDnsAndDeduplicates()
        {
            var dns = Ldap.BuildGroupDns(kMixedGroupNames, "ou=groups,dc=example,dc=com");

            Assert.That(dns, Has.Count.EqualTo(1));
            Assert.That(dns, Does.Contain("cn=AppOwners,ou=groups,dc=example,dc=com"));
        }

        [Test]
        public void BuildGroupDnsReturnsEmptyWhenPathMissing()
        {
            var dns = Ldap.BuildGroupDns(kSingleGroupName, "");

            Assert.That(dns, Is.Empty);
        }

        [Test]
        public async Task ResolveUsersFromDns_ReturnsDistinctDirectUsersWhenSearchPathsAreMissing()
        {
            Ldap ldap = new()
            {
                UserSearchPath = "",
                GroupSearchPath = ""
            };

            List<string> resolved = await ldap.ResolveUsersFromDns(kDirectUserDns);

            Assert.That(resolved, Is.EquivalentTo(kResolvedDns));
            Assert.That(resolved, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ResolveUsersFromDns_ReturnsEmptyForNullInput()
        {
            Ldap ldap = new();

            List<string> resolved = await ldap.ResolveUsersFromDns(null!);

            Assert.That(resolved, Is.Empty);
        }

        [Test]
        public async Task GetGroups_ReturnsEmptyWhenSearchPathIsMissing()
        {
            TestableLdap ldap = new(new RecordingLdapClient())
            {
                GroupSearchPath = "",
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword
            };

            List<string> groups = await ldap.GetGroups(kSingleResolvedUserDns);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public async Task GetGroups_ReturnsEmptyWhenSearchPasswordCannotBeDecrypted()
        {
            RecordingLdapClient client = new();
            TestableLdap ldap = new(client)
            {
                GroupSearchPath = kGroupSearchPath,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = string.Empty
            };

            List<string> groups = await ldap.GetGroups(kSingleResolvedUserDns);

            Assert.That(groups, Is.Empty);
            Assert.That(client.SearchCalls, Is.Empty);
        }

        [Test]
        public async Task ResolveUsersFromDns_ExpandsNestedGroupsAndKeepsDirectUsers()
        {
            RecordingLdapClient client = new()
            {
                ReadResultsByDn =
                {
                    [kRootGroupDn] = LdapTestSupport.CreateEntry(
                        kRootGroupDn,
                        new LdapAttribute("uniqueMember", kResolvedUserValues)),
                    [kNestedGroupDn] = LdapTestSupport.CreateEntry(
                        kNestedGroupDn,
                        new LdapAttribute("uniqueMember", kAnotherResolvedUserValues))
                }
            };
            TestableLdap ldap = new(client)
            {
                UserSearchPath = kUserSearchPath,
                GroupSearchPath = kGroupSearchPath
            };

            List<string> resolved = await ldap.ResolveUsersFromDns(kRootGroupDns);

            Assert.That(resolved, Is.EquivalentTo(kResolvedUsersExpected));
            Assert.That(client.ReadCalls, Is.EqualTo(new List<string> { kRootGroupDn, kNestedGroupDn }));
        }

        [Test]
        public async Task GetGroups_ReturnsMatchingMembershipsFromSearchResults()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "cn=AppOwners,ou=groups,dc=example,dc=com",
                        new LdapAttribute("cn", kAppOwnerCn),
                        new LdapAttribute("uniqueMember", kMemberWithBlankValues)))
            };
            TestableLdap ldap = new(client)
            {
                GroupSearchPath = kGroupSearchPath,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword
            };

            List<string> groups = [];
            await InvokePrivateAsync(
                ldap,
                "SearchAndCollectMemberships",
                client,
                kGroupSearchPath,
                kSingleResolvedUserDns,
                groups);

            Assert.That(groups, Is.EqualTo(kAppOwnerCn));
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetGroups_MatchesEscapedCommasInMemberDns()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "cn=Escaped,ou=groups,dc=example,dc=com",
                        new LdapAttribute("cn", kEscapedGroupCn),
                        new LdapAttribute("uniqueMember", kEscapedCommaMemberValues)))
            };
            TestableLdap ldap = new(client)
            {
                GroupSearchPath = kGroupSearchPath,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword
            };

            List<string> groups = [];
            await InvokePrivateAsync(
                ldap,
                "SearchAndCollectMemberships",
                client,
                kGroupSearchPath,
                kEscapedCommaUserDnsList,
                groups);

            Assert.That(groups, Is.EqualTo(kEscapedGroupCn));
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetRoles_ReturnsMatchingMembershipsFromSearchResults()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "cn=AppOwners,ou=roles,dc=example,dc=com",
                        new LdapAttribute("cn", kAppOwnerCn),
                        new LdapAttribute("uniqueMember", kMemberWithBlankValues)))
            };
            TestableLdap ldap = new(client)
            {
                RoleSearchPath = "ou=roles,dc=example,dc=com",
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword
            };

            List<string> roles = [];
            await InvokePrivateAsync(
                ldap,
                "SearchAndCollectMemberships",
                client,
                "ou=roles,dc=example,dc=com",
                kSingleResolvedUserDns,
                roles);

            Assert.That(roles, Is.EqualTo(kAppOwnerCn));
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetRoles_ReturnsEmptyWhenSearchResultsAreNull()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = null
            };
            TestableLdap ldap = new(client)
            {
                RoleSearchPath = "ou=roles,dc=example,dc=com",
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword
            };

            List<string> roles = [];
            await InvokePrivateAsync(
                ldap,
                "SearchAndCollectMemberships",
                client,
                "ou=roles,dc=example,dc=com",
                kSingleResolvedUserDns,
                roles);

            Assert.That(roles, Is.Empty);
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetGroups_ReturnsEmptyWhenSearchThrows()
        {
            RecordingLdapClient client = new()
            {
                SearchResponder = (_, _, _, _, _) => throw new InvalidOperationException("boom")
            };
            TestableLdap ldap = new(client)
            {
                GroupSearchPath = kGroupSearchPath,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword
            };

            List<string> groups = [];

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await InvokePrivateAsync(
                    ldap,
                    "SearchAndCollectMemberships",
                    client,
                    kGroupSearchPath,
                    kSingleResolvedUserDns,
                    groups));

            Assert.That(groups, Is.Empty);
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetGroupsOfUser_ReturnsMemberOfDns()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "uid=user,ou=users,dc=example,dc=com",
                        new LdapAttribute("memberOf", kMemberOfDns)))
            };
            TestableLdap ldap = new(client)
            {
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kUserSearchPath
            };

            List<string> groups = await ldap.GetGroupsOfUser("user");

            Assert.That(groups, Is.EqualTo(new List<string> { kMemberOfDns[0], kMemberOfDns[1] }));
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetAllGroupObjects_UsesActiveDirectoryMemberAttribute()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "cn=ad-group,ou=groups,dc=example,dc=com",
                        new LdapAttribute("member", kMemberWithBlankValues),
                        new LdapAttribute("businessCategory", kOwnerGroupValues)))
            };
            TestableLdap ldap = new(client)
            {
                Type = (int)LdapType.ActiveDirectory,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
                GroupSearchPath = kGroupSearchPath
            };

            List<GroupGetReturnParameters> groups = await ldap.GetAllGroupObjects("ad-group");

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].Members, Is.EqualTo(new List<string> { kResolvedUserDn }));
            Assert.That(groups[0].OwnerGroup, Is.True);
        }

        [Test]
        public async Task GetAllGroupObjects_ReturnsEmptyMembersWhenNoMemberAttributeExists()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "cn=plain-group,ou=groups,dc=example,dc=com",
                        new LdapAttribute("description", kPlainDescriptionValues)))
            };
            TestableLdap ldap = new(client)
            {
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
                GroupSearchPath = kGroupSearchPath
            };

            List<GroupGetReturnParameters> groups = await ldap.GetAllGroupObjects("plain");

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].Members, Is.Empty);
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public void GetMemberKey_ReturnsMemberForActiveDirectoryAndUniqueMemberOtherwise()
        {
            Ldap ldap = new()
            {
                Type = (int)LdapType.ActiveDirectory
            };

            Assert.That(InvokePrivate<string>(ldap, "GetMemberKey"), Is.EqualTo("member"));

            ldap.Type = (int)LdapType.Default;

            Assert.That(InvokePrivate<string>(ldap, "GetMemberKey"), Is.EqualTo("uniqueMember"));
        }

        [Test]
        public void IsGroupDnAndIsUserDn_UseConfiguredSearchPaths()
        {
            Ldap ldap = new()
            {
                GroupSearchPath = "ou=groups,dc=example,dc=com",
                UserSearchPath = "ou=users,dc=example,dc=com"
            };

            Assert.Multiple(() =>
            {
                Assert.That(InvokePrivate<bool>(ldap, "IsGroupDn", "cn=team,ou=groups,dc=example,dc=com"), Is.True);
                Assert.That(InvokePrivate<bool>(ldap, "IsGroupDn", "uid=user,ou=users,dc=example,dc=com"), Is.False);
                Assert.That(InvokePrivate<bool>(ldap, "IsUserDn", "uid=user,ou=users,dc=example,dc=com"), Is.True);
                Assert.That(InvokePrivate<bool>(ldap, "IsUserDn", "cn=team,ou=groups,dc=example,dc=com"), Is.False);
            });
        }

        [Test]
        public async Task GetAllRoles_ReturnsEmptyWhenRoleHandlingIsDisabled()
        {
            Ldap ldap = new()
            {
                RoleSearchPath = ""
            };

            List<RoleGetReturnParameters> roles = await ldap.GetAllRoles();

            Assert.That(roles, Is.Empty);
        }

        [Test]
        public async Task GetAllRoles_ReturnsRoleWithMembersAndDescription()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "cn=AppOwners,ou=roles,dc=example,dc=com",
                        new LdapAttribute("description", kRoleDescriptionValues),
                        new LdapAttribute("uniqueMember", kMemberWithBlankValues)))
            };
            TestableLdap ldap = new(client)
            {
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
                RoleSearchPath = "ou=roles,dc=example,dc=com"
            };

            List<RoleGetReturnParameters> roles = await ldap.GetAllRoles();

            Assert.That(roles, Has.Count.EqualTo(1));
            Assert.That(roles[0].Attributes, Has.Count.EqualTo(2));
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RemoveUserFromAllEntries_ReturnsTrueWhenNoMembershipHandlingIsConfigured()
        {
            Ldap ldap = new()
            {
                RoleSearchPath = "",
                GroupSearchPath = "",
                GroupWritePath = ""
            };

            bool removed = await ldap.RemoveUserFromAllEntries("uid=user,ou=users,dc=example,dc=com");

            Assert.That(removed, Is.True);
        }

        private static T InvokePrivate<T>(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = FindPrivateInstanceMethod(instance.GetType(), methodName)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            return (T)method.Invoke(instance, parameters)!;
        }

        private static async Task InvokePrivateAsync(object instance, string methodName, params object?[] parameters)
        {
            MethodInfo method = FindPrivateInstanceMethod(instance.GetType(), methodName)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            object? result = method.Invoke(instance, parameters);
            if (result is Task task)
            {
                await task;
                return;
            }

            throw new InvalidOperationException($"Unexpected task type for {methodName}.");
        }

        private static MethodInfo? FindPrivateInstanceMethod(Type type, string methodName)
        {
            for (Type? currentType = type; currentType != null; currentType = currentType.BaseType)
            {
                MethodInfo? method = currentType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
