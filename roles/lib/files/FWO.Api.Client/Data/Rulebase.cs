using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class Rulebase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmtId { get; set; }

        [JsonProperty("is_global"), JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; }

        [JsonProperty("created"), JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public long Removed { get; set; }

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public Rule[] Rules { get; set; } = [];

    }
}
