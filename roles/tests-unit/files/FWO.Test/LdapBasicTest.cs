using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class LdapBasicTest
    {
        [TestCase(true, LdapModification.Add, false)]
        [TestCase(false, LdapModification.Add, true)]
        [TestCase(true, LdapModification.Delete, true)]
        [TestCase(false, LdapModification.Delete, false)]
        public void ShouldModifyMembershipHandlesAddAndDelete(bool memberExists, int modification, bool expected)
        {
            bool result = Ldap.ShouldModifyMembership(memberExists, modification);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(@"cn=Müller \2C (xy),ou=users,dc=example,dc=com", @"cn=Müller \, (xy),ou=users,dc=example,dc=com")]
        [TestCase(@"cn=M\C3\BCller \2C (xy),ou=users,dc=example,dc=com", @"cn=Müller \, (xy),ou=users,dc=example,dc=com")]
        [TestCase(@"CN=User\, Example,OU=Users,DC=Example,DC=COM", @"cn=User\2C Example,ou=users,dc=example,dc=com")]
        public void NormalizeDnForComparison_TreatsEquivalentEscapedDnsAsEqual(string leftDn, string rightDn)
        {
            string normalizedLeft = Ldap.NormalizeDnForComparison(leftDn);
            string normalizedRight = Ldap.NormalizeDnForComparison(rightDn);

            Assert.That(normalizedLeft, Is.EqualTo(normalizedRight));
        }
    }
}
