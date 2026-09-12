using FWO.Basics.Exceptions;
using FWO.Config.File;
using FWO.Middleware.Server;
using NUnit.Framework;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Test
{
    /// <summary>
    /// Tests the certificate trust policy used by LDAP-over-TLS connections.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class LdapTlsCertificateTest
    {
        private static readonly string kCertificateAuthorityPath = Path.Combine(Path.GetTempPath(), "fwo_ldap_ca_test.crt");
        private object? originalConfigFileData;

        /// <summary>
        /// Preserves the process-wide configuration before each test changes its CA path.
        /// </summary>
        [SetUp]
        public void SaveConfiguration()
        {
            originalConfigFileData = GetConfigFileDataProperty().GetValue(null);
        }

        /// <summary>
        /// Restores process-wide configuration and removes the temporary certificate.
        /// </summary>
        [TearDown]
        public void RestoreConfiguration()
        {
            GetConfigFileDataProperty().SetValue(null, originalConfigFileData);
            File.Delete(kCertificateAuthorityPath);
        }

        /// <summary>
        /// A certificate already accepted by the platform trust store needs no internal CA fallback.
        /// </summary>
        [Test]
        public void ValidateCertificate_AcceptsPlatformTrustResult()
        {
            bool accepted = ValidateCertificate(null, null, SslPolicyErrors.None);

            Assert.That(accepted, Is.True);
        }

        /// <summary>
        /// Missing certificates and hostname mismatches must always be rejected.
        /// </summary>
        [Test]
        public void ValidateCertificate_RejectsMissingCertificateAndNameMismatch()
        {
            using X509Certificate2 certificate = CreateSelfSignedCertificate("CN=ldap-name-mismatch-test");

            bool missingCertificateAccepted = ValidateCertificate(
                null,
                null,
                SslPolicyErrors.RemoteCertificateNotAvailable);
            bool nameMismatchAccepted = ValidateCertificate(
                certificate,
                null,
                SslPolicyErrors.RemoteCertificateNameMismatch);

            Assert.Multiple(() =>
            {
                Assert.That(missingCertificateAccepted, Is.False);
                Assert.That(nameMismatchAccepted, Is.False);
            });
        }

        /// <summary>
        /// A peer-supplied intermediate must complete a chain to FWO's configured internal CA.
        /// </summary>
        [Test]
        public void ValidateCertificate_AcceptsConfiguredAuthorityWithIntermediate()
        {
            using ECDsa rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using X509Certificate2 root = CreateCertificateAuthority("CN=fwo-ldap-root-test", rootKey);
            using ECDsa intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using X509Certificate2 intermediate = CreateIssuedCertificateAuthority(
                "CN=fwo-ldap-intermediate-test",
                intermediateKey,
                root);
            using X509Certificate2 signingIntermediate = intermediate.CopyWithPrivateKey(intermediateKey);
            using ECDsa leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using X509Certificate2 leaf = CreateIssuedServerCertificate(
                "CN=fwo-ldap-leaf-test",
                leafKey,
                signingIntermediate);
            File.WriteAllText(kCertificateAuthorityPath, root.ExportCertificatePem());
            SetCertificateAuthorityPath(kCertificateAuthorityPath);

            using X509Chain peerChain = BuildPeerChain(leaf, intermediate, root);
            bool accepted = ValidateCertificate(
                leaf,
                peerChain,
                SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.That(accepted, Is.True);
        }

        /// <summary>
        /// A certificate from an unrelated issuer must not be accepted by the internal CA fallback.
        /// </summary>
        [Test]
        public void ValidateCertificate_RejectsUntrustedAuthority()
        {
            using ECDsa rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using X509Certificate2 root = CreateCertificateAuthority("CN=fwo-ldap-root-test", rootKey);
            using X509Certificate2 untrustedCertificate = CreateSelfSignedCertificate("CN=untrusted-ldap-test");
            File.WriteAllText(kCertificateAuthorityPath, root.ExportCertificatePem());
            SetCertificateAuthorityPath(kCertificateAuthorityPath);

            bool accepted = ValidateCertificate(
                untrustedCertificate,
                null,
                SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.That(accepted, Is.False);
        }

        /// <summary>
        /// An unreadable internal CA leaves the platform validation failure in effect.
        /// </summary>
        [Test]
        public void ValidateCertificate_RejectsUnreadableCertificateAuthority()
        {
            using X509Certificate2 certificate = CreateSelfSignedCertificate("CN=ldap-missing-ca-test");
            SetCertificateAuthorityPath(kCertificateAuthorityPath);

            bool accepted = ValidateCertificate(
                certificate,
                null,
                SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.That(accepted, Is.False);
        }

        /// <summary>
        /// Enabling TLS installs the certificate callback before attempting the network connection.
        /// </summary>
        [Test]
        public void Connect_WithTlsEnabledConfiguresCertificateValidation()
        {
            Ldap ldap = new()
            {
                Address = "127.0.0.1",
                Port = 1,
                Tls = true
            };

            Assert.ThrowsAsync<LdapConnectionException>(() => ldap.TestConnection());
        }

        /// <summary>
        /// Invokes the private callback with the values normally supplied by the TLS stack.
        /// </summary>
        private static bool ValidateCertificate(
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors errors)
        {
            Ldap ldap = new()
            {
                Address = "ldap.example.test",
                Port = 636
            };
            List<object?> parameters = new() { certificate, chain, errors };
            return TestHelper.InvokeMethod<Ldap, bool>(
                "ValidateLdapServerCertificate",
                parameters.ToArray(),
                ldap);
        }

        /// <summary>
        /// Creates a self-signed end-entity certificate.
        /// </summary>
        private static X509Certificate2 CreateSelfSignedCertificate(string subject)
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            return request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1));
        }

        /// <summary>
        /// Creates a self-signed root certificate authority.
        /// </summary>
        private static X509Certificate2 CreateCertificateAuthority(string subject, ECDsa key)
        {
            CertificateRequest request = CreateCertificateAuthorityRequest(subject, key);
            return request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(2));
        }

        /// <summary>
        /// Creates an intermediate certificate authority issued by the supplied parent.
        /// </summary>
        private static X509Certificate2 CreateIssuedCertificateAuthority(
            string subject,
            ECDsa key,
            X509Certificate2 issuer)
        {
            CertificateRequest request = CreateCertificateAuthorityRequest(subject, key);
            return request.Create(
                issuer,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1),
                RandomNumberGenerator.GetBytes(16));
        }

        /// <summary>
        /// Creates an end-entity certificate issued by the supplied authority.
        /// </summary>
        private static X509Certificate2 CreateIssuedServerCertificate(
            string subject,
            ECDsa key,
            X509Certificate2 issuer)
        {
            CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            return request.Create(
                issuer,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1),
                RandomNumberGenerator.GetBytes(16));
        }

        /// <summary>
        /// Builds the chain that a TLS peer would supply with its leaf certificate.
        /// </summary>
        private static X509Chain BuildPeerChain(
            X509Certificate2 leaf,
            X509Certificate2 intermediate,
            X509Certificate2 root)
        {
            X509Chain peerChain = new();
            peerChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            peerChain.ChainPolicy.CustomTrustStore.Add(root);
            peerChain.ChainPolicy.ExtraStore.Add(intermediate);
            peerChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            if (!peerChain.Build(leaf))
            {
                peerChain.Dispose();
                throw new InvalidOperationException("Could not build the peer certificate chain for the test.");
            }
            return peerChain;
        }

        /// <summary>
        /// Creates a certificate request suitable for a root or intermediate authority.
        /// </summary>
        private static CertificateRequest CreateCertificateAuthorityRequest(string subject, ECDsa key)
        {
            CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            return request;
        }

        /// <summary>
        /// Replaces ConfigFile data with the CA path needed by the current test.
        /// </summary>
        private static void SetCertificateAuthorityPath(string path)
        {
            Type configFileType = typeof(ConfigFile);
            Type? configFileDataType = configFileType.GetNestedType("ConfigFileData", BindingFlags.NonPublic);
            object configFileData = Activator.CreateInstance(
                configFileDataType ?? throw new MissingMemberException(configFileType.FullName, "ConfigFileData"))
                ?? throw new InvalidOperationException("Could not create ConfigFile data.");
            PropertyInfo certificateAuthorityProperty = configFileData.GetType().GetProperty("TlsCaCertificatePath")
                ?? throw new MissingMemberException(configFileData.GetType().FullName, "TlsCaCertificatePath");
            certificateAuthorityProperty.SetValue(configFileData, path);
            GetConfigFileDataProperty().SetValue(null, configFileData);
        }

        /// <summary>
        /// Gets the private process-wide ConfigFile data property.
        /// </summary>
        private static PropertyInfo GetConfigFileDataProperty()
        {
            return typeof(ConfigFile).GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(typeof(ConfigFile).FullName, "Data");
        }
    }
}
