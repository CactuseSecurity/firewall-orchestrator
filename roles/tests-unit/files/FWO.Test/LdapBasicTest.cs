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
        private static readonly string kUserDn = "uid=user,ou=users,dc=example,dc=com";
        private static readonly string kSearchUser = "cn=search,dc=example,dc=com";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string kRoleDn = "cn=AppOwners,ou=roles,dc=example,dc=com";
        private static readonly string kGroupDn = "cn=AppOwners,ou=groups,dc=example,dc=com";
        private static readonly string kGroupMemberDn = "uid=groupmember,ou=users,dc=example,dc=com";
        private static readonly string kMail = "user@example.test";
        private static readonly string kDescription = "Application owners";
        private static readonly string[] kOwnerGroupValues = { "ownergroup" };
        private static readonly string[] kUidValues = { "user" };
        private static readonly string[] kMailValues = { kMail };
        private static readonly string[] kDescriptionValues = { kDescription };
        private static readonly string[] kUniqueMemberValues = { kUserDn };
        private static readonly string[] kGroupMemberValues = { kGroupMemberDn };

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
            FakeLdapConnection connection = new();
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

        [Test]
        public async Task GetAllUsers_ReturnsUsersFromSearchResults()
        {
            RecordingLdapClient connection = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        "uid=user1,ou=users,dc=example,dc=com",
                        new LdapAttribute("mail", kMailValues)),
                    LdapTestSupport.CreateEntry(
                        "uid=user2,ou=users,dc=example,dc=com",
                        new LdapAttribute("uid", kUidValues)))
            };
            global::FWO.Test.TestableLdap ldap = new(connection)
            {
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = "ou=users,dc=example,dc=com"
            };

            List<LdapUserGetReturnParameters> users = await ldap.GetAllUsers("user");

            Assert.That(users, Has.Count.EqualTo(2));
            Assert.That(users[0].UserDn, Does.StartWith("uid=user"));
            Assert.That(connection.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetAllRoles_ReturnsRoleObjectsFromSearchResults()
        {
            RecordingLdapClient connection = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        kRoleDn,
                        new LdapAttribute("description", kDescriptionValues),
                        new LdapAttribute("uniqueMember", kUniqueMemberValues)))
            };
            global::FWO.Test.TestableLdap ldap = new(connection)
            {
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                RoleSearchPath = "ou=roles,dc=example,dc=com"
            };

            List<RoleGetReturnParameters> roles = await ldap.GetAllRoles();

            Assert.That(roles, Has.Count.EqualTo(1));
            Assert.That(roles[0].Role, Is.EqualTo(kRoleDn));
            Assert.That(roles[0].Attributes, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task GetAllInternalGroups_ReturnsGroupObjectsFromSearchResults()
        {
            RecordingLdapClient connection = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        kGroupDn,
                        new LdapAttribute("uniqueMember", kGroupMemberValues),
                        new LdapAttribute("businessCategory", kOwnerGroupValues)))
            };
            global::FWO.Test.TestableLdap ldap = new(connection)
            {
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                GroupSearchPath = "ou=groups,dc=example,dc=com"
            };

            List<GroupGetReturnParameters> groups = await ldap.GetAllInternalGroups();

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].GroupDn, Is.EqualTo(kGroupDn));
            Assert.That(groups[0].Members, Is.EqualTo(new List<string> { kGroupMemberDn }));
            Assert.That(groups[0].OwnerGroup, Is.True);
        }

        [Test]
        public async Task GetGroupMembers_ReturnsMembersFromReadEntry()
        {
            RecordingLdapClient connection = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kGroupDn,
                    new LdapAttribute("uniqueMember", kGroupMemberValues))
            };
            global::FWO.Test.TestableLdap ldap = new(connection)
            {
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                GroupSearchPath = "ou=groups,dc=example,dc=com"
            };

            List<string> members = await ldap.GetGroupMembers(kGroupDn);

            Assert.That(members, Is.EqualTo(new List<string> { kGroupMemberDn }));
            Assert.That(connection.ReadCalls, Is.EqualTo(new List<string> { kGroupDn }));
        }

        [Test]
        public async Task AddUpdateAndDeleteGroupReturnExpectedValues()
        {
            RecordingLdapClient addClient = new();
            global::FWO.Test.TestableLdap addLdap = new(addClient)
            {
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd",
                GroupWritePath = "ou=groups,dc=example,dc=com"
            };

            string addedDn = await addLdap.AddGroup("AppOwners", true);
            Assert.That(addedDn, Is.EqualTo(kGroupDn));
            Assert.That(addClient.AddedEntries, Has.Count.EqualTo(1));

            RecordingLdapClient renameClient = new();
            global::FWO.Test.TestableLdap renameLdap = new(renameClient)
            {
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd",
                GroupWritePath = "ou=groups,dc=example,dc=com"
            };

            string updatedDn = await renameLdap.UpdateGroup("OldName", "NewName");
            Assert.That(updatedDn, Is.EqualTo("cn=NewName,ou=groups,dc=example,dc=com"));
            Assert.That(renameClient.RenameCalls, Has.Count.EqualTo(1));

            RecordingLdapClient deleteClient = new();
            global::FWO.Test.TestableLdap deleteLdap = new(deleteClient)
            {
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd",
                GroupWritePath = "ou=groups,dc=example,dc=com"
            };

            bool deleted = await deleteLdap.DeleteGroup("AppOwners");
            Assert.That(deleted, Is.True);
            Assert.That(deleteClient.DeletedDns, Is.EqualTo(new List<string> { kGroupDn }));
        }

        [Test]
        public async Task ChangeAndSetPasswordReturnEmptyOnSuccess()
        {
            RecordingLdapClient changeClient = new();
            global::FWO.Test.TestableLdap changeLdap = new(changeClient);

            string changeResult = await changeLdap.ChangePassword(kUserDn, "old", "new");
            Assert.That(changeResult, Is.Empty);
            Assert.That(changeClient.ModifyCalls, Has.Count.EqualTo(1));

            RecordingLdapClient setClient = new();
            global::FWO.Test.TestableLdap setLdap = new(setClient)
            {
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd"
            };

            string setResult = await setLdap.SetPassword(kUserDn, "new");
            Assert.That(setResult, Is.Empty);
            Assert.That(setClient.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task TestConnection_UsesOverriddenConnectionWithoutNetwork()
        {
            FakeLdapConnection connection = new();
            TestableLdap ldap = new(connection)
            {
                Address = "example.test",
                Port = 636,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                WriteUser = "cn=write,dc=example,dc=com",
                WriteUserPwd = "writepwd"
            };

            await ldap.TestConnection();

            Assert.That(connection.BindCalls, Is.EqualTo(2));
            Assert.That(connection.LastBoundUsers, Does.Contain(kSearchUser));
            Assert.That(connection.LastBoundUsers, Does.Contain("cn=write,dc=example,dc=com"));
        }

        [Test]
        public async Task GetUserDetailsFromLdap_UsesOverriddenConnectionWithoutNetwork()
        {
            LdapEntry expectedEntry = Entry(kUserDn, ("uid", ["user"]));
            FakeLdapConnection connection = new()
            {
                ReadResult = expectedEntry
            };
            TestableLdap ldap = new(connection)
            {
                Address = "example.test",
                Port = 389,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword
            };

            LdapEntry? entry = await ldap.GetUserDetailsFromLdap(kUserDn);

            Assert.That(entry, Is.SameAs(expectedEntry));
            Assert.That(connection.ReadCalls, Is.EqualTo(1));
            Assert.That(connection.BindCalls, Is.EqualTo(1));
        }

        private static Ldap FailingLdap()
        {
            return new Ldap
            {
                Address = "127.0.0.1",
                Port = 1,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
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

        private sealed class TestableLdap : Ldap
        {
            private readonly ILdapClient connection;

            public TestableLdap(ILdapClient connection)
            {
                this.connection = connection;
            }

            protected override Task<ILdapClient> Connect()
            {
                return Task.FromResult(connection);
            }
        }

        private sealed class FakeLdapConnection : ILdapClient
        {
            public bool Bound { get; private set; }
            public int BindCalls { get; private set; }
            public int ReadCalls { get; private set; }
            public List<string> LastBoundUsers { get; } = [];
            public LdapConstraints SearchConstraints { get; } = new();
            public LdapConstraints Constraints { get; set; } = new();
            public LdapEntry? ReadResult { get; set; }

            public Task BindAsync(string user, string password)
            {
                BindCalls++;
                LastBoundUsers.Add(user);
                Bound = true;
                return Task.CompletedTask;
            }

            public Task<LdapEntry?> ReadAsync(string distinguishedName)
            {
                ReadCalls++;
                return Task.FromResult(ReadResult);
            }

            public Task<ILdapSearchResults?> SearchAsync(string? baseDn, int scope, string filter, string[]? attributes, bool typesOnly)
            {
                throw new AssertionException("SearchAsync was not expected in this test.");
            }

            public Task AddAsync(LdapEntry entry)
            {
                throw new AssertionException("AddAsync was not expected in this test.");
            }

            public Task DeleteAsync(string distinguishedName)
            {
                throw new AssertionException("DeleteAsync was not expected in this test.");
            }

            public Task ModifyAsync(string distinguishedName, LdapModification[] mods)
            {
                throw new AssertionException("ModifyAsync was not expected in this test.");
            }

            public Task RenameAsync(string distinguishedName, string newRdn, bool deleteOldRdn)
            {
                throw new AssertionException("RenameAsync was not expected in this test.");
            }

            public void Dispose()
            { }
        }
    }
}
