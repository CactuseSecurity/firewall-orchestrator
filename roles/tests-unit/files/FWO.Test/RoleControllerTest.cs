using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using NUnit.Framework;
using Novell.Directory.Ldap;
using MiddlewareLdap = FWO.Middleware.Server.Ldap;

namespace FWO.Test
{
    [TestFixture]
    internal class RoleControllerTest
    {
        private static readonly string kRoleSearchPath = "ou=roles,dc=fworch,dc=internal";
        private static readonly string kInternalUserSearchPath = "ou=users,dc=fworch,dc=internal";
        private static readonly string kSearchUser = "cn=search,dc=fworch,dc=internal";
        private static readonly string kWriteUser = "cn=write,dc=fworch,dc=internal";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string kRoleDn = "cn=AppOwners,ou=roles,dc=fworch,dc=internal";
        private static readonly string kRoleMemberDn = "uid=user1,ou=users,dc=fworch,dc=internal";
        private static readonly string[] kRoleDescription = { "Application owners" };
        private static readonly string[] kRoleMembers = { kRoleMemberDn };

        [Test]
        public async Task Get_ReturnsRolesFromLdap()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        kRoleDn,
                        new LdapAttribute("description", kRoleDescription),
                        new LdapAttribute("uniqueMember", kRoleMembers)))
            };
            MiddlewareLdap ldap = CreateRoleLdap(client);
            RoleController controller = new(new List<MiddlewareLdap> { ldap });

            List<RoleGetReturnParameters> result = await controller.Get();

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Role, Is.EqualTo(kRoleDn));
            Assert.That(result[0].Attributes, Has.Count.EqualTo(2));
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AddUser_ReturnsTrueWhenWritableRoleLdapMatches()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kRoleDn)
            };
            MiddlewareLdap ldap = CreateWritableRoleLdap(client);
            RoleController controller = new(new List<MiddlewareLdap> { ldap });

            bool result = await controller.AddUser(new RoleAddDeleteUserParameters
            {
                UserDn = "uid=user1,ou=users,dc=fworch,dc=internal",
                Role = kRoleDn
            });

            Assert.That(result, Is.True);
            Assert.That(client.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RemoveUser_ReturnsTrueWhenWritableRoleLdapMatches()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kRoleDn,
                    new LdapAttribute("uniqueMember", kRoleMembers))
            };
            MiddlewareLdap ldap = CreateWritableRoleLdap(client);
            RoleController controller = new(new List<MiddlewareLdap> { ldap });

            bool result = await controller.RemoveUser(new RoleAddDeleteUserParameters
            {
                UserDn = "uid=user1,ou=users,dc=fworch,dc=internal",
                Role = kRoleDn
            });

            Assert.That(result, Is.True);
            Assert.That(client.ModifyCalls, Has.Count.EqualTo(1));
        }

        private static TestableLdap CreateRoleLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 1,
                Address = "ldap.example.test",
                Port = 389,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kInternalUserSearchPath,
                RoleSearchPath = kRoleSearchPath
            };
        }

        private static TestableLdap CreateWritableRoleLdap(RecordingLdapClient client)
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
                RoleSearchPath = kRoleSearchPath
            };
        }
    }
}
