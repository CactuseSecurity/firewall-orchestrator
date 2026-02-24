using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AuthLoggingHelperTest
    {
        [Test]
        public void FormatSelectedLdapContainsRelevantFields()
        {
            LdapConnectionBase ldap = new()
            {
                Id = 7,
                Address = "ldap.example.local",
                Port = 636,
                Type = (int)LdapType.OpenLdap,
                TenantId = 3
            };

            string formatted = AuthLoggingHelper.FormatSelectedLdap(ldap);

            Assert.That(formatted, Does.Contain("id=7"));
            Assert.That(formatted, Does.Contain("host=ldap.example.local:636"));
            Assert.That(formatted, Does.Contain($"type={(int)LdapType.OpenLdap}"));
            Assert.That(formatted, Does.Contain("tenant=3"));
        }

        [Test]
        public void FormatSelectedLdapHandlesNullAndDynamicTenant()
        {
            LdapConnectionBase ldap = new()
            {
                Id = 8,
                Address = "ldap.example.local",
                Port = 389,
                Type = (int)LdapType.Default,
                TenantId = null
            };

            string formattedNull = AuthLoggingHelper.FormatSelectedLdap(null);
            string formattedDynamicTenant = AuthLoggingHelper.FormatSelectedLdap(ldap);

            Assert.That(formattedNull, Is.EqualTo("ldap=<null>"));
            Assert.That(formattedDynamicTenant, Does.Contain("tenant=<dynamic>"));
        }

        [Test]
        public void FormatResolvedGroupsNormalizesAndSortsGroupDns()
        {
            string formatted = AuthLoggingHelper.FormatResolvedGroups(
            [
                "cn=Sec,ou=groups,dc=example,dc=com",
                "",
                "CN=App,ou=groups,dc=example,dc=com",
                "cn=app,ou=groups,dc=example,dc=com"
            ]);

            Assert.That(formatted, Does.StartWith("count=2, groups=["));
            Assert.That(formatted, Does.Contain("CN=App,ou=groups,dc=example,dc=com"));
            Assert.That(formatted, Does.Contain("cn=Sec,ou=groups,dc=example,dc=com"));
        }

        [Test]
        public void FormatResolvedGroupsHandlesNullInput()
        {
            string formatted = AuthLoggingHelper.FormatResolvedGroups(null);

            Assert.That(formatted, Is.EqualTo("count=0, groups=[]"));
        }
    }
}
