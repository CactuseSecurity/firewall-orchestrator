using FWO.Basics.Exceptions;
using FWO.Config.File;
using FWO.Logging;
using Novell.Directory.Ldap;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Connects to the servers of an LDAP connection. Several servers are replicas of one directory: they are tried in
    /// order, starting with the one that answered last, so that a failed server delays at most the first operation
    /// after its failure.
    /// </summary>
    public partial class Ldap
    {
        private const int timeOutInMs = 3000;
        private const string LdapTlsLogCategory = "LdapTls";

        // index into Addresses of the server that answered last
        private int preferredAddressIndex;

        /// <summary>
        /// Builds a connection to a server of this LDAP connection, trying each server once.
        /// </summary>
        /// <param name="cancellationToken">Cancels the attempts when the request ends.</param>
        /// <returns>Connection to the first server that answered.</returns>
        /// <exception cref="LdapConnectionException">No server could be reached.</exception>
        protected virtual async Task<ILdapClient> Connect(CancellationToken cancellationToken = default)
        {
            List<string> addresses = Addresses;
            if (addresses.Count == 0)
            {
                throw new LdapConnectionException($"LDAP connection {Id} has no server.");
            }

            int startIndex = Volatile.Read(ref preferredAddressIndex) % addresses.Count;
            LdapConnectionException? lastException = null;
            for (int attempt = 0; attempt < addresses.Count; attempt++)
            {
                int index = (startIndex + attempt) % addresses.Count;
                try
                {
                    ILdapClient connection = await ConnectToServer(addresses[index], cancellationToken);
                    if (index != startIndex)
                    {
                        Volatile.Write(ref preferredAddressIndex, index);
                        Log.WriteWarning("LDAP Connection", $"LDAP server {addresses[startIndex]}:{Port} of connection {Id} is not reachable, using {addresses[index]}:{Port}.");
                    }
                    return connection;
                }
                catch (LdapConnectionException exception)
                {
                    lastException = exception;
                }
            }
            throw new LdapConnectionException($"Error while trying to reach any LDAP server of {Host()}", lastException!);
        }

        /// <summary>
        /// Builds a connection to one server of this LDAP connection.
        /// </summary>
        /// <param name="address">Name or ip address of the server.</param>
        /// <param name="cancellationToken">Cancels the attempt when the request ends.</param>
        /// <returns>Connection to the server.</returns>
        /// <exception cref="LdapConnectionException">The server could not be reached.</exception>
        protected virtual async Task<ILdapClient> ConnectToServer(string address, CancellationToken cancellationToken = default)
        {
            LdapConnection? connection = null;
            try
            {
                LdapConnectionOptions ldapOptions = new();
                if (Tls)
                {
                    ldapOptions.ConfigureRemoteCertificateValidationCallback(
                        (object sen, X509Certificate? cer, X509Chain? cha, SslPolicyErrors err) => ValidateLdapServerCertificate(address, cer, cha, err));
                }
                connection = new(ldapOptions) { SecureSocketLayer = Tls, ConnectionTimeout = timeOutInMs };
                await connection.ConnectAsync(address, Port, cancellationToken);

                // a connection with a deadline belongs to a login: its operations must not outlast the login deadline
                TimeSpan? operationTimeLimit = cancellationToken.CanBeCanceled ? LdapAuthenticationGate.kTotalTimeout : null;
                return new NovellLdapConnectionAdapter(connection, operationTimeLimit, cancellationToken);
            }

            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                connection?.Dispose();
                throw;
            }
            catch (Exception exception)
            {
                connection?.Dispose();
                Log.WriteDebug($"Could not connect to LDAP server {address}:{Port}: ", exception.Message);
                throw new LdapConnectionException($"Error while trying to reach LDAP server {address}:{Port}", exception);
            }
        }

        /// <summary>
        /// Validates the certificate presented by an LDAP server over TLS.
        /// </summary>
        /// <remarks>
        /// Accepts a certificate the platform already trusts, which covers an external
        /// directory using a public or enterprise CA, and additionally accepts one issued
        /// by FWO's own internal CA. Everything else is rejected: this connection carries
        /// the search user's password and the credentials of every user who logs in.
        /// Previously every certificate was accepted unconditionally.
        /// </remarks>
        /// <param name="address">Name or ip address of the server presenting the certificate.</param>
        /// <param name="certificate">The certificate presented by the LDAP server.</param>
        /// <param name="chain">The chain supplied by the TLS stack.</param>
        /// <param name="sslPolicyErrors">Platform TLS validation errors.</param>
        /// <returns>True when the certificate is trusted for this connection.</returns>
        private bool ValidateLdapServerCertificate(string address, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }
            if (certificate == null || sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                Log.WriteWarning(LdapTlsLogCategory,
                    $"Rejected the certificate of LDAP server {address}:{Port}: {sslPolicyErrors}.");
                return false;
            }
            if (ChainsToInternalCertificateAuthority(certificate, chain))
            {
                return true;
            }
            Log.WriteWarning(LdapTlsLogCategory,
                $"Rejected the certificate of LDAP server {address}:{Port}: {sslPolicyErrors}. " +
                "It is neither trusted by this host nor issued by the FWO internal CA. " +
                "Add the issuing CA to the host trust store to use this server over TLS.");
            return false;
        }

        /// <summary>
        /// Builds the presented chain against FWO's internal CA alone.
        /// </summary>
        /// <param name="certificate">The certificate presented by the LDAP server.</param>
        /// <param name="chain">The chain supplied by the TLS stack, used for intermediates.</param>
        /// <returns>True when the internal CA issued the certificate.</returns>
        private static bool ChainsToInternalCertificateAuthority(X509Certificate certificate, X509Chain? chain)
        {
            try
            {
                // Shared and cached rather than re-read here: this runs on every TLS
                // handshake, and the cache reloads when the configured file changes.
                // Not disposed - the instances belong to the cache, not to this call.
                X509Certificate2Collection trustAnchors = InternalCaCertificate.Get();
                using X509Certificate2 serverCertificate = new(certificate);
                using X509Chain pinnedChain = new();
                pinnedChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                pinnedChain.ChainPolicy.CustomTrustStore.AddRange(trustAnchors);
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
            catch (Exception exception)
            {
                // An unreadable or unconfigured trust anchor only means this particular
                // route to acceptance is unavailable; the platform result already stands.
                Log.WriteDebug(LdapTlsLogCategory, $"Could not check the LDAP certificate against the internal CA: {exception.Message}");
                return false;
            }
        }
    }
}
