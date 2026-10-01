using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// A dn is unique only inside its LDAP connection, so a user is the same directory account only
    /// when both the connection and the dn match (SEC-11).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiUserDirectoryAccountTest
    {
        [Test]
        public void IsSameDirectoryAccount_WithSameLdapConnectionAndEquivalentDn_IsTrue()
        {
            UiUser user = new() { Dn = "CN=Alice,OU=T1,DC=fwo", LdapConnection = new() { Id = 1 } };
            UiUser sameAccount = new() { Dn = "cn=alice,ou=t1,dc=fwo", LdapConnection = new() { Id = 1 } };

            Assert.That(user.IsSameDirectoryAccount(sameAccount), Is.True);
        }

        [Test]
        public void IsSameDirectoryAccount_WithSameDnInAnotherLdapConnection_IsFalse()
        {
            UiUser user = new() { Dn = "cn=alice,ou=t1,dc=fwo", LdapConnection = new() { Id = 1 } };
            UiUser otherDirectoryUser = new() { Dn = "cn=alice,ou=t1,dc=fwo", LdapConnection = new() { Id = 2 } };

            Assert.That(user.IsSameDirectoryAccount(otherDirectoryUser), Is.False);
        }

        [Test]
        public void IsSameDirectoryAccount_WithOtherDnInSameLdapConnection_IsFalse()
        {
            UiUser user = new() { Dn = "cn=alice,ou=t1,dc=fwo", LdapConnection = new() { Id = 1 } };
            UiUser otherAccount = new() { Dn = "cn=bob,ou=t1,dc=fwo", LdapConnection = new() { Id = 1 } };

            Assert.That(user.IsSameDirectoryAccount(otherAccount), Is.False);
        }

        [Test]
        public void IsSameDirectoryAccount_WithoutLdapConnectionOnOneSide_IsFalse()
        {
            UiUser user = new() { Dn = "cn=alice,ou=t1,dc=fwo", LdapConnection = new() { Id = 1 } };
            UiUser unboundUser = new() { Dn = "cn=alice,ou=t1,dc=fwo", LdapConnection = null! };

            Assert.That(user.IsSameDirectoryAccount(unboundUser), Is.False);
        }
    }
}
