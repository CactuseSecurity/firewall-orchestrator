using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    /// <summary>
    /// Logged flow of an owner as stored in the logging schema.
    /// </summary>
    public class OwnerFirewallLogEntry : FirewallLogEntry
    {
        private const int kIpV4MaskLength = 32;
        private const int kIpV6MaskLength = 128;

        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("protocol_name"), JsonPropertyName("protocol_name")]
        public NetworkProtocol? Protocol { get; set; }

        /// <summary>
        /// Source address without the single host mask the logging schema enforces.
        /// </summary>
        public string SourceDisplay => RemoveHostMask(Source);

        /// <summary>
        /// Destination address without the single host mask the logging schema enforces.
        /// </summary>
        public string DestinationDisplay => RemoveHostMask(Destination);

        /// <summary>
        /// Protocol of the logged flow, e.g. "TCP" or the protocol number when the log data names
        /// a protocol the database does not know. Empty when the source reported none.
        /// Protocol and port are displayed as two columns on purpose: a combined "TCP/443" would
        /// sort its ports as text, which puts TCP/1024 before TCP/443.
        /// </summary>
        public string ProtocolDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Protocol?.Name))
                {
                    return Protocol.Name.ToUpperInvariant();
                }
                return ServiceProtocol is null ? "" : ServiceProtocol.Value.ToString();
            }
        }

        /// <summary>
        /// Log time in the timezone of the server the UI runs on, as the log table displays, sorts
        /// and filters it, and as every other timestamp of the application is displayed. The
        /// browser timezone is not available here: the table renders on the server.
        /// A DateTimeOffset cannot be filtered by the table component.
        /// </summary>
        public DateTime LogTimeLocal => LogTime.ToLocalTime().DateTime;

        private static string RemoveHostMask(string address)
        {
            int maskSeparator = address.IndexOf('/');
            if (maskSeparator < 0)
            {
                return address;
            }

            string mask = address[(maskSeparator + 1)..];
            return mask == kIpV4MaskLength.ToString() || mask == kIpV6MaskLength.ToString()
                ? address[..maskSeparator]
                : address;
        }
    }
}
