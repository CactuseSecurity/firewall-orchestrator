using FWO.Basics.Exceptions;
using FWO.Config.File;
using NUnit.Framework;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Assert = NUnit.Framework.Assert;

namespace FWO.Test
{
    /// <summary>
    /// The trust anchor is read from inside TLS validation callbacks, so it is cached
    /// rather than parsed per handshake - but a cache that never expires would pin a
    /// service to a superseded CA. These tests cover both halves.
    /// </summary>
    [TestFixture]
    [NonParallelizable] // mutates the process wide ConfigFile paths and the shared cache
    internal class InternalCaCertificateTest
    {
        private static readonly string kAnchorPath = Path.Combine(Path.GetTempPath(), "fwo_internal_ca_cache_test.crt");
        private static string firstAnchorPem = "";
        private static string secondAnchorPem = "";
        private static string firstAnchorThumbprint = "";
        private static string secondAnchorThumbprint = "";

        [OneTimeSetUp]
        public void CreateAnchors()
        {
            (firstAnchorPem, firstAnchorThumbprint) = CreateAnchor("CN=fwo-internal-ca-cache-test-first");
            (secondAnchorPem, secondAnchorThumbprint) = CreateAnchor("CN=fwo-internal-ca-cache-test-second");
        }

        [SetUp]
        public void WriteFirstAnchor()
        {
            File.WriteAllText(kAnchorPath, firstAnchorPem);
            SetConfiguredAnchorPath(kAnchorPath);
            ClearCache();
        }

        [TearDown]
        public void RemoveAnchor()
        {
            File.Delete(kAnchorPath);
            SetConfiguredAnchorPath(kAnchorPath);
            ClearCache();
        }

        /// <summary>
        /// Parsing the file per handshake would be paid on every connection.
        /// </summary>
        [Test]
        public void Get_CalledTwiceForAnUnchangedFile_ReturnsTheSameInstance()
        {
            X509Certificate2Collection first = InternalCaCertificate.Get();
            X509Certificate2Collection second = InternalCaCertificate.Get();

            Assert.That(second, Is.SameAs(first));
        }

        /// <summary>
        /// An installer run may replace the anchor - a rotated internal CA, or a customer
        /// managed issuer added on upgrade - without restarting the consuming services.
        /// </summary>
        [Test]
        public void Get_AfterTheAnchorFileChanged_ReturnsTheNewAnchor()
        {
            X509Certificate2Collection first = InternalCaCertificate.Get();
            Assert.That(Thumbprints(first), Is.EqualTo(new List<string> { firstAnchorThumbprint }));

            ReplaceAnchorFile(secondAnchorPem);

            X509Certificate2Collection second = InternalCaCertificate.Get();
            Assert.Multiple(() =>
            {
                Assert.That(Thumbprints(second), Is.EqualTo(new List<string> { secondAnchorThumbprint }));
                Assert.That(second, Is.Not.SameAs(first));
            });
        }

        /// <summary>
        /// A rotation that puts an unreadable file in place must not keep the previous
        /// anchor alive: it would accept certificates this installation has stopped trusting.
        /// </summary>
        [Test]
        public void Get_AfterTheAnchorFileBecameUnreadable_Fails()
        {
            InternalCaCertificate.Get();

            ReplaceAnchorFile("not a certificate");

            Assert.Throws<ConfigException>(() => InternalCaCertificate.Get());
        }

