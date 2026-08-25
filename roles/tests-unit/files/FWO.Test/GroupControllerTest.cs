using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Novell.Directory.Ldap;
using MiddlewareLdap = FWO.Middleware.Server.Ldap;

namespace FWO.Test
{
    [TestFixture]
    internal class GroupControllerTest
    {
        private static readonly string kInternalUserSearchPath = "ou=users,dc=fworch,dc=internal";
        private static readonly string kGroupSearchPath = "ou=groups,dc=fworch,dc=internal";
        private static readonly string kGroupWritePath = "ou=groups,dc=fworch,dc=internal";
        private static readonly string kSearchUser = "cn=search,dc=fworch,dc=internal";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string kWriteUser = "cn=write,dc=fworch,dc=internal";
        private static readonly string kGroupDn = "cn=AppOwners,ou=groups,dc=fworch,dc=internal";
        private static readonly string kOtherGroupDn = "cn=OtherGroup,ou=groups,dc=fworch,dc=internal";
        private static readonly string kGroupMemberDn = "uid=groupmember,ou=users,dc=fworch,dc=internal";
        private static readonly string kDirectUserDn = "uid=direct,ou=users,dc=fworch,dc=internal";
        private static readonly string kUserDn = "uid=user,ou=users,dc=fworch,dc=internal";
        private static readonly string kUserName = "user";
        private static readonly string[] kUserValues = { kUserName };
        private static readonly string[] kUniqueMemberValues = { kUserDn };
        private static readonly string[] kGroupMemberValues = { kGroupMemberDn };
        private static readonly string[] kOwnerGroupValues = { "ownergroup" };
        private static readonly string[] kMembersValues = { kUserDn, "", kOtherGroupDn };

        [Test]
        public async Task Get_ReturnsInternalGroupObjectsFromLdap()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        kGroupDn,
                        new LdapAttribute("uniqueMember", kUniqueMemberValues),
                        new LdapAttribute("businessCategory", kOwnerGroupValues)))
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            ActionResult<List<GroupGetReturnParameters>> result = await controller.Get();

            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            List<GroupGetReturnParameters> groups = (List<GroupGetReturnParameters>)((OkObjectResult)result.Result!).Value!;
            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].GroupDn, Is.EqualTo(kGroupDn));
            Assert.That(groups[0].Members, Is.EqualTo(new List<string> { kUserDn }));
            Assert.That(groups[0].OwnerGroup, Is.True);
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetMembers_ReturnsMembersFromMatchingGroupLdap()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kGroupDn,
                    new LdapAttribute("uniqueMember", kMembersValues))
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            List<string> result = await controller.GetMembers(new GroupMemberGetParameters { GroupDn = kGroupDn });

            Assert.That(result, Is.EqualTo(new List<string> { kUserDn, kOtherGroupDn }));
            Assert.That(client.ReadCalls, Is.EqualTo(new List<string> { kGroupDn }));
        }

        [Test]
        public async Task GetMemberships_FallsBackToMemberDnSearchWhenUserEntryHasNoMemberships()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kUserDn,
                    new LdapAttribute("uid", kUserValues),
                    new LdapAttribute("memberOf", kGroupDn))
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            List<string> result = await controller.GetMemberships(new GroupMembershipGetParameters
            {
                UserDn = kUserDn
            });

            Assert.That(result, Is.EqualTo(new List<string> { kGroupDn }));
            Assert.That(client.ReadCalls, Is.EqualTo(new List<string> { kUserDn }));
            Assert.That(client.SearchCalls, Is.Empty);
        }

        [Test]
        public async Task ResolveMembers_ReturnsResolvedUsersAndKeepsDirectDns()
        {
            RecordingLdapClient client = new()
            {
                ReadResultsByDn =
                {
                    [kGroupDn] = LdapTestSupport.CreateEntry(
                        kGroupDn,
                        new LdapAttribute("uniqueMember", kGroupMemberValues))
                }
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            List<string> result = await controller.ResolveMembers(new GroupResolveParameters
            {
                Dns = new List<string> { kGroupDn, kDirectUserDn }
            });

            Assert.That(result, Is.EquivalentTo(new List<string> { kGroupMemberDn, kDirectUserDn }));
            Assert.That(result, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task AddUser_ReturnsTrueWhenWritableInternalGroupLdapMatches()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kGroupDn)
            };
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            bool result = await controller.AddUser(new GroupAddDeleteUserParameters
            {
                UserDn = kUserDn,
                GroupDn = kGroupDn
            });

            Assert.That(result, Is.True);
            Assert.That(client.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RemoveUser_ReturnsTrueWhenWritableInternalGroupLdapMatches()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kGroupDn,
                    new LdapAttribute("uniqueMember", kUniqueMemberValues))
            };
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            bool result = await controller.RemoveUser(new GroupAddDeleteUserParameters
            {
                UserDn = kUserDn,
                GroupDn = kGroupDn
            });

            Assert.That(result, Is.True);
            Assert.That(client.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Create_ReturnsGroupDnWhenWritableInternalGroupLdapAddsEntry()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            string result = await controller.Create(new GroupAddDeleteParameters
            {
                GroupName = "AppOwners",
                OwnerGroup = true
            });

            Assert.That(result, Is.EqualTo(kGroupDn));
            Assert.That(client.AddedEntries, Has.Count.EqualTo(1));
            Assert.That(client.AddedEntries[0].Dn, Is.EqualTo(kGroupDn));
        }

        [Test]
        public async Task Edit_ReturnsUpdatedGroupDnWhenWritableInternalGroupLdapRenamesEntry()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            string result = await controller.Edit(new GroupEditParameters
            {
                OldGroupName = "OldName",
                NewGroupName = "NewName"
            });

            Assert.That(result, Is.EqualTo("cn=NewName,ou=groups,dc=fworch,dc=internal"));
            Assert.That(client.RenameCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Delete_ReturnsTrueWhenWritableInternalGroupLdapDeletesEntry()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap });

            bool result = await controller.Delete(new GroupAddDeleteParameters
            {
                GroupName = "AppOwners"
            });

            Assert.That(result, Is.True);
            Assert.That(client.DeletedDns, Is.EqualTo(new List<string> { kGroupDn }));
        }

        private static TestableLdap CreateInternalGroupLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 1,
                Address = "ldap.example.test",
                Port = 389,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kInternalUserSearchPath,
                GroupSearchPath = kGroupSearchPath,
                GroupWritePath = kGroupWritePath
            };
        }

        private static TestableLdap CreateInternalWritableGroupLdap(RecordingLdapClient client)
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
                UserSearchPath = kInternalUserSearchPath,
                GroupSearchPath = kGroupSearchPath,
                GroupWritePath = kGroupWritePath
            };
        }
    }
}
