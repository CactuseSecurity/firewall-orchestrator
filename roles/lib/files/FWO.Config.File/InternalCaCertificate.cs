using FWO.Basics.Exceptions;
using FWO.Logging;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Config.File
{
    /// <summary>
    /// Provides the trust anchors configured as tls_ca_certificate in the FWO config file.
    /// </summary>
    /// <remarks>
    /// The API connection and the LDAP client both build a pinned chain against these
    /// anchors from inside a TLS validation callback, which runs per handshake - so the
    /// file is parsed once and the result shared, and a failure is not re-read and
    /// re-logged per connection. The cache keys on the configured path and its last
    /// write time, so replacing the anchors takes effect without a service restart:
    /// an installer run may add a customer managed issuer or rotate the internal CA
    /// while only the certificate consuming services would otherwise be restarted.
    ///
    /// The file is a bundle rather than a single certificate, because one installation
    /// can legitimately need more than one anchor: an upgrade may retain a customer
    /// managed certificate on some Apache endpoints while others keep serving an
    /// internal CA one, and a single anchor cannot cover both.
    /// </remarks>
    public static class InternalCaCertificate
    {
        private const string LogCategory = "Certificates";
        private const string NotConfiguredMessage =
            "The FWO config file has no tls_ca_certificate, so no trust anchor is available. " +
            "An installation upgraded from before the internal CA needs the installer to add it.";

        private const string NoAnchorsMessage =
            "The file configured as tls_ca_certificate ({0}) contains no certificate. " +
            "It must hold the CA certificate, or bundle of CA certificates, that API and " +
            "LDAP server certificates are validated against.";

        private static readonly TimeSpan retryInterval = TimeSpan.FromSeconds(30);
        private static readonly object certificateLock = new();
        private static X509Certificate2Collection? certificates;
        private static string cachedPath = "";
        private static DateTime cachedWriteTimeUtc = DateTime.MinValue;
        private static ConfigException? failure;
        private static DateTime failedAt = DateTime.MinValue;

        /// <summary>
        /// Returns the configured trust anchors, reloading them when the file has changed.
        /// </summary>
        /// <remarks>
        /// Replaced anchors are not disposed: a concurrent handshake may still hold one in
        /// the custom trust store of a chain it is building, and the certificates this
        /// returns are shared rather than owned by their callers.
        /// </remarks>
        /// <returns>The CA certificates API and LDAP server certificates may chain to.</returns>
        /// <exception cref="ConfigException">The anchors are not configured or cannot be read.</exception>
        public static X509Certificate2Collection Get()
        {
            lock (certificateLock)
            {
                ReadConfiguration(out string path, out DateTime writeTimeUtc, out Exception? configurationError);
                if (path == cachedPath && writeTimeUtc == cachedWriteTimeUtc)
                {
                    if (certificates != null)
                    {
                        return certificates;
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
        /// Loads the anchors and remembers the outcome for the file they were loaded from.
        /// </summary>
        /// <param name="path">The configured path.</param>
        /// <param name="writeTimeUtc">Last write time the outcome is remembered for.</param>
        /// <param name="configurationError">The failure of the config lookup, null when it succeeded.</param>
        /// <returns>The loaded trust anchors.</returns>
        /// <exception cref="ConfigException">The anchors are not configured or cannot be read.</exception>
        private static X509Certificate2Collection Load(string path, DateTime writeTimeUtc, Exception? configurationError)
        {
            cachedPath = path;
            cachedWriteTimeUtc = writeTimeUtc;
            try
            {
                if (configurationError != null)
                {
                    throw new ConfigException(NotConfiguredMessage, configurationError);
                }
                X509Certificate2Collection loaded = LoadCertificates(path);
                if (loaded.Count == 0)
                {
                    throw new ConfigException(string.Format(NoAnchorsMessage, path));
                }
                certificates = loaded;
                failure = null;
                return certificates;
            }
            catch (Exception exception)
            {
                // A changed file that cannot be read invalidates what was cached for the
                // previous one: serving the superseded anchors would accept certificates
                // this installation no longer trusts.
                certificates = null;
                failure = exception as ConfigException ?? new ConfigException(
                    $"Could not load the trust anchors configured as tls_ca_certificate ({path}). " +
                    "API and LDAP server certificates cannot be validated against them until this is fixed.",
                    exception);
                failedAt = DateTime.UtcNow;
                Log.WriteError(LogCategory, failure.Message);
                throw failure;
            }
        }

        /// <summary>
        /// Imports every certificate in the configured anchor file.
        /// </summary>
        /// <param name="path">The configured anchor file.</param>
        /// <returns>The imported trust anchors, which can be empty for a file without PEM certificates.</returns>
        private static X509Certificate2Collection LoadCertificates(string path)
        {
            X509Certificate2Collection loaded = [];
            // Reads every certificate in the file, so the configured anchor may be a
            // bundle. A file holding a single certificate is the ordinary case and
            // behaves exactly as before.
            loaded.ImportFromPemFile(path);
            return loaded;
        }

    }
}
