using FWO.Api.Client;
using FWO.Basics.Exceptions;
using FWO.Config.File;
using NUnit.Framework;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Assert = NUnit.Framework.Assert;

namespace FWO.Test
{
    /// <summary>
    /// The API vhost requires a client certificate. These tests cover how that identity
    /// is loaded: once per process, only for https, and with a diagnosable error when the
    /// configured files are missing.
    /// </summary>
    [TestFixture]
    [NonParallelizable] // mutates the static ConfigFile paths that ConfigFileTest also writes
    internal class GraphQlApiConnectionClientCertificateTest
    {
        private const string kClientCertificateSubject = "CN=fwo-client-certificate-test";
        private static readonly string kCertificatePath = Path.Combine(Path.GetTempPath(), "fwo_client_cert_test.crt");
        private static readonly string kPrivateKeyPath = Path.Combine(Path.GetTempPath(), "fwo_client_cert_test.key");
        private static readonly string kApiCaCertificatePath = Path.Combine(Path.GetTempPath(), "fwo_api_ca_test.crt");
        private static X509Certificate2? apiServerCertificate;
        private static string apiCaCertificatePem = "";

        [OneTimeSetUp]
        public void WriteClientIdentity()
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest request = new(kClientCertificateSubject, key, HashAlgorithmName.SHA256);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            File.WriteAllText(kCertificatePath, certificate.ExportCertificatePem());
            File.WriteAllText(kPrivateKeyPath, key.ExportPkcs8PrivateKeyPem());
            CreateApiServerCertificate();
            SetConfiguredPaths(kCertificatePath, kPrivateKeyPath);
        }

        [OneTimeTearDown]
        public void RemoveClientIdentity()
        {
            File.Delete(kCertificatePath);
            File.Delete(kPrivateKeyPath);
            File.Delete(kApiCaCertificatePath);
            apiServerCertificate?.Dispose();
        }

        [SetUp]
        public void ResetCache()
        {
            File.WriteAllText(kApiCaCertificatePath, apiCaCertificatePem);
            SetConfiguredPaths(kCertificatePath, kPrivateKeyPath);
            ClearCachedCertificate();
        }

        /// <summary>
        /// ConfigFile state is process wide, so it must not be left pointing at the
        /// missing or unset paths some of these tests configure.
        /// </summary>
        [TearDown]
        public void RestoreConfiguredPaths()
        {
            SetConfiguredPaths(kCertificatePath, kPrivateKeyPath);
            ClearCachedCertificate();
        }

        /// <summary>
        /// The certificate holds unmanaged key material, so it must be loaded once and
        /// shared rather than re-read for every connection.
        /// </summary>
        [Test]
        public void ClientCertificate_IsLoadedOncePerProcess()
        {
            using HttpClientHandler firstHandler = CreateHttpClientHandler(useTls: true);
            using HttpClientHandler secondHandler = CreateHttpClientHandler(useTls: true);

            Assert.That(firstHandler.ClientCertificates, Has.Count.EqualTo(1));
            Assert.That(secondHandler.ClientCertificates, Has.Count.EqualTo(1));
            Assert.That(ReferenceEquals(firstHandler.ClientCertificates[0], secondHandler.ClientCertificates[0]), Is.True,
                "every connection must reuse the same client certificate instance");
        }

        [Test]
        public void ClientCertificate_IsNotPresentedOverPlainHttp()
        {
            using HttpClientHandler handler = CreateHttpClientHandler(useTls: false);

            Assert.That(handler.ClientCertificates, Is.Empty);
        }

        [Test]
        public void ClientCertificate_HttpConnectionNeedsNoCertificate()
        {
            // guards the plain-http path end to end: constructing must not touch the identity
            Assert.DoesNotThrow(() =>
            {
                using GraphQlApiConnection connection = new("http://localhost");
            });
        }

