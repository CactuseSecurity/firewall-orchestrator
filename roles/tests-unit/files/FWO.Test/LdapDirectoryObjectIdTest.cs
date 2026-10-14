using FWO.Data;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class LdapDirectoryObjectIdTest
    {
        private const string kUserDn = "uid=user,ou=users,dc=example,dc=com";
        private const string kUserSearchPath = "ou=users,dc=example,dc=com";
        private static readonly Guid kGuid = new("0f8fad5b-d9cb-469f-a165-70867728950e");
        private static readonly byte[] kShortBinaryId = [0x01, 0xab, 0x02, 0xcd];
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");

        [Test]
        public void GetFrom_ReadsEntryUuidWithItsAttributeName()
        {
            LdapEntry entry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("entryUUID", " 6F1B6C2E-0C5B-4A55-9E5A-5D3E2C1B0A99 "));

            Assert.That(LdapDirectoryObjectId.GetFrom(entry), Is.EqualTo("entryUUID:6f1b6c2e-0c5b-4a55-9e5a-5d3e2c1b0a99"));
        }

        [Test]
        public void GetFrom_FormatsTheBinaryObjectGuidAsGuid()
        {
            LdapEntry entry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("objectGUID", kGuid.ToByteArray()));

            Assert.That(LdapDirectoryObjectId.GetFrom(entry), Is.EqualTo($"objectGUID:{kGuid}"));
        }

        [Test]
        public void GetFrom_FormatsOtherBinaryIdsAsHex()
        {
            LdapEntry entry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("GUID", kShortBinaryId));

            Assert.That(LdapDirectoryObjectId.GetFrom(entry), Is.EqualTo("GUID:01ab02cd"));
        }

        [Test]
        public void GetFrom_PrefersTheFirstAttributeInOrderOfPreference()
        {
            LdapEntry entry = LdapTestSupport.CreateEntry(kUserDn,
                new LdapAttribute("nsUniqueId", "a1b2c3d4-11111111-22222222-33333333"),
                new LdapAttribute("entryUUID", "6f1b6c2e-0c5b-4a55-9e5a-5d3e2c1b0a99"));

            Assert.That(LdapDirectoryObjectId.GetFrom(entry), Is.EqualTo("entryUUID:6f1b6c2e-0c5b-4a55-9e5a-5d3e2c1b0a99"));
        }

        [Test]
        public void GetFrom_MatchesAttributeNamesCaseInsensitively()
        {
            LdapEntry entry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("nsuniqueid", "A1B2C3D4-11111111-22222222-33333333"));

            Assert.That(LdapDirectoryObjectId.GetFrom(entry), Is.EqualTo("nsUniqueId:a1b2c3d4-11111111-22222222-33333333"));
        }

        [Test]
        public void GetFrom_ReturnsNullWithoutIdAttribute()
        {
            LdapEntry entry = LdapTestSupport.CreateEntry(kUserDn, new LdapAttribute("uid", "user"), new LdapAttribute("entryUUID", "  "));

            Assert.That(LdapDirectoryObjectId.GetFrom(entry), Is.Null);
        }

        [Test]
        public void BuildUserAttributeList_RequestsUserAttributesExtrasAndAllIdAttributes()
        {
            string[] attributes = LdapDirectoryObjectId.BuildUserAttributeList("memberof");

            Assert.Multiple(() =>
            {
                Assert.That(attributes[0], Is.EqualTo("*"));
                Assert.That(attributes, Does.Contain("memberof"));
                Assert.That(attributes, Is.SupersetOf(LdapDirectoryObjectId.AttributeNames));
            });
        }

        [Test]
        public async Task GetLdapEntry_RequestsTheIdAttributesWhenReadingByDn()
        {
            RecordingLdapClient connection = new() { ReadResult = LdapTestSupport.CreateEntry(kUserDn) };
            Ldap ldap = CreateLdap(connection);

            LdapEntry? entry = await ldap.GetLdapEntry(new UiUser { Dn = kUserDn }, false);

            Assert.Multiple(() =>
            {
                Assert.That(entry?.Dn, Is.EqualTo(kUserDn));
                Assert.That(connection.ReadAttributeCalls, Has.Count.EqualTo(1));
                Assert.That(connection.ReadAttributeCalls[0], Is.SupersetOf(LdapDirectoryObjectId.AttributeNames));
            });
        }

        [Test]
        public async Task GetLdapEntry_RequestsTheIdAttributesWhenSearchingByName()
        {
            RecordingLdapClient connection = new() { SearchResults = LdapTestSupport.CreateSearchResults(LdapTestSupport.CreateEntry(kUserDn)) };
            Ldap ldap = CreateLdap(connection);

            await ldap.GetLdapEntry(new UiUser { Name = "user" }, false);

            Assert.Multiple(() =>
            {
                Assert.That(connection.SearchCalls, Has.Count.EqualTo(1));
                Assert.That(connection.SearchCalls[0].Attributes, Is.SupersetOf(LdapDirectoryObjectId.AttributeNames));
                Assert.That(connection.SearchCalls[0].Attributes, Does.Contain("memberof"));
            });
        }

        private static TestableLdap CreateLdap(RecordingLdapClient connection)
        {
            return new TestableLdap(connection)
            {
                Id = 1,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kUserSearchPath
            };
        }
    }
}
