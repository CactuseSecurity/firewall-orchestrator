using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FWO.Test.Helpers
{
    /// <summary>
    /// TLS server on the loopback interface with a self-signed certificate for localhost and 127.0.0.1, which no trust
    /// store accepts. It serves the first connection and counts the bytes the client sends after the handshake, so a
    /// test can show that a client refusing the certificate sent nothing, e.g. no client secret.
    /// </summary>
    internal sealed class SelfSignedTlsServer : IDisposable
    {
        private const string kSubject = "CN=localhost";
        private const string kHostName = "localhost";
        private const int kReadBufferBytes = 4096;
        private static readonly TimeSpan kCertificateValidity = TimeSpan.FromDays(1);

        private readonly TcpListener listener;
        private readonly X509Certificate2 certificate;
        private readonly Task serverTask;
        private int receivedBytes;

        /// <summary>
        /// Creates the certificate, starts listening on a free loopback port and serves the first connection in the background.
        /// </summary>
        public SelfSignedTlsServer()
        {
            certificate = CreateCertificate();
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            serverTask = ServeOneConnection();
        }

        /// <summary>
        /// Port the server listens on.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Bytes the client sent after the handshake.
        /// </summary>
        public int ReceivedBytes => Volatile.Read(ref receivedBytes);

        /// <summary>
        /// Returns an https URL with the given path on this server.
        /// </summary>
        /// <param name="path">Absolute path of the URL</param>
        /// <returns>The URL, with 127.0.0.1 as host.</returns>
        public Uri GetUrl(string path)
        {
            return new Uri($"https://{IPAddress.Loopback}:{Port}{path}");
        }

        /// <summary>
        /// Waits until the client has closed the connection.
        /// </summary>
        /// <returns>A task that completes when the connection ended.</returns>
        public Task WaitForConnectionEnd()
        {
            return serverTask;
        }

        /// <summary>
        /// Stops listening and disposes the certificate.
        /// </summary>
        public void Dispose()
        {
            listener.Stop();
            certificate.Dispose();
        }

        /// <summary>
        /// Accepts one connection, completes the handshake if the client lets it, and counts what the client sends
        /// until it closes the connection. A client refusing the certificate ends the handshake or the connection.
        /// </summary>
        private async Task ServeOneConnection()
        {
            try
            {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                await using SslStream stream = new(client.GetStream());
                await stream.AuthenticateAsServerAsync(certificate);
                byte[] buffer = new byte[kReadBufferBytes];
                int read;
                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    Interlocked.Add(ref receivedBytes, read);
                }
            }
            catch (Exception exception) when (exception is AuthenticationException or IOException or SocketException or ObjectDisposedException)
            {
                // expected: the client refused the certificate and dropped the connection, or the server was disposed
            }
        }

        /// <summary>
        /// Creates a self-signed server certificate for localhost and 127.0.0.1 with a private key that the TLS
        /// implementation of every platform can use (a key of CreateSelfSigned is ephemeral, which SChannel refuses).
        /// </summary>
        private static X509Certificate2 CreateCertificate()
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest request = new(kSubject, key, HashAlgorithmName.SHA256);
            SubjectAlternativeNameBuilder alternativeNames = new();
            alternativeNames.AddDnsName(kHostName);
            alternativeNames.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(alternativeNames.Build());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            using X509Certificate2 selfSigned = request.CreateSelfSigned(now - kCertificateValidity, now + kCertificateValidity);
            return X509CertificateLoader.LoadPkcs12(selfSigned.Export(X509ContentType.Pkcs12), null);
        }
    }
}
