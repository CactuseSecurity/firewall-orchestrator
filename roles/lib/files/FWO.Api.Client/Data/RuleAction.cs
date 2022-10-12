using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RuleAction
    {
        [JsonProperty("action_id"), JsonPropertyName("action_id")]
        public int Id { get; set; }

        [JsonProperty("action_name"), JsonPropertyName("action_name")]
        public string Name { get; set; } = "";
    }
}
