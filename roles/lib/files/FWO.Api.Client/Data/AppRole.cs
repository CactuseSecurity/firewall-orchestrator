using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using NetTools;

namespace FWO.Api.Data
{
    public class AppRole
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int AppId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("appServers"), JsonPropertyName("appServers")]
        public List<NetworkObject> NetworkObjects { get; set; } = new List<NetworkObject>{};

        public NetworkArea Area { get; set; }

    }
}