        /// <summary>
        /// A raw CryptographicException gives no hint which setting is wrong, so the
        /// loader names both config keys and their configured values.
        /// </summary>
        [Test]
        public void LoadClientCertificate_ReportsWhichConfigValuesAreWrong()
        {
            string missingCertificate = Path.Combine(Path.GetTempPath(), "fwo_absent_client.crt");
            string missingKey = Path.Combine(Path.GetTempPath(), "fwo_absent_client.key");
            SetConfiguredPaths(missingCertificate, missingKey);

            try
            {
                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                    () => TestHelper.InvokeMethod<GraphQlApiConnection, X509Certificate2>("LoadClientCertificate"))!;

                Assert.That(thrown.InnerException, Is.TypeOf<ConfigException>());
                Assert.That(thrown.InnerException!.Message, Does.Contain("tls_client_certificate"));
                Assert.That(thrown.InnerException.Message, Does.Contain("tls_client_private_key"));
                Assert.That(thrown.InnerException.Message, Does.Contain(missingCertificate));
                Assert.That(thrown.InnerException.InnerException, Is.Not.Null, "the original failure must be preserved");
            }
            finally
            {
                SetConfiguredPaths(kCertificatePath, kPrivateKeyPath);
            }
        }

        [Test]
        public void LoadClientCertificate_ReadsConfiguredPemPair()
        {
            X509Certificate2 certificate = TestHelper.InvokeMethod<GraphQlApiConnection, X509Certificate2>("LoadClientCertificate");

            Assert.That(certificate.Subject, Is.EqualTo(kClientCertificateSubject));
            Assert.That(certificate.HasPrivateKey, Is.True, "the client identity must carry its private key");
        }

        /// <summary>
        /// The paths themselves throw when the keys are absent from the config file, which is
        /// the likeliest failure on an upgraded installation. Reporting that must not run the
        /// same lookup again, or the ConfigException is replaced by the lookup's own exception.
        /// </summary>
        [Test]
        public void LoadClientCertificate_ReportsUnconfiguredPathsAsConfigError()
        {
            SetConfiguredPaths(null, null);

            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                () => TestHelper.InvokeMethod<GraphQlApiConnection, X509Certificate2>("LoadClientCertificate"))!;

            Assert.That(thrown.InnerException, Is.TypeOf<ConfigException>(),
                "an unset config value must still surface as a ConfigException naming the keys");
            Assert.That(thrown.InnerException!.Message, Does.Contain("tls_client_certificate"));
            Assert.That(thrown.InnerException.Message, Does.Contain("tls_client_private_key"));
            Assert.That(thrown.InnerException.InnerException, Is.Not.Null, "the original failure must be preserved");
        }

        /// <summary>
        /// A failed load must not be remembered: a service that starts before the installer
        /// has finished writing the certificate would otherwise stay broken until restarted.
        /// </summary>
        [Test]
        public void ClientCertificate_RecoversAfterAnEarlierFailure()
        {
            SetConfiguredPaths(Path.Combine(Path.GetTempPath(), "fwo_absent.crt"), Path.Combine(Path.GetTempPath(), "fwo_absent.key"));
            Assert.Throws<TargetInvocationException>(() => CreateHttpClientHandler(useTls: true));

            SetConfiguredPaths(kCertificatePath, kPrivateKeyPath);
            ExpireBackOffWindow();

            using HttpClientHandler handler = CreateHttpClientHandler(useTls: true);
            Assert.That(handler.ClientCertificates, Has.Count.EqualTo(1));
            Assert.That(handler.ClientCertificates[0].Subject, Is.EqualTo(kClientCertificateSubject));
        }

        /// <summary>
        /// Every failed attempt writes a stack trace, and connections are created per user
        /// session, so a permanently broken config must not re-run the load on every call.
        /// </summary>
        [Test]
        public void ClientCertificate_DoesNotRetryWithinBackOffWindow()
        {
            SetConfiguredPaths(null, null);

            ConfigException first = CaptureLoadFailure();
            ConfigException second = CaptureLoadFailure();

            Assert.That(ReferenceEquals(first, second), Is.True,
                "within the back-off window the remembered failure must be rethrown, not reloaded");
            // rethrowing the stored instance directly would overwrite the trace with the
            // rethrow site, hiding where the load actually failed
            Assert.That(second.StackTrace, Does.Contain("LoadClientCertificate"),
                "the rethrown failure must keep the stack trace of the original load");
        }

        [Test]
        public void ApiCertificate_OnlyAcceptsTheConfiguredCertificateAuthority()
        {
            bool accepted = ValidateApiServerCertificate(apiServerCertificate!, SslPolicyErrors.RemoteCertificateChainErrors);
            bool rejectedForWrongName = ValidateApiServerCertificate(apiServerCertificate!, SslPolicyErrors.RemoteCertificateNameMismatch);

            using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest otherRequest = new("CN=untrusted-api", otherKey, HashAlgorithmName.SHA256);
            using X509Certificate2 untrustedCertificate = otherRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            bool rejectedForWrongAuthority = ValidateApiServerCertificate(untrustedCertificate, SslPolicyErrors.RemoteCertificateChainErrors);

            // re-validating the good certificate last proves the rejections above came from
            // the certificates, not from the trust anchor having become unusable
            bool stillAcceptedAfterRejections = ValidateApiServerCertificate(apiServerCertificate!, SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.That(accepted, Is.True);
            Assert.That(rejectedForWrongName, Is.False);
            Assert.That(rejectedForWrongAuthority, Is.False);
            Assert.That(stillAcceptedAfterRejections, Is.True,
                "the cached trust anchor must survive repeated chain builds");
        }

        /// <summary>
        /// A customer managed certificate is normally issued by an intermediate rather than
        /// directly by the root, so the intermediates the peer supplies have to be used.
        /// </summary>
        [Test]
        public void ApiCertificate_AcceptsAChainWithAnIntermediateAuthority()
        {
            using ECDsa rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest rootRequest = new("CN=fwo-api-root-test", rootKey, HashAlgorithmName.SHA256);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            using X509Certificate2 root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));

            using ECDsa intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest intermediateRequest = new("CN=fwo-api-intermediate-test", intermediateKey, HashAlgorithmName.SHA256);
            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            using X509Certificate2 intermediate = intermediateRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            using X509Certificate2 signingIntermediate = intermediate.CopyWithPrivateKey(intermediateKey);

            using ECDsa leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest leafRequest = new("CN=fwo-api-leaf-test", leafKey, HashAlgorithmName.SHA256);
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            using X509Certificate2 leaf = leafRequest.Create(signingIntermediate, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));

            File.WriteAllText(kApiCaCertificatePath, root.ExportCertificatePem());
            ClearCachedCertificate();

            // the TLS stack hands the peer chain to the callback; it holds the intermediate
            using X509Chain peerChain = new();
            peerChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            peerChain.ChainPolicy.CustomTrustStore.Add(root);
            peerChain.ChainPolicy.ExtraStore.Add(intermediate);
            peerChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            peerChain.Build(leaf);

            bool accepted = TestHelper.InvokeMethod<GraphQlApiConnection, bool>(
                "ValidateApiServerCertificate", [leaf, peerChain, SslPolicyErrors.RemoteCertificateChainErrors]);

            Assert.That(accepted, Is.True,
                "a server certificate issued by an intermediate of the configured root must be accepted");
        }

        private static ConfigException CaptureLoadFailure()
        {
            TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
                () => CreateHttpClientHandler(useTls: true))!;
            Assert.That(thrown.InnerException, Is.TypeOf<ConfigException>());
            return (ConfigException)thrown.InnerException!;
        }

        /// <summary>
        /// Drops the cached identity so each test starts from an unloaded state.
        /// </summary>
        private static void ClearCachedCertificate()
        {
            SetStaticField("clientCertificate", null);
            SetStaticField("apiCaCertificate", null);
            SetStaticField("apiCaCertificateFailure", null);
            SetStaticField("apiCaCertificateFailedAt", DateTime.MinValue);
            SetStaticField("clientCertificateFailure", null);
            SetStaticField("clientCertificateFailedAt", DateTime.MinValue);
        }

        /// <summary>
        /// Pretends the retry back-off has elapsed, so recovery can be tested without waiting.
        /// </summary>
        private static void ExpireBackOffWindow()
        {
            SetStaticField("clientCertificateFailedAt", DateTime.MinValue);
        }

        private static void SetStaticField(string name, object? value)
        {
            FieldInfo field = typeof(GraphQlApiConnection).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"GraphQlApiConnection.{name} could not be found.");
            field.SetValue(null, value);
        }

        private static HttpClientHandler CreateHttpClientHandler(bool useTls)
        {
            return TestHelper.InvokeMethod<GraphQlApiConnection, HttpClientHandler>("CreateHttpClientHandler", [useTls]);
        }

        private static bool ValidateApiServerCertificate(X509Certificate2 certificate, SslPolicyErrors errors)
        {
            List<object?> validationParameters = [certificate, null, errors];
            return TestHelper.InvokeMethod<GraphQlApiConnection, bool>("ValidateApiServerCertificate", validationParameters.ToArray());
        }

        private static void CreateApiServerCertificate()
        {
            using ECDsa certificateAuthorityKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest certificateAuthorityRequest = new("CN=fwo-api-ca-test", certificateAuthorityKey, HashAlgorithmName.SHA256);
            certificateAuthorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            certificateAuthorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            using X509Certificate2 certificateAuthority = certificateAuthorityRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            apiCaCertificatePem = certificateAuthority.ExportCertificatePem();
            File.WriteAllText(kApiCaCertificatePath, apiCaCertificatePem);

            using ECDsa serverKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest serverRequest = new("CN=fwo-api-server-test", serverKey, HashAlgorithmName.SHA256);
            serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            apiServerCertificate = serverRequest.Create(certificateAuthority, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
        }

        /// <summary>
        /// Points ConfigFile at the given client identity without loading a whole config file.
        /// </summary>
        private static void SetConfiguredPaths(string? certificatePath, string? privateKeyPath)
        {
            PropertyInfo dataProperty = typeof(ConfigFile).GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ConfigFile.Data could not be found.");
            object data = dataProperty.GetValue(null) ?? throw new InvalidOperationException("ConfigFile.Data is null.");

            data.GetType().GetProperty("TlsClientCertificate")!.SetValue(data, certificatePath);
            data.GetType().GetProperty("TlsClientPrivateKey")!.SetValue(data, privateKeyPath);
            data.GetType().GetProperty("TlsCaCertificate")!.SetValue(data, kApiCaCertificatePath);
            dataProperty.SetValue(null, data);
        }
    }
}
