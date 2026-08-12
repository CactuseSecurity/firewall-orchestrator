using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    /// <summary>
    /// Columns of a logged flow which are both written and read through the API.
    /// </summary>
    public abstract class FirewallLogEntry
    {
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
    }
}
