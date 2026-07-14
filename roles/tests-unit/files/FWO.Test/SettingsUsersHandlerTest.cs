using FWO.Data;
using FWO.Data.Middleware;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class SettingsUsersHandlerTest
    {
        [Test]
        public void BuildUsersByNormalizedDnGroupsEquivalentDns()
        {
            UiUser upperCase = new() { Name = "alice", Dn = "CN=Alice,OU=T1,DC=fwo" };
            UiUser lowerCase = new() { Name = "alice2", Dn = "cn=alice,ou=t1,dc=fwo" };
            UiUser other = new() { Name = "bob", Dn = "cn=bob,ou=t1,dc=fwo" };
            UiUser empty = new() { Name = "ghost", Dn = "" };

            Dictionary<string, List<UiUser>> result =
                SettingsUsersHandler.BuildUsersByNormalizedDn([upperCase, lowerCase, other, empty]);

            // Equivalent DNs that differ only in casing collapse to a single key.
            Assert.That(result, Has.Count.EqualTo(2));
            string aliceKey = DistName.NormalizeDnForComparison(upperCase.Dn);
            Assert.That(result[aliceKey], Has.Count.EqualTo(2));
            Assert.That(result[aliceKey], Does.Contain(upperCase).And.Contain(lowerCase));
            // Users with an empty DN are skipped.
            Assert.That(result.Values.SelectMany(u => u), Does.Not.Contain(empty));
        }

        [Test]
        public void AssignGroupsToUsersAssignsMembershipAndResetsPrevious()
        {
            UiUser alice = new() { Name = "alice", Dn = "cn=alice,ou=t1", Groups = ["stale"] };
            UiUser bob = new() { Name = "bob", Dn = "cn=bob,ou=t1" };
            List<UiUser> users = [alice, bob];

            UserGroup admins = new() { Name = "admins", Users = [new UiUser { Dn = "CN=Alice,OU=T1" }] };
            UserGroup auditors = new() { Name = "auditors", Users = [new UiUser { Dn = "cn=bob,ou=t1" }] };

            SettingsUsersHandler.AssignGroupsToUsers(users, [admins, auditors]);

            // Stale membership is cleared and matching is case-insensitive on the DN.
            Assert.That(alice.Groups, Is.EqualTo(new List<string> { "admins" }));
            Assert.That(bob.Groups, Is.EqualTo(new List<string> { "auditors" }));
        }

        [Test]
        public void AssignRolesToUsersAssignsMembership()
        {
            UiUser alice = new() { Name = "alice", Dn = "cn=alice,ou=t1", Roles = ["stale"] };
            List<UiUser> users = [alice];
            Role admin = new() { Name = "admin", Users = [new UiUser { Dn = "cn=alice,ou=t1" }] };
            Role reporter = new() { Name = "reporter", Users = [new UiUser { Dn = "cn=nobody,ou=t1" }] };

            SettingsUsersHandler.AssignRolesToUsers(users, [admin, reporter]);

            Assert.That(alice.Roles, Is.EqualTo(new List<string> { "admin" }));
        }

        [Test]
        public void MapApiUsersToUiUsersResolvesLdapAndTenant()
        {
            UiLdapConnection internalLdap = new() { Id = 5, Name = "internal" };
            List<UiLdapConnection> ldaps = [internalLdap];
            List<Tenant> tenants = [new Tenant { Id = 3, Name = "tenant3" }];
            List<UserGetReturnParameters> apiUsers =
            [
                new() { Name = "alice", UserDn = "cn=alice", LdapId = 5, TenantId = 3 }
            ];

            List<UiUser> result = SettingsUsersHandler.MapApiUsersToUiUsers(apiUsers, ldaps, tenants);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].LdapConnection, Is.SameAs(internalLdap));
            Assert.That(result[0].Tenant?.Name, Is.EqualTo("tenant3"));
        }

        [Test]
        public void MapApiUsersToUiUsersThrowsWhenLdapMissing()
        {
            List<UserGetReturnParameters> apiUsers =
            [
                new() { Name = "alice", UserDn = "cn=alice", LdapId = 99 }
            ];

            Assert.Throws<ArgumentNullException>(
                () => SettingsUsersHandler.MapApiUsersToUiUsers(apiUsers, [], []));
        }

        [Test]
        public void GetAvailableTenantsHonorsLdapConfiguration()
        {
            List<Tenant> tenants = [new Tenant { Id = 1, Name = "t1" }, new Tenant { Id = 2, Name = "t2" }];

            List<Tenant> fixedTenant = SettingsUsersHandler.GetAvailableTenants(
                new UiLdapConnection { TenantId = 2, TenantLevel = 0 }, tenants);
            Assert.That(fixedTenant.Select(t => t.Name), Is.EqualTo(new[] { "t2" }));

            List<Tenant> allTenants = SettingsUsersHandler.GetAvailableTenants(
                new UiLdapConnection { TenantId = null, TenantLevel = 1 }, tenants);
            Assert.That(allTenants, Has.Count.EqualTo(2));

            Assert.That(SettingsUsersHandler.GetAvailableTenants(null, tenants), Is.Empty);
        }

        [Test]
        public void BuildUserDnBuildsExpectedDistinguishedNames()
        {
            UiLdapConnection adLdap = new()
            {
                Type = (int)LdapType.ActiveDirectory,
                TenantLevel = 0,
                UserSearchPath = "ou=users,dc=fwo"
            };
            Assert.That(SettingsUsersHandler.BuildUserDn("alice", adLdap, null),
                Is.EqualTo("cn=alice,ou=users,dc=fwo"));

            UiLdapConnection openLdap = new()
            {
                Type = (int)LdapType.OpenLdap,
                TenantLevel = 1,
                UserSearchPath = "ou=users,dc=fwo"
            };
            Tenant tenant = new() { Id = 2, Name = "customer1" };
            Assert.That(SettingsUsersHandler.BuildUserDn("bob", openLdap, tenant),
                Is.EqualTo("uid=bob,ou=customer1,ou=users,dc=fwo"));
        }
    }
}
