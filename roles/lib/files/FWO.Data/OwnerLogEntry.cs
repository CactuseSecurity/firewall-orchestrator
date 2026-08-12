using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    /// <summary>
    /// Logged flow of an owner as stored in the logging schema.
    /// </summary>
    public class OwnerLogEntry
    {
        private const int kIpV4MaskLength = 32;
        private const int kIpV6MaskLength = 128;

        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("log_count"), JsonPropertyName("log_count")]
        public int LogCount { get; set; }

        [JsonProperty("source"), JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonProperty("destination"), JsonPropertyName("destination")]
        public string Destination { get; set; } = "";

        [JsonProperty("service_protocol"), JsonPropertyName("service_protocol")]
        public int? ServiceProtocol { get; set; }

        [JsonProperty("service_port"), JsonPropertyName("service_port")]
        public int? ServicePort { get; set; }

        [JsonProperty("allowed"), JsonPropertyName("allowed")]
        public bool Allowed { get; set; }

        [JsonProperty("log_time"), JsonPropertyName("log_time")]
        public DateTimeOffset LogTime { get; set; }

        [JsonProperty("logging_rule_name"), JsonPropertyName("logging_rule_name")]
        public string? LoggingRuleName { get; set; }

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
        /// Service of the logged flow, e.g. "TCP/443", "ICMP" or "47".
        /// </summary>
        public string ServiceDisplay
        {
            get
            {
                string protocol = DisplayProtocol();
                if (ServicePort is null)
                {
                    return protocol;
                }
                return protocol.Length > 0 ? $"{protocol}/{ServicePort}" : ServicePort.Value.ToString();
            }
        }

        /// <summary>
        /// Sort key of the service column, keeping entries of one protocol together.
        /// </summary>
        public long ServiceSortKey => ((long)(ServiceProtocol ?? -1) << 32) + (ServicePort ?? -1);

        private string DisplayProtocol()
        {
            if (!string.IsNullOrWhiteSpace(Protocol?.Name))
            {
                return Protocol.Name.ToUpperInvariant();
            }
            return ServiceProtocol is null ? "" : ServiceProtocol.Value.ToString();
        }

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
