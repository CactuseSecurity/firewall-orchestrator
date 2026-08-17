using FWO.Basics.Exceptions;
using FWO.Logging;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Config.File
{
    /// <summary>
    /// Provides the trust anchor configured as tls_ca_certificate in the FWO config file.
    /// </summary>
    /// <remarks>
    /// The API connection and the LDAP client both build a pinned chain against this
    /// anchor from inside a TLS validation callback, which runs per handshake - so the
    /// file is parsed once and the result shared, and a failure is not re-read and
    /// re-logged per connection. The cache keys on the configured path and its last
    /// write time, so replacing the anchor takes effect without a service restart:
    /// an installer run may add a customer managed issuer or rotate the internal CA
    /// while only the certificate consuming services would otherwise be restarted.
    /// </remarks>
    public static class InternalCaCertificate
    {
        private const string LogCategory = "Certificates";
        private const string NotConfiguredMessage =
            "The FWO config file has no tls_ca_certificate, so no trust anchor is available. " +
            "An installation upgraded from before the internal CA needs the installer to add it.";

        private static readonly TimeSpan retryInterval = TimeSpan.FromSeconds(30);
        private static readonly object certificateLock = new();
        private static X509Certificate2? certificate;
        private static string cachedPath = "";
        private static DateTime cachedWriteTimeUtc = DateTime.MinValue;
        private static ConfigException? failure;
        private static DateTime failedAt = DateTime.MinValue;

        /// <summary>
        /// Returns the configured trust anchor, reloading it when the file has changed.
        /// </summary>
        /// <remarks>
        /// A replaced anchor is not disposed: a concurrent handshake may still hold it in
        /// the custom trust store of a chain it is building, and the certificates this
        /// returns are shared rather than owned by their callers.
        /// </remarks>
        /// <returns>The CA certificate that API and LDAP server certificates must chain to.</returns>
        /// <exception cref="ConfigException">The anchor is not configured or cannot be read.</exception>
        public static X509Certificate2 Get()
        {
            lock (certificateLock)
            {
                ReadConfiguration(out string path, out DateTime writeTimeUtc, out Exception? configurationError);
                if (path == cachedPath && writeTimeUtc == cachedWriteTimeUtc)
                {
                    if (certificate != null)
                    {
                        return certificate;
                    }
                    if (failure != null && DateTime.UtcNow - failedAt < retryInterval)
                    {
                        // Rethrowing the stored instance directly would overwrite its stack
                        // trace with this call site and lose where the load actually failed.
                        ExceptionDispatchInfo.Capture(failure).Throw();
                    }
                }
                return Load(path, writeTimeUtc, configurationError);
            }
        }

        /// <summary>
        /// Reads the configured anchor path and the point in time it was last written.
        /// </summary>
        /// <param name="path">The configured path, empty when it is not configured.</param>
        /// <param name="writeTimeUtc">Last write time of that file, minimum value when it is absent.</param>
        /// <param name="configurationError">The failure of the config lookup, null when it succeeded.</param>
        private static void ReadConfiguration(out string path, out DateTime writeTimeUtc, out Exception? configurationError)
        {
            try
            {
                path = ConfigFile.TlsCaCertificate;
                configurationError = null;
            }
            catch (Exception exception)
            {
                path = "";
                writeTimeUtc = DateTime.MinValue;
                configurationError = exception;
                return;
            }
            // A missing file reports the minimum value rather than throwing, which is the
            // same key an unconfigured anchor gets and keeps both on the back-off path.
            writeTimeUtc = System.IO.File.GetLastWriteTimeUtc(path);
        }

        /// <summary>
        /// Loads the anchor and remembers the outcome for the file it was loaded from.
        /// </summary>
        /// <param name="path">The configured path.</param>
        /// <param name="writeTimeUtc">Last write time the outcome is remembered for.</param>
        /// <param name="configurationError">The failure of the config lookup, null when it succeeded.</param>
        /// <returns>The loaded trust anchor.</returns>
        /// <exception cref="ConfigException">The anchor is not configured or cannot be read.</exception>
        private static X509Certificate2 Load(string path, DateTime writeTimeUtc, Exception? configurationError)
        {
            cachedPath = path;
            cachedWriteTimeUtc = writeTimeUtc;
            try
            {
                if (configurationError != null)
                {
                    throw new ConfigException(NotConfiguredMessage, configurationError);
                }
                certificate = X509CertificateLoader.LoadCertificateFromFile(path);
                failure = null;
                return certificate;
            }
            catch (Exception exception)
            {
                // A changed file that cannot be read invalidates what was cached for the
                // previous one: serving the superseded anchor would accept certificates
                // this installation no longer trusts.
                certificate = null;
                failure = exception as ConfigException ?? new ConfigException(
                    $"Could not load the trust anchor configured as tls_ca_certificate ({path}). " +
                    "API and LDAP server certificates cannot be validated against it until this is fixed.",
                    exception);
                failedAt = DateTime.UtcNow;
                Log.WriteError(LogCategory, failure.Message);
                throw failure;
            }
        }
    }
}
