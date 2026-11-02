using FWO.Encryption;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Security.Cryptography;
using System.Text;
using Assert = NUnit.Framework.Assert;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AesDecryptTest
    {

        private static readonly Random random = new Random();
        private const string kAuthenticatedPrefix = "fwo-v2:";
        private const string kMainKey = "0123456789ABCDEF0123456789ABCDEF";
        private const string kOtherKey = "FEDCBA9876543210FEDCBA9876543210";
        private const string kVectorPlaintext = "pw:with:colon äöü€";
        // kVectorPlaintext encrypted with kMainKey and IV 00..0f by an independent implementation of the format (Python
        // cryptography), shared with the importer test test_fwo_encrypt.py: C# and Python must read each other's secrets
        private const string kVectorCiphertext = "fwo-v2:AAECAwQFBgcICQoLDA0OD1ufs4AnvnYj6EC6iJI2Uy3RsaYUWNvBkQ7DpC0hylIZYIegs7IKHHLf45uJxN/AnTf5hrKcTwy/SDPHrST8ET0=";
        // "pw:with:colon" encrypted with kOtherKey in the legacy format (AES-CBC without tag) and a fixed IV
        private const string kLegacyCiphertext = "AAECAwQFBgcICQoLDA0ODxFcDA027hBvCxTC/0FVQ/M=";
        private const int kWrongKeyAttempts = 2000;
        private const int kTagSize = 32;
        private const string printableChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        public static string GenerateRandomString(int minLength, int maxLength)
        {
            int length = random.Next(minLength, maxLength + 1);
            StringBuilder stringBuilder = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                char randomChar = printableChars[random.Next(printableChars.Length)];
                stringBuilder.Append(randomChar);
            }

            return stringBuilder.ToString();
        }
        [Test]
        public void TestEncryptDecryptRandomData()
        {
            string tempKey = GenerateRandomString(32, 32);
            string randomPlaintext = GenerateRandomString(15, 100);
            string encryptedString = AesEnc.Encrypt(randomPlaintext, tempKey);
            string decryptedString = AesEnc.Decrypt(encryptedString, tempKey);
            ClassicAssert.AreEqual(randomPlaintext, decryptedString);
        }

        [Test]
        public void TestTryDecryptRejectsInvalidBase64()
        {
            string tempKey = GenerateRandomString(32, 32);
            bool result = AesEnc.TryDecrypt("not-base64", tempKey, out string decryptedText);

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual(string.Empty, decryptedText);
        }

        [Test]
        public void TestTryDecryptRejectsInvalidCiphertextBlockSize()
        {
            string tempKey = GenerateRandomString(32, 32);
            string invalidCiphertext = Convert.ToBase64String(new byte[20]);
            bool result = AesEnc.TryDecrypt(invalidCiphertext, tempKey, out string decryptedText);

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual(string.Empty, decryptedText);
        }

        [Test]
        public void TestEncryptWritesAuthenticatedFormat()
        {
            string encryptedString = AesEnc.Encrypt("aSecret", kMainKey);

            Assert.That(encryptedString, Does.StartWith(kAuthenticatedPrefix));
            Assert.That(AesEnc.Encrypt("aSecret", kMainKey), Is.Not.EqualTo(encryptedString), "random IV per encryption");
        }

        [Test]
        public void TestDecryptReadsCrossLanguageVector()
        {
            Assert.That(AesEnc.Decrypt(kVectorCiphertext, kMainKey), Is.EqualTo(kVectorPlaintext));
        }

        [Test]
        public void TestEncryptDecryptEmptyAndUnicodeSecrets()
        {
            Assert.That(AesEnc.Decrypt(AesEnc.Encrypt("", kMainKey), kMainKey), Is.EqualTo(""));
            Assert.That(AesEnc.Decrypt(AesEnc.Encrypt(kVectorPlaintext, kMainKey), kMainKey), Is.EqualTo(kVectorPlaintext));
        }

        [Test]
        public void TestDecryptReadsLegacyFormat()
        {
            Assert.That(AesEnc.Decrypt(kLegacyCiphertext, kOtherKey), Is.EqualTo("pw:with:colon"));
        }

        [Test]
        public void TestTryDecryptAlwaysRejectsWrongKey()
        {
            // the legacy format accepted about 1 in 250 wrong keys through a valid padding by chance (#5476)
            int acceptedCount = 0;
            for (int attempt = 0; attempt < kWrongKeyAttempts; attempt++)
            {
                string encryptedString = AesEnc.Encrypt(GenerateRandomString(0, 40), kOtherKey);
                if (AesEnc.TryDecrypt(encryptedString, kMainKey, out _))
                {
                    acceptedCount++;
                }
            }

            Assert.That(acceptedCount, Is.Zero);
        }

        [Test]
        public void TestTryDecryptRejectsModifiedCiphertext()
        {
            byte[] payload = Convert.FromBase64String(kVectorCiphertext[kAuthenticatedPrefix.Length..]);
            int lastCiphertextByte = payload.Length - kTagSize - 1;
            int lastTagByte = payload.Length - 1;

            Assert.Multiple(() =>
            {
                Assert.That(AesEnc.TryDecrypt(FlipByte(payload, 0), kMainKey, out _), Is.False, "IV");
                Assert.That(AesEnc.TryDecrypt(FlipByte(payload, lastCiphertextByte), kMainKey, out _), Is.False, "ciphertext");
                Assert.That(AesEnc.TryDecrypt(FlipByte(payload, lastTagByte), kMainKey, out string decryptedText), Is.False, "tag");
                Assert.That(decryptedText, Is.Empty);
            });
        }

        [Test]
        public void TestTryDecryptRejectsTruncatedOrInvalidAuthenticatedFormat()
        {
            byte[] payload = Convert.FromBase64String(kVectorCiphertext[kAuthenticatedPrefix.Length..]);
            string withoutTag = kAuthenticatedPrefix + Convert.ToBase64String(payload, 0, payload.Length - kTagSize);
            string withoutCiphertext = kAuthenticatedPrefix + Convert.ToBase64String(new byte[16 + kTagSize]);

            Assert.Multiple(() =>
            {
                Assert.That(AesEnc.TryDecrypt(withoutTag, kMainKey, out _), Is.False);
                Assert.That(AesEnc.TryDecrypt(withoutCiphertext, kMainKey, out _), Is.False);
                Assert.That(AesEnc.TryDecrypt(kAuthenticatedPrefix + "not-base64", kMainKey, out _), Is.False);
                Assert.That(AesEnc.TryDecrypt(kAuthenticatedPrefix, kMainKey, out _), Is.False);
            });
        }

        [Test]
        public void TestTryDecryptDoesNotFallBackToLegacyFormat()
        {
            // a secret marked as authenticated must never be read without its tag
            Assert.That(AesEnc.TryDecrypt(kAuthenticatedPrefix + kLegacyCiphertext, kOtherKey, out _), Is.False);
        }

        [Test]
        public void TestEncryptRejectsInvalidKeyLength()
        {
            Assert.Throws<CryptographicException>(() => AesEnc.Encrypt("aSecret", "shortKey"));
            Assert.That(AesEnc.TryDecrypt(kVectorCiphertext, "shortKey", out _), Is.False);
        }

        private static string FlipByte(byte[] payload, int index)
        {
            byte[] modified = (byte[])payload.Clone();
            modified[index] ^= 0x01;
            return kAuthenticatedPrefix + Convert.ToBase64String(modified);
        }
    }
}
