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

    /// <summary>
    /// Application, network-area and reverse-DNS information calculated for one logged address.
    /// </summary>
    public class IpMetadata
    {
        [JsonProperty("ip_address"), JsonPropertyName("ip_address")]
        public string IpAddress { get; set; } = "";

        [JsonProperty("app_ids"), JsonPropertyName("app_ids")]
        public List<string> AppIds { get; set; } = [];

        [JsonProperty("area_ids"), JsonPropertyName("area_ids")]
        public List<string> AreaIds { get; set; } = [];

        [JsonProperty("dns"), JsonPropertyName("dns")]
        public string Dns { get; set; } = "";
    }

    /// <summary>
    /// Address range and its relationships used to calculate log IP metadata.
    /// </summary>
    public class IpMetadataSource
    {
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string IpEnd { get; set; } = "";

        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwnerBase? Owner { get; set; }

        [JsonProperty("nwobject_nwgroups"), JsonPropertyName("nwobject_nwgroups")]
        public List<IpMetadataAreaMembership> AreaMemberships { get; set; } = [];
    }

    /// <summary>
    /// Membership of an address range in a network area.
    /// </summary>
    public class IpMetadataAreaMembership
    {
        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public IpMetadataArea? Area { get; set; }
    }

    /// <summary>
    /// Network area an address range belongs to.
    /// </summary>
    public class IpMetadataArea
    {
        [JsonProperty("id_string"), JsonPropertyName("id_string")]
        public string? IdString { get; set; }
    }
}
