using System;
using FWO.Encryption;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers AesEnc.TryEncrypt, which the UI applies to every secret it saves: secrets already encrypted in the
    /// authenticated format are kept, secrets in the legacy format are moved to the authenticated format (#5476).
    /// </summary>
    /// <remarks>
    /// NonParallelizable: the main key is configured through a process global environment variable.
    /// </remarks>
    [TestFixture]
    [NonParallelizable]
    internal class AesTryEncryptTest
    {
        private const string kAuthenticatedPrefix = "fwo-v2:";
        private const string kMainKey = "0123456789ABCDEF0123456789ABCDEF";
        private const string kSecret = "aSecret";
        // "pw:with:colon" encrypted with kMainKey in the legacy format (AES-CBC without tag) and IV 00..0f
        private const string kLegacyCiphertext = "AAECAwQFBgcICQoLDA0OD97QaZL2o6rGPdf9bg4WMEA=";

        [Test]
        public void TryEncrypt_EncryptsPlaintextInAuthenticatedFormat()
        {
            using IDisposable scope = LdapTestSupport.UseTestMainKey();

            string encrypted = AesEnc.TryEncrypt(kSecret);

            Assert.That(encrypted, Does.StartWith(kAuthenticatedPrefix));
            Assert.That(AesEnc.Decrypt(encrypted, kMainKey), Is.EqualTo(kSecret));
        }

        [Test]
        public void TryEncrypt_KeepsSecretAlreadyInAuthenticatedFormat()
        {
            using IDisposable scope = LdapTestSupport.UseTestMainKey();
            string encrypted = AesEnc.Encrypt(kSecret, kMainKey);

            Assert.That(AesEnc.TryEncrypt(encrypted), Is.EqualTo(encrypted));
        }

        [Test]
        public void TryEncrypt_ReencryptsLegacyFormat()
        {
            using IDisposable scope = LdapTestSupport.UseTestMainKey();

            string encrypted = AesEnc.TryEncrypt(kLegacyCiphertext);

            Assert.That(encrypted, Does.StartWith(kAuthenticatedPrefix));
            Assert.That(AesEnc.Decrypt(encrypted, kMainKey), Is.EqualTo("pw:with:colon"));
        }
    }
}
