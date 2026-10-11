using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Encryption;
using FWO.ExternalSystems;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the Authorization header string of external ticket systems, which is built from their credential
    /// and only stored encrypted (GHSA-jhj7-f9vj-r9v8).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class ExternalTicketAuthorizationTest
    {
        private const string kMainKey = "0123456789ABCDEF0123456789ABCDEF";
        private const string kOtherKey = "FEDCBA9876543210FEDCBA9876543210";
        private const string kUsername = "probe_user";
        private const string kPassword = "pw:with:colon";
        private const string kBasicHeader = "Basic cHJvYmVfdXNlcjpwdzp3aXRoOmNvbG9u";

        [Test]
        public void BuildBasicHeader_EncodesUsernameAndPassword()
        {
            Assert.That(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kPassword), Is.EqualTo(kBasicHeader));
        }

        [Test]
        public void BuildEncryptedHeader_EncryptsHeaderBuiltFromCredential()
        {
            Credential credential = new() { Name = "probe", ImportUser = kUsername, Secret = AesEnc.Encrypt(kPassword, kMainKey) };

            string encryptedHeader = ExternalTicketAuthorization.BuildEncryptedHeader(credential, kMainKey);

            Assert.Multiple(() =>
            {
                Assert.That(encryptedHeader, Does.Not.Contain(kBasicHeader["Basic ".Length..]));
                Assert.That(AesEnc.Decrypt(encryptedHeader, kMainKey), Is.EqualTo(kBasicHeader));
            });
        }

        [Test]
        public void BuildEncryptedHeader_UsesEmptyUsernameWhenMissing()
        {
            Credential credential = new() { Name = "probe", ImportUser = null, Secret = AesEnc.Encrypt(kPassword, kMainKey) };

            string encryptedHeader = ExternalTicketAuthorization.BuildEncryptedHeader(credential, kMainKey);

            Assert.That(AesEnc.Decrypt(encryptedHeader, kMainKey), Is.EqualTo(ExternalTicketAuthorization.BuildBasicHeader("", kPassword)));
        }

        [Test]
        public void BuildEncryptedHeader_ThrowsWhenSecretCannotBeDecrypted()
        {
            Credential credential = new() { Name = "probe", ImportUser = kUsername, Secret = AesEnc.Encrypt(kPassword, kOtherKey) };

            ProcessingFailedException? exception = Assert.Throws<ProcessingFailedException>(() => ExternalTicketAuthorization.BuildEncryptedHeader(credential, kMainKey));

            Assert.That(exception?.Message, Does.Contain("probe").And.Not.Contain(kPassword));
        }

        [Test]
        public void DecryptHeader_ReturnsEmptyWithoutHeader()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExternalTicketAuthorization.DecryptHeader(null, kMainKey), Is.Empty);
                Assert.That(ExternalTicketAuthorization.DecryptHeader("", kMainKey), Is.Empty);
                Assert.That(ExternalTicketAuthorization.DecryptHeader(null), Is.Empty);
            });
        }

        [Test]
        public void DecryptHeader_ReturnsHeaderInPlainText()
        {
            Assert.That(ExternalTicketAuthorization.DecryptHeader(AesEnc.Encrypt(kBasicHeader, kMainKey), kMainKey), Is.EqualTo(kBasicHeader));
        }

        [Test]
        public void DecryptHeader_RejectsHeaderInPlainText()
        {
            ProcessingFailedException? exception = Assert.Throws<ProcessingFailedException>(() => ExternalTicketAuthorization.DecryptHeader(kBasicHeader, kMainKey));

            Assert.That(exception?.Message, Does.Not.Contain(kBasicHeader));
        }

        [Test]
        public void DecryptHeader_UsesMainKeyOfInstallation()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);

            Assert.That(ExternalTicketAuthorization.DecryptHeader(AesEnc.Encrypt(kBasicHeader, kMainKey)), Is.EqualTo(kBasicHeader));
        }

        [Test]
        public void UsingCredential_ListsTicketSystemsReferencingCredential()
        {
            string extTicketSystems = System.Text.Json.JsonSerializer.Serialize(new List<ExternalTicketSystem>
            {
                new() { Id = 1, Name = "uses", CredentialId = 5 },
                new() { Id = 2, Name = "other", CredentialId = 6 },
                new() { Id = 3, Name = "none" }
            });

            Assert.Multiple(() =>
            {
                Assert.That(ExternalTicketSystem.UsingCredential(extTicketSystems, 5).Select(system => system.Name), Is.EqualTo(new List<string> { "uses" }));
                Assert.That(ExternalTicketSystem.UsingCredential(extTicketSystems, 7), Is.Empty);
                Assert.That(ExternalTicketSystem.UsingCredential("", 5), Is.Empty);
                Assert.That(ExternalTicketSystem.UsingCredential(null, 5), Is.Empty);
            });
        }

        [Test]
        public void TicketSystemConfiguration_ContainsNoAuthorization()
        {
            ExternalTicketSystem system = new() { Id = 1, Name = "probe", CredentialId = 5 };

            string systemTextJson = System.Text.Json.JsonSerializer.Serialize(system);
            string newtonsoftJson = Newtonsoft.Json.JsonConvert.SerializeObject(system);

            Assert.Multiple(() =>
            {
                Assert.That(systemTextJson, Does.Not.Contain("Authorization"));
                Assert.That(newtonsoftJson, Does.Not.Contain("Authorization"));
                Assert.That(systemTextJson, Does.Contain("\"CredentialId\":5"));
                Assert.That(System.Text.Json.JsonSerializer.Deserialize<ExternalTicketSystem>(systemTextJson)?.CredentialId, Is.EqualTo(5));
            });
        }

        [Test]
        public void RedactAuthorization_HidesAuthorizationValue()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExternalTicketSystem.RedactAuthorization("{\"Id\":1,\"Authorization\":\"c2VjcmV0+/==\",\"Name\":\"probe\"}"),
                    Is.EqualTo("{\"Id\":1,\"Authorization\":\"****\",\"Name\":\"probe\"}"));
                Assert.That(ExternalTicketSystem.RedactAuthorization("{\"Id\": 1, \"Authorization\": \"Basic dTpw\"}"),
                    Is.EqualTo("{\"Id\": 1, \"Authorization\": \"****\"}"));
                Assert.That(ExternalTicketSystem.RedactAuthorization("{\"Authorization\":\"a\\\"b\",\"Name\":\"probe\"}"),
                    Is.EqualTo("{\"Authorization\":\"****\",\"Name\":\"probe\"}"));
            });
        }

        [Test]
        public void RedactAuthorization_KeepsTicketSystemWithoutAuthorization()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExternalTicketSystem.RedactAuthorization("{\"Id\":1,\"Name\":\"probe\"}"), Is.EqualTo("{\"Id\":1,\"Name\":\"probe\"}"));
                Assert.That(ExternalTicketSystem.RedactAuthorization("{\"Authorization\":\"\"}"), Is.EqualTo("{\"Authorization\":\"\"}"));
                Assert.That(ExternalTicketSystem.RedactAuthorization(""), Is.Empty);
            });
        }

        [Test]
        public void RequestCopy_KeepsEncryptedAuthorization()
        {
            ExternalTicketSystem system = new() { Id = 1, Name = "probe", Authorization = "encrypted" };

            ExternalTicketSystem? stored = System.Text.Json.JsonSerializer.Deserialize<ExternalTicketSystem>(System.Text.Json.JsonSerializer.Serialize(system));

            Assert.That(stored?.Authorization, Is.EqualTo("encrypted"));
        }
    }
}
