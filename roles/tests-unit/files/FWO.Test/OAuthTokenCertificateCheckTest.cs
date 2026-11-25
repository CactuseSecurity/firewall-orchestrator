using FWO.Basics.Exceptions;
using FWO.Basics.OAuth;
using FWO.Data;
using FWO.DeviceAutoDiscovery;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Verifies against a real TLS server with an untrusted certificate that the token clients of all OAuth 2.0 logins
    /// check the certificate of the token endpoint and send no client secret to it: the client of FWO.Basics, and the
    /// clients of the Azure autodiscovery and of the Azure firewall write client, also when certificate checks of the
    /// firewall connections are switched off.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class OAuthTokenCertificateCheckTest
    {
        private const string kTokenPath = "/oauth2/v2.0/token";
        private const string kClientId = "fwo-client";
        private const string kClientSecret = "very-secret-value";
        private const int kRequestTimeoutSeconds = 10;
        private static readonly TimeSpan kRequestTimeout = TimeSpan.FromSeconds(kRequestTimeoutSeconds);
        private static readonly TimeSpan kConnectionEndTimeout = TimeSpan.FromSeconds(5);

        [Test]
        public async Task TokenClient_RefusesUntrustedCertificateOfTheTokenEndpoint()
        {
            using OAuthTokenClient tokenClient = new(requestTimeout: kRequestTimeout);

            await AssertCertificateRefusedWithoutSendingSecret(tokenClient);
        }

        [Test]
        public async Task AzureWriteClient_WithCertificateChecksOff_ChecksTheCertificateOfTheLogin()
        {
            AzureWriteClientWithoutCertificateChecks writeClient = new(new Management { Name = "azure" });
            using OAuthTokenClient tokenClient = writeClient.CreateLoginTokenClient();

            await AssertCertificateRefusedWithoutSendingSecret(tokenClient);
        }

        [Test]
        public async Task AzureAutodiscoveryClient_ChecksTheCertificateOfTheLogin()
        {
            AzureAutodiscoveryClient autodiscoveryClient = new();
            using OAuthTokenClient tokenClient = autodiscoveryClient.CreateLoginTokenClient();

            await AssertCertificateRefusedWithoutSendingSecret(tokenClient);
        }

        /// <summary>
        /// Requests a token from a TLS server with an untrusted certificate and asserts that the request fails because
        /// of the certificate and that the server got no data, i.e. not the client secret.
        /// </summary>
        private static async Task AssertCertificateRefusedWithoutSendingSecret(OAuthTokenClient tokenClient)
        {
            using SelfSignedTlsServer server = new();
            OAuthClientCredentials credentials = new(server.GetUrl(kTokenPath), kClientId, kClientSecret);

            OAuthTokenException? exception = Assert.ThrowsAsync<OAuthTokenException>(async () =>
                await tokenClient.RequestClientCredentialsTokenAsync(credentials, CancellationToken.None));
            await server.WaitForConnectionEnd().WaitAsync(kConnectionEndTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Failure, Is.EqualTo(OAuthTokenFailure.CertificateRejected));
                Assert.That(server.ReceivedBytes, Is.Zero);
            });
        }

        /// <summary>
        /// Azure firewall write client created with the certificate checks of the firewall connections switched off,
        /// giving access to the token client of its Entra ID login.
        /// </summary>
        private sealed class AzureWriteClientWithoutCertificateChecks(Management management)
            : AzureWriteClient(management, checkCertificates: false, responseTimeout: kRequestTimeoutSeconds)
        {
            internal OAuthTokenClient CreateLoginTokenClient() => CreateTokenClient();
        }

        /// <summary>
        /// Azure autodiscovery client giving access to the token client of its Entra ID login.
        /// </summary>
        private sealed class AzureAutodiscoveryClient : AzureClient
        {
            internal OAuthTokenClient CreateLoginTokenClient() => CreateTokenClient();
        }
    }
}
