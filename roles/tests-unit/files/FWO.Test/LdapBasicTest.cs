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
    }
}
