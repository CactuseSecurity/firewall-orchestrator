using FWO.Data;
using FWO.Encryption;
using FWO.ExternalSystems;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the Authorization header string of external ticket systems, which is built from their current credential
    /// right before the calls and never stored (GHSA-jhj7-f9vj-r9v8).
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
        private const string kRotatedPassword = "rotated:pw";
        private const string kPrivateKey = "-----BEGIN PRIVATE KEY-----probe-----END PRIVATE KEY-----";
        private const int kCredentialId = 7;
        private const int kOldCredentialId = 5;

        [Test]
        public void BuildBasicHeader_EncodesUsernameAndPassword()
        {
            Assert.That(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kPassword), Is.EqualTo(kBasicHeader));
        }

        [Test]
        public void BuildHeader_BuildsHeaderFromCredential()
        {
            Credential credential = new() { Name = "probe", ImportUser = kUsername, Secret = AesEnc.Encrypt(kPassword, kMainKey) };

            Assert.That(ExternalTicketAuthorization.BuildHeader(credential, kMainKey), Is.EqualTo(kBasicHeader));
        }

        [Test]
        public void BuildHeader_UsesEmptyUsernameWhenMissing()
        {
            Credential credential = new() { Name = "probe", ImportUser = null, Secret = AesEnc.Encrypt(kPassword, kMainKey) };

            Assert.That(ExternalTicketAuthorization.BuildHeader(credential, kMainKey), Is.EqualTo(ExternalTicketAuthorization.BuildBasicHeader("", kPassword)));
        }

        [Test]
        public void BuildHeader_ThrowsWhenSecretCannotBeDecrypted()
        {
            Credential credential = new() { Name = "probe", ImportUser = kUsername, Secret = AesEnc.Encrypt(kPassword, kOtherKey) };

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => ExternalTicketAuthorization.BuildHeader(credential, kMainKey));

            Assert.That(exception?.Message, Does.Contain("probe").And.Not.Contain(kPassword));
        }

        [Test]
        public void BuildHeader_RejectsKeyPair()
        {
            Credential credential = new() { Name = "probe_key", ImportUser = kUsername, IsKeyPair = true, Secret = AesEnc.Encrypt(kPrivateKey, kMainKey) };

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => ExternalTicketAuthorization.BuildHeader(credential, kMainKey));

            Assert.That(exception?.Message, Does.Contain("probe_key").And.Contain("key pair").And.Not.Contain(kPrivateKey));
        }

        [Test]
        public void DecryptHeader_ReturnsEmptyWithoutHeader()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ExternalTicketAuthorization.DecryptHeader(null, kMainKey), Is.Empty);
                Assert.That(ExternalTicketAuthorization.DecryptHeader("", kMainKey), Is.Empty);
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
            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => ExternalTicketAuthorization.DecryptHeader(kBasicHeader, kMainKey));

            Assert.That(exception?.Message, Does.Not.Contain(kBasicHeader));
        }

        [Test]
        public async Task ResolveHeader_UsesCurrentPasswordOfCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new() { Credential = CreateCredential(kPassword) };
            ExternalTicketSystem system = new() { Name = "probe", CredentialId = kCredentialId };

            await ExternalTicketAuthorization.ResolveHeader(system, apiConnection);
            string headerBefore = system.AuthorizationHeader;
            apiConnection.Credential = CreateCredential(kRotatedPassword);
            await ExternalTicketAuthorization.ResolveHeader(system, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(headerBefore, Is.EqualTo(kBasicHeader));
                Assert.That(system.AuthorizationHeader, Is.EqualTo(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kRotatedPassword)));
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(2));
                Assert.That(apiConnection.CredentialQueryVars[0], Does.Contain($"id = {kCredentialId}"));
            });
        }

        [Test]
        public async Task ResolveHeader_PrefersCredentialOverLegacyHeader()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new() { Credential = CreateCredential(kRotatedPassword) };
            ExternalTicketSystem system = new() { Name = "probe", CredentialId = kCredentialId, Authorization = AesEnc.Encrypt(kBasicHeader, kMainKey) };

            await ExternalTicketAuthorization.ResolveHeader(system, apiConnection);

            Assert.That(system.AuthorizationHeader, Is.EqualTo(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kRotatedPassword)));
        }

        [Test]
        public async Task ResolveHeader_DecryptsLegacyHeaderWithoutCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new();
            ExternalTicketSystem system = new() { Name = "probe", Authorization = AesEnc.Encrypt(kBasicHeader, kMainKey) };

            await ExternalTicketAuthorization.ResolveHeader(system, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(system.AuthorizationHeader, Is.EqualTo(kBasicHeader));
                Assert.That(apiConnection.CredentialQueryVars, Is.Empty);
            });
        }

        [Test]
        public async Task ResolveHeader_LeavesHeaderEmptyWithoutCredentialAndLegacyHeader()
        {
            ExtTicketHandlerTestApiConn apiConnection = new();
            ExternalTicketSystem system = new() { Name = "probe", AuthorizationHeader = "stale" };

            await ExternalTicketAuthorization.ResolveHeader(system, apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(system.AuthorizationHeader, Is.Empty);
                Assert.That(apiConnection.CredentialQueryVars, Is.Empty);
            });
        }

        [Test]
        public async Task ResolveHeader_UsesCredentialOfConfiguredTicketSystem()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new() { Credential = CreateCredential(kRotatedPassword) };
            ExternalTicketSystem storedSystem = new() { Id = 1, Name = "probe", CredentialId = kOldCredentialId };
            string configured = ConfiguredTicketSystems(new ExternalTicketSystem { Id = 1, Name = "probe", CredentialId = kCredentialId });

            await ExternalTicketAuthorization.ResolveHeader(storedSystem, apiConnection, configured);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.CredentialQueryVars[0], Does.Contain($"id = {kCredentialId}"));
                Assert.That(storedSystem.AuthorizationHeader, Is.EqualTo(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kRotatedPassword)));
            });
        }

        [Test]
        public async Task ResolveHeader_UsesLegacyHeaderWhenConfiguredTicketSystemHasNoCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new() { Credential = CreateCredential(kRotatedPassword) };
            ExternalTicketSystem storedSystem = new() { Id = 1, Name = "probe", CredentialId = kOldCredentialId, Authorization = AesEnc.Encrypt(kBasicHeader, kMainKey) };
            string configured = ConfiguredTicketSystems(new ExternalTicketSystem { Id = 1, Name = "probe" });

            await ExternalTicketAuthorization.ResolveHeader(storedSystem, apiConnection, configured);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CredentialQueryVars, Is.Empty);
                Assert.That(storedSystem.AuthorizationHeader, Is.EqualTo(kBasicHeader));
            });
        }

        [Test]
        public async Task ResolveHeader_UsesStoredCredentialWhenTicketSystemIsNoLongerConfigured()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new() { Credential = CreateCredential(kPassword) };
            ExternalTicketSystem storedSystem = new() { Id = 1, Name = "probe", CredentialId = kCredentialId };
            string configured = ConfiguredTicketSystems(new ExternalTicketSystem { Id = 2, Name = "other", CredentialId = kOldCredentialId });

            await ExternalTicketAuthorization.ResolveHeader(storedSystem, apiConnection, configured);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.CredentialQueryVars[0], Does.Contain($"id = {kCredentialId}"));
                Assert.That(storedSystem.AuthorizationHeader, Is.EqualTo(kBasicHeader));
            });
        }

        [Test]
        public void FindConfigured_FindsTicketSystemById()
        {
            string configured = ConfiguredTicketSystems(new ExternalTicketSystem { Id = 1, Name = "first" }, new ExternalTicketSystem { Id = 2, Name = "second" });

            Assert.Multiple(() =>
            {
                Assert.That(ExternalTicketSystem.FindConfigured(configured, 2)?.Name, Is.EqualTo("second"));
                Assert.That(ExternalTicketSystem.FindConfigured(configured, 3), Is.Null);
                Assert.That(ExternalTicketSystem.FindConfigured("", 1), Is.Null);
                Assert.That(ExternalTicketSystem.FindConfigured(null, 1), Is.Null);
            });
        }

        [Test]
        public void ResolveHeader_ThrowsForUnknownCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new();
            ExternalTicketSystem system = new() { Name = "probe", CredentialId = kCredentialId };

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => ExternalTicketAuthorization.ResolveHeader(system, apiConnection));

            Assert.That(exception?.Message, Does.Contain($"Credential {kCredentialId}").And.Contain("probe"));
        }

        [Test]
        public void AuthorizationHeader_IsNeverSerialized()
        {
            ExternalTicketSystem system = new() { Id = 1, Name = "probe", CredentialId = 5, AuthorizationHeader = kBasicHeader };

            Assert.Multiple(() =>
            {
                Assert.That(System.Text.Json.JsonSerializer.Serialize(system), Does.Not.Contain(kBasicHeader).And.Not.Contain("AuthorizationHeader"));
                Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(system), Does.Not.Contain(kBasicHeader).And.Not.Contain("AuthorizationHeader"));
            });
        }

        private static string ConfiguredTicketSystems(params ExternalTicketSystem[] systems)
        {
            return System.Text.Json.JsonSerializer.Serialize(systems.ToList());
        }

        private static Credential CreateCredential(string password)
        {
            return new() { Id = kCredentialId, Name = "probe", ImportUser = kUsername, Secret = AesEnc.Encrypt(password, kMainKey) };
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
