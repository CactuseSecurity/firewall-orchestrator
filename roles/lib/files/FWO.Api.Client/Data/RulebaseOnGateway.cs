using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RulebaseOnGateway
    {
        [JsonProperty("dev_id"), JsonPropertyName("dev_id")]
        public int DeviceId { get; set; }

        [JsonProperty("rulebase_id"), JsonPropertyName("rulebase_id")]
        public int RulebaseId { get; set; }

        [JsonProperty("order_no"), JsonPropertyName("order_no")]
        public int OrderNo { get; set; }

        [JsonProperty("rulebase"), JsonPropertyName("rulebase")]
        public Rulebase Rulebase { get; set; } = new Rulebase();

    }
}