        /// <summary>
        /// The failure is remembered, so an unreadable anchor is not re-read and re-logged
        /// on every handshake.
        /// </summary>
        [Test]
        public void Get_WithAnUnreadableAnchor_ReportsTheSameFailureWithoutRetrying()
        {
            File.WriteAllText(kAnchorPath, "not a certificate");
            ClearCache();

            ConfigException first = Assert.Throws<ConfigException>(() => InternalCaCertificate.Get())!;
            // Readable content behind an unchanged last write time: the second call still
            // fails, which is only possible if it did not read the file again.
            DateTime unchangedWriteTime = File.GetLastWriteTimeUtc(kAnchorPath);
            File.WriteAllText(kAnchorPath, firstAnchorPem);
            SetLastWriteTime(unchangedWriteTime);
            ConfigException second = Assert.Throws<ConfigException>(() => InternalCaCertificate.Get())!;

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.SameAs(first));
                Assert.That(first.Message, Does.Contain(kAnchorPath));
            });
        }

        /// <summary>
        /// Fixing the file recovers within the back-off window rather than after it: the
        /// remembered failure belongs to the file it was read from.
        /// </summary>
        [Test]
        public void Get_AfterAFailedLoadWasFixed_RecoversWithoutWaitingOutTheBackOff()
        {
            File.WriteAllText(kAnchorPath, "not a certificate");
            ClearCache();
            Assert.Throws<ConfigException>(() => InternalCaCertificate.Get());

            ReplaceAnchorFile(firstAnchorPem);

            Assert.That(Thumbprints(InternalCaCertificate.Get()), Is.EqualTo(new List<string> { firstAnchorThumbprint }));
        }

        /// <summary>
        /// Without a configured anchor nothing can be validated, and the message has to say
        /// which setting is missing.
        /// </summary>
        [Test]
        public void Get_WithoutAConfiguredAnchor_ReportsTheMissingSetting()
        {
            SetConfiguredAnchorPath(null);
            ClearCache();

            ConfigException thrown = Assert.Throws<ConfigException>(() => InternalCaCertificate.Get())!;

            Assert.That(thrown.Message, Does.Contain("tls_ca_certificate"));
        }

        /// <summary>
        /// An empty tls_ca_certificate is not null, so it passes the config layer's null
        /// check and reaches the file lookup, which rejects a zero length path with an
        /// ArgumentException. That is not a ConfigException, so it would escape Get()
        /// untranslated, skip the back-off, and leave the tls validation callbacks - which
        /// catch ConfigException precisely to avoid it - throwing out of a handshake.
        /// </summary>
        [Test]
        public void Get_WithAnEmptyConfiguredAnchorPath_ReportsTheMissingSetting()
        {
            SetConfiguredAnchorPath("");
            ClearCache();

            ConfigException thrown = Assert.Throws<ConfigException>(() => InternalCaCertificate.Get())!;

            Assert.That(thrown.Message, Does.Contain("tls_ca_certificate"));
        }

        /// <summary>
        /// The same for a path of blanks, which reaches the file lookup just as an empty
        /// one does.
        /// </summary>
        [Test]
        public void Get_WithABlankConfiguredAnchorPath_ReportsTheMissingSetting()
        {
            SetConfiguredAnchorPath("   ");
            ClearCache();

            ConfigException thrown = Assert.Throws<ConfigException>(() => InternalCaCertificate.Get())!;

            Assert.That(thrown.Message, Does.Contain("tls_ca_certificate"));
        }

        /// <summary>
        /// An upgrade that retains a customer managed certificate on part of its Apache
        /// endpoints has to trust that issuer alongside the internal CA, so the configured
        /// file is a bundle rather than a single certificate.
        /// </summary>
        [Test]
        public void Get_WithABundleOfAnchors_ReturnsEveryAnchor()
        {
            // Newline separated, the way the installer assembles the bundle: run the two
            // blocks together and the END of one meets the BEGIN of the next, and neither
            // parses.
            ReplaceAnchorFile(firstAnchorPem + "\n" + secondAnchorPem);

            X509Certificate2Collection anchors = InternalCaCertificate.Get();

            Assert.That(Thumbprints(anchors), Is.EquivalentTo(new List<string> { firstAnchorThumbprint, secondAnchorThumbprint }));
        }

        /// <summary>
        /// A readable file holding no certificate at all is a misconfiguration, not an
        /// empty trust store that silently rejects every peer.
        /// </summary>
        [Test]
        public void Get_WithAFileHoldingNoCertificate_ReportsTheMisconfiguration()
        {
            ReplaceAnchorFile("# no certificate here\n");

            ConfigException thrown = Assert.Throws<ConfigException>(() => InternalCaCertificate.Get())!;

            Assert.That(thrown.Message, Does.Contain(kAnchorPath));
        }

        /// <summary>
        /// Lists the thumbprints of a loaded anchor set in file order.
        /// </summary>
        /// <param name="anchors">The loaded anchors.</param>
        /// <returns>The thumbprint of each anchor.</returns>
        private static List<string> Thumbprints(X509Certificate2Collection anchors)
        {
            List<string> thumbprints = [];
            foreach (X509Certificate2 anchor in anchors)
            {
                thumbprints.Add(anchor.Thumbprint);
            }
            return thumbprints;
        }

        /// <summary>
        /// Drops the shared cache so a test starts from an unloaded state. Also used by
        /// <see cref="GraphQlApiConnectionClientCertificateTest"/>, which validates against
        /// the same anchor.
        /// </summary>
        internal static void ClearCache()
        {
            SetStaticField("certificates", null);
            SetStaticField("cachedPath", "");
            SetStaticField("cachedWriteTimeUtc", DateTime.MinValue);
            SetStaticField("failure", null);
            SetStaticField("failedAt", DateTime.MinValue);
        }

        /// <summary>
        /// Writes new content and forces a distinct last write time, which a file system
        /// with coarse timestamps would otherwise not report for a fast rewrite.
        /// </summary>
        /// <param name="content">The new file content.</param>
        private static void ReplaceAnchorFile(string content)
        {
            DateTime previousWriteTime = File.GetLastWriteTimeUtc(kAnchorPath);
            File.WriteAllText(kAnchorPath, content);
            SetLastWriteTime(previousWriteTime.AddSeconds(1));
        }

        private static void SetLastWriteTime(DateTime writeTimeUtc)
        {
            File.SetLastWriteTimeUtc(kAnchorPath, writeTimeUtc);
        }

        private static (string Pem, string Thumbprint) CreateAnchor(string subject)
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return (certificate.ExportCertificatePem(), certificate.Thumbprint);
        }

        private static void SetConfiguredAnchorPath(string? path)
        {
            object data = typeof(ConfigFile).GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
                ?? throw new InvalidOperationException("ConfigFile.Data could not be read.");
            PropertyInfo property = data.GetType().GetProperty("TlsCaCertificatePath")
                ?? throw new InvalidOperationException("ConfigFileData.TlsCaCertificatePath could not be found.");
            property.SetValue(data, path);
        }

        private static void SetStaticField(string name, object? value)
        {
            FieldInfo field = typeof(InternalCaCertificate).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"InternalCaCertificate.{name} could not be found.");
            field.SetValue(null, value);
        }
    }
}
