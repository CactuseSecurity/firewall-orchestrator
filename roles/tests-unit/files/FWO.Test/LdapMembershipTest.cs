using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class LdapMembershipTest
    {
        [Test]
        public void GetGroupsIncludesWritePathMemberships()
        {
            Ldap ldap = new()
            {
                GroupSearchPath = "ou=search,dc=example,dc=com",
                GroupWritePath = "ou=write,dc=example,dc=com"
            };

            LdapAttributeSet attrs = new();
            attrs.Add(new LdapAttribute("memberOf", new[]
            {
                "cn=AppOwners,ou=write,dc=example,dc=com"
            }));
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
            attrs.Add(new LdapAttribute("memberOf", new[]
            {
                "cn=AppOwners,ou=search,dc=example,dc=com"
            }));
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
            attrs.Add(new LdapAttribute("memberOf", new[]
            {
                "cn=OtherGroup,ou=other,dc=example,dc=com"
            }));
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
    }
}
