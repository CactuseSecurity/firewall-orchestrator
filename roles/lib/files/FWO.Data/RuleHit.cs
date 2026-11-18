using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    /// <summary>
    /// The last hit of a rule on one gateway (firewall.rule_hit), written by the rule hit import.
    /// </summary>
    public class RuleHit
    {
        private DateTime lastHit;

        /// <summary>
        /// Gets or sets the database id of the gateway.
        /// </summary>
        [JsonProperty("dev_id"), JsonPropertyName("dev_id")]
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the last hit of the rule on the gateway, normalized to UTC.
        /// </summary>
        [JsonProperty("rule_last_hit"), JsonPropertyName("rule_last_hit")]
        public DateTime LastHit
        {
            get => lastHit;
            set => lastHit = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        }

        /// <summary>
        /// Gets or sets the gateway; only its name is read, and it is null when the query does not select it.
        /// </summary>
        [JsonProperty("device"), JsonPropertyName("device")]
        public RuleHitGateway? Gateway { get; set; }
    }

    /// <summary>
    /// The gateway of a rule hit as selected by the rule fragments.
    /// </summary>
    public class RuleHitGateway
    {
        /// <summary>
        /// Gets or sets the name of the gateway.
        /// </summary>
        [JsonProperty("dev_name"), JsonPropertyName("dev_name")]
        public string? Name { get; set; }
    }
}
