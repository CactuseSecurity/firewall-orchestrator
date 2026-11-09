using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class LdapTenantTest
    {
        private static readonly string kUserSearchPath = "ou=users,dc=example,dc=com";

        [Test]
        public async Task AddTenant_AddsOrganizationalUnitAtDerivedDn()
        {
            RecordingLdapClient client = new();
            TestableLdap ldap = new(client)
            {
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd",
                UserSearchPath = kUserSearchPath
            };

            bool result = await ldap.AddTenant("tenant-a");

            Assert.That(result, Is.True);
            Assert.That(client.AddedEntries, Has.Count.EqualTo(1));
            Assert.That(client.AddedEntries[0].Dn, Is.EqualTo("ou=tenant-a,ou=users,dc=example,dc=com"));
        }

        [Test]
        public async Task DeleteTenant_DeletesDerivedDn()
        {
            RecordingLdapClient client = new();
            TestableLdap ldap = new(client)
            {
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd",
                UserSearchPath = kUserSearchPath
            };

            bool result = await ldap.DeleteTenant("tenant-a");

            Assert.That(result, Is.True);
            Assert.That(client.DeletedDns, Is.EqualTo(new List<string> { "ou=tenant-a,ou=users,dc=example,dc=com" }));
        }

        [Test]
        public async Task AddTenant_ReturnsFalseWhenWriteOperationThrows()
        {
            RecordingLdapClient client = new()
            {
                ThrowOnAdd = true
            };
            TestableLdap ldap = new(client)
            {
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd",
                UserSearchPath = kUserSearchPath
            };

            bool result = await ldap.AddTenant("tenant-a");

            Assert.That(result, Is.False);
        }
    }
}
