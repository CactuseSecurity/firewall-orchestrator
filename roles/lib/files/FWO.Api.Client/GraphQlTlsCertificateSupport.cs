using FWO.Basics.Exceptions;
using FWO.Config.File;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Api.Client
{
    /// <summary>
    /// Loads the API client identity and validates API server certificates.
    /// </summary>
    internal static class GraphQlTlsCertificateSupport
    {
        private static readonly TimeSpan clientCertificateRetryInterval = TimeSpan.FromSeconds(30);
        private static readonly object clientCertificateLock = new();
        private static X509Certificate2? clientCertificate;
        private static ConfigException? clientCertificateFailure;
        private static DateTime clientCertificateFailedAt = DateTime.MinValue;

        /// <summary>
        /// Returns the local FWO client identity, loading it on first use and reusing it after.
        /// </summary>
        /// <remarks>
        /// Holds unmanaged key material, so it must not be re-created per connection, and
        /// it is deliberately never disposed: it lives for the lifetime of the process and
        /// the installer restarts the services when it renews the certificate.
        /// Only a successful load is cached, so a service that starts while the certificate
        /// is still unreadable recovers instead of failing permanently. A failure is retried
        /// no more than once per <see cref="clientCertificateRetryInterval"/>, because every
        /// attempt writes a stack trace and connections are created per user session.
        /// This is a method rather than a property because it can fail (S2372).
        /// </remarks>
        /// <returns>The client identity presented to the API server.</returns>
        /// <exception cref="ConfigException">The certificate or key is missing or unreadable.</exception>
        internal static X509Certificate2 GetClientCertificate()
        {
            lock (clientCertificateLock)
            {
                if (clientCertificate != null)
                {
                    return clientCertificate;
                }
                if (clientCertificateFailure != null
                    && DateTime.UtcNow - clientCertificateFailedAt < clientCertificateRetryInterval)
                {
                    // Rethrowing the stored instance directly would overwrite its stack trace
                    // with this call site on every attempt, losing where the load actually failed.
                    ExceptionDispatchInfo.Capture(clientCertificateFailure).Throw();
                }
                try
                {
                    clientCertificate = LoadClientCertificate();
                    clientCertificateFailure = null;
                    return clientCertificate;
                }
                catch (ConfigException exception)
                {
                    clientCertificateFailure = exception;
                    clientCertificateFailedAt = DateTime.UtcNow;
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads the client certificate and its private key from the paths in the FWO config file.
        /// </summary>
        /// <returns>The client identity presented to the API server.</returns>
        /// <exception cref="ConfigException">The paths are not configured, or the files cannot be read.</exception>
        internal static X509Certificate2 LoadClientCertificate()
        {
            string certificatePath;
            string privateKeyPath;

            // Read the paths before the load, so reporting a failure cannot trigger the
            // very config lookup that failed and replace the exception with its own.
            try
            {
                certificatePath = ConfigFile.TlsClientCertificate;
                privateKeyPath = ConfigFile.TlsClientPrivateKey;
            }
            catch (Exception exception)
            {
                throw new ConfigException("The API requires a client certificate, but tls_client_certificate " +
                    "and tls_client_private_key are not set in the FWO config file. An installation upgraded " +
                    "from before the internal CA needs the installer to add them.", exception);
            }

            try
            {
                return X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
            }
            catch (Exception exception)
            {
                throw new ConfigException($"Could not load the FWO client certificate from " +
                    $"tls_client_certificate ({certificatePath}) and " +
                    $"tls_client_private_key ({privateKeyPath}). " +
                    $"Check that the files exist and are readable by this service.", exception);
            }
        }

        /// <summary>
        /// Validates an API server certificate against the configured CA and requested host name.
        /// </summary>
        /// <param name="certificate">The API server certificate.</param>
        /// <param name="chain">The chain supplied by the TLS stack.</param>
        /// <param name="sslPolicyErrors">Platform TLS validation errors.</param>
        /// <returns>True only when the certificate name and configured CA chain are valid.</returns>
        internal static bool ValidateApiServerCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null || sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                return false;
            }

            X509Certificate2Collection trustAnchors;
            try
            {
                // Shared and cached: they are reloaded only when the configured file changes,
                // so a rotated anchor takes effect without restarting this service.
                trustAnchors = InternalCaCertificate.Get();
            }
            catch (ConfigException)
            {
                // Throwing out of a validation callback surfaces as an opaque
                // AuthenticationException. The loader has already logged the cause, so
                // reject the certificate instead; the connection fails either way.
                return false;
            }

            using X509Certificate2 serverCertificate = new(certificate);
            using X509Chain pinnedChain = new();
            pinnedChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            // Disposing the chain does not dispose the custom trust store, so the cached
            // anchors stay usable for later handshakes. More than one is expected: an
            // installation that retained a customer managed certificate on part of its
            // Apache endpoints trusts that issuer alongside the internal CA.
            pinnedChain.ChainPolicy.CustomTrustStore.AddRange(trustAnchors);
            // The peer supplies its intermediates in the chain handed to this callback.
            // Without them a root -> intermediate -> leaf chain cannot be built, which is
            // the usual shape of a customer managed certificate.
            if (chain != null)
            {
                foreach (X509ChainElement element in chain.ChainElements)
                {
                    pinnedChain.ChainPolicy.ExtraStore.Add(element.Certificate);
                }
            }
            pinnedChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return pinnedChain.Build(serverCertificate);
        }

        /// <summary>
        /// Creates the message handler, presenting the client identity when TLS is used.
        /// </summary>
        /// <param name="useTls">Whether the API server is addressed over https.</param>
        /// <returns>The handler used by the GraphQL client.</returns>
        internal static HttpClientHandler CreateHttpClientHandler(bool useTls)
        {
            HttpClientHandler handler = new();
            if (useTls)
            {
                handler.ClientCertificates.Add(GetClientCertificate());
                handler.ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) => ValidateApiServerCertificate(certificate, chain, errors);
            }
            return handler;
        }
    }
}
