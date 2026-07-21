using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;
using System.Reflection;

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

        [Test]
        public void EscapeFilterValue_EncodesReservedCharacters()
        {
            string escaped = Ldap.EscapeFilterValue("a\\b*(c)\0");

            Assert.That(escaped, Is.EqualTo(@"a\5cb\2a\28c\29\00"));
        }

        [Test]
        public void EscapeSearchPattern_EscapesSegmentsButKeepsWildcards()
        {
            string escaped = Ldap.EscapeSearchPattern("cn=User*(test)");

            Assert.That(escaped, Is.EqualTo(@"cn=User*\28test\29"));
        }

        [Test]
        public void AttributeHelpers_ReturnExpectedValuesAndFallbacks()
        {
            LdapEntry richUser = Entry("uid=user,ou=users,dc=example,dc=com",
                ("mail", ["user@example.test"]),
                ("givenName", ["Ada"]),
                ("sn", ["Lovelace"]),
                ("sAMAccountName", ["adal"]),
                ("uid", ["fallback"]));

            LdapEntry uidOnlyUser = Entry("uid=other,ou=users,dc=example,dc=com",
                ("uid", ["otheruser"]));

            LdapEntry emptyUser = Entry("uid=empty,ou=users,dc=example,dc=com");

            Assert.Multiple(() =>
            {
                Assert.That(Ldap.GetEmail(richUser), Is.EqualTo("user@example.test"));
                Assert.That(Ldap.GetFirstName(richUser), Is.EqualTo("Ada"));
                Assert.That(Ldap.GetLastName(richUser), Is.EqualTo("Lovelace"));
                Assert.That(Ldap.GetName(richUser), Is.EqualTo("adal"));
                Assert.That(Ldap.GetName(uidOnlyUser), Is.EqualTo("otheruser"));
                Assert.That(Ldap.GetEmail(emptyUser), Is.Empty);
                Assert.That(Ldap.GetFirstName(emptyUser), Is.Empty);
                Assert.That(Ldap.GetLastName(emptyUser), Is.Empty);
                Assert.That(Ldap.GetName(emptyUser), Is.Empty);
            });
        }

        [Test]
        public void IsGroupEntry_DetectsMembershipAttributesAndObjectClasses()
        {
            LdapEntry memberGroup = Entry("cn=member-group,dc=example,dc=com",
                ("member", ["uid=user,dc=example,dc=com"]));
            LdapEntry uniqueMemberGroup = Entry("cn=unique-group,dc=example,dc=com",
                ("uniqueMember", ["uid=user,dc=example,dc=com"]));
            LdapEntry objectClassGroup = Entry("cn=class-group,dc=example,dc=com",
                ("objectClass", ["top", "groupOfUniqueNames"]));
            LdapEntry plainUser = Entry("uid=user,dc=example,dc=com",
                ("objectClass", ["person"]));

            Assert.Multiple(() =>
            {
                Assert.That(Ldap.IsGroupEntry(memberGroup), Is.True);
                Assert.That(Ldap.IsGroupEntry(uniqueMemberGroup), Is.True);
                Assert.That(Ldap.IsGroupEntry(objectClassGroup), Is.True);
                Assert.That(Ldap.IsGroupEntry(plainUser), Is.False);
            });
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

        [Test]
        public void EnableReferralFollowing_SetsConnectionConstraint()
        {
            LdapConnection connection = new();
            MethodInfo? method = typeof(Ldap).GetMethod("EnableReferralFollowing", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);

            method!.Invoke(null, [connection]);

            Assert.That(connection.Constraints.ReferralFollowing, Is.True);
        }

        [Test]
        public async Task LookupOperations_ReturnFallbackValuesWhenConnectionFails()
        {
            Ldap ldap = FailingLdap();
            UiUser user = new()
            {
                Name = "user",
                Dn = "uid=user,ou=users,dc=example,dc=com",
                Password = "secret"
            };

            LdapEntry? details = await ldap.GetUserDetailsFromLdap(user.Dn);
            LdapEntry? entry = await ldap.GetLdapEntry(user, validateCredentials: false);
            List<LdapUserGetReturnParameters> allUsers = await ldap.GetAllUsers("user");

            Assert.Multiple(() =>
            {
                Assert.That(details, Is.Null);
                Assert.That(entry, Is.Null);
                Assert.That(allUsers, Is.Empty);
            });
        }

        [Test]
        public async Task MutatingOperations_ReturnFallbackValuesWhenConnectionFails()
        {
            Ldap ldap = FailingLdap();

            string changePassword = await ldap.ChangePassword("uid=user,ou=users,dc=example,dc=com", "old", "new");
            string setPassword = await ldap.SetPassword("uid=user,ou=users,dc=example,dc=com", "new");
            bool added = await ldap.AddUser("uid=user,ou=users,dc=example,dc=com", "pw", "user@example.test");
            bool updated = await ldap.UpdateUser("uid=user,ou=users,dc=example,dc=com", "user@example.test");
            bool deleted = await ldap.DeleteUser("uid=user,ou=users,dc=example,dc=com");

            Assert.Multiple(() =>
            {
                Assert.That(changePassword, Is.Not.Empty);
                Assert.That(setPassword, Is.Not.Empty);
                Assert.That(added, Is.False);
                Assert.That(updated, Is.False);
                Assert.That(deleted, Is.False);
            });
        }

        private static Ldap FailingLdap()
        {
            return new Ldap
            {
                Address = "127.0.0.1",
                Port = 1,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = "searchpwd",
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd",
                UserSearchPath = "ou=users,dc=example,dc=com",
                RoleSearchPath = "ou=roles,dc=example,dc=com",
                GroupSearchPath = "ou=groups,dc=example,dc=com",
                GroupWritePath = "ou=groups,dc=example,dc=com"
            };
        }

        private static LdapEntry Entry(string dn, params (string Name, string[] Values)[] attributes)
        {
            LdapAttributeSet attributeSet = new();
            foreach (var attribute in attributes)
            {
                attributeSet.Add(new LdapAttribute(attribute.Name, attribute.Values));
            }
            return new LdapEntry(dn, attributeSet);
        }
    }
}
