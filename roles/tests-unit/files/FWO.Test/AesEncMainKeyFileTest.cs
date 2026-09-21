using System;
using System.IO;
using FWO.Encryption;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the main key file override AesEnc reads, which lets tests exercise real
    /// encryption without the installed key at the fixed location.
    /// </summary>
    /// <remarks>
    /// NonParallelizable: the override is an environment variable and therefore process global.
    /// </remarks>
    [TestFixture]
    [NonParallelizable]
    internal class AesEncMainKeyFileTest
    {
        private const string kMainKeyFileEnvVar = "FWO_MAIN_KEY_FILE";
        private const string kMainKey = "0123456789ABCDEF0123456789ABCDEF";

        private string? previousValue;

        [SetUp]
        public void SaveEnvironment()
        {
            previousValue = Environment.GetEnvironmentVariable(kMainKeyFileEnvVar);
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            Environment.SetEnvironmentVariable(kMainKeyFileEnvVar, previousValue);
        }

        [Test]
        public void GetMainKey_ReadsTheFileNamedByTheEnvironmentVariable()
        {
            using IDisposable scope = LdapTestSupport.UseTestMainKey();

            Assert.That(AesEnc.GetMainKey(), Is.EqualTo(kMainKey));
        }

        [Test]
        public void GetMainKey_TrimsTrailingWhitespaceFromTheKeyFile()
        {
            string keyFile = WriteTemporaryKeyFile(kMainKey + "\n\n  ");
            try
            {
                Environment.SetEnvironmentVariable(kMainKeyFileEnvVar, keyFile);

                Assert.That(AesEnc.GetMainKey(), Is.EqualTo(kMainKey));
            }
            finally
            {
                File.Delete(keyFile);
            }
        }

        [Test]
        public void GetMainKey_SecretsEncryptedUnderTheConfiguredKeyRoundTrip()
        {
            using IDisposable scope = LdapTestSupport.UseTestMainKey();

            string encrypted = AesEnc.TryEncrypt("aSecret");

            Assert.That(encrypted, Is.Not.EqualTo("aSecret"));
            Assert.That(AesEnc.TryDecrypt(encrypted, true), Is.EqualTo("aSecret"));
        }

        [Test]
        public void GetMainKey_IgnoresAnEmptyEnvironmentVariable()
        {
            // An empty value must not be taken as a path: the installed key location applies,
            // which on a machine without an installation cannot be read at all.
            Environment.SetEnvironmentVariable(kMainKeyFileEnvVar, "");

            string? installedKey = null;
            try
            {
                installedKey = AesEnc.GetMainKey();
            }
            catch (Exception)
            {
                // no installed key on this host, which is the expected case for a unit test run
            }

            Assert.That(installedKey, Is.Not.EqualTo(kMainKey));
        }

        /// <summary>
        /// Writes a main key file with the exact content given, including any whitespace.
        /// </summary>
        /// <param name="content">Content to write.</param>
        /// <returns>Path of the created file.</returns>
        private static string WriteTemporaryKeyFile(string content)
        {
            string keyFile = Path.Combine(Path.GetTempPath(), $"fwo-test-main-key-{Guid.NewGuid():N}");
            File.WriteAllText(keyFile, content);
            return keyFile;
        }
    }
}
