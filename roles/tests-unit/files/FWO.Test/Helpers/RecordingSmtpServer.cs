using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FWO.Test.Helpers
{
    /// <summary>
    /// Minimal SMTP server on the loopback interface that accepts one connection, accepts every command
    /// and records the envelope recipients and the message data it receives.
    /// </summary>
    internal sealed class RecordingSmtpServer : IDisposable
    {
        private const string kRecipientCommand = "RCPT TO:";
        private readonly TcpListener listener;
        private readonly Task serverTask;
        private readonly List<string> recipients = [];
        private readonly StringBuilder data = new();

        /// <summary>
        /// Starts listening on a free loopback port and serves the first connection in the background.
        /// </summary>
        public RecordingSmtpServer()
        {
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
        /// Envelope recipients of the received message, in the order they were sent.
        /// </summary>
        public IReadOnlyList<string> Recipients => recipients;

        /// <summary>
        /// Raw message data (headers and body) of the received message.
        /// </summary>
        public string Data => data.ToString();

        /// <summary>
        /// Waits until the client has closed the SMTP session.
        /// </summary>
        /// <returns>A task that completes when the session ended.</returns>
        public Task WaitForSessionEnd()
        {
            return serverTask;
        }

        /// <summary>
        /// Returns a loopback port nobody listens on, so a connection attempt is refused at once.
        /// </summary>
        /// <returns>A closed port.</returns>
        public static int GetClosedPort()
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        /// <summary>
        /// Stops listening.
        /// </summary>
        public void Dispose()
        {
            listener.Stop();
        }

        private async Task ServeOneConnection()
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            await using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
            using StreamWriter writer = new(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 localhost");

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                string command = line.Split(' ', 2)[0].ToUpperInvariant();
                switch (command)
                {
                    case "EHLO":
                    case "HELO":
                        await writer.WriteLineAsync("250-localhost");
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "RCPT":
                        recipients.Add(ExtractAddress(line));
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                        await ReadData(reader);
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 Bye");
                        return;
                    default:
                        await writer.WriteLineAsync("250 OK");
                        break;
                }
            }
        }

        private async Task ReadData(StreamReader reader)
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null && line != ".")
            {
                data.AppendLine(line);
            }
        }

        private static string ExtractAddress(string recipientLine)
        {
            string address = recipientLine.Length > kRecipientCommand.Length ? recipientLine[kRecipientCommand.Length..] : "";
            int parameterStart = address.IndexOf('>');
            if (parameterStart >= 0)
            {
                address = address[..(parameterStart + 1)];
            }
            return address.Trim().Trim('<', '>');
        }
    }
}
