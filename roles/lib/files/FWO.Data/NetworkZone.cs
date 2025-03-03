using System.Net;
using System.Text.Json.Serialization;
using NetTools;
using Newtonsoft.Json;

namespace FWO.Data
{
    public class NetworkZone
    {
        [JsonProperty("zone_id"), JsonPropertyName("zone_id")]
        public int Id { get; set; }

        [JsonProperty("zone_name"), JsonPropertyName("zone_name")]
        public string Name { get; set; } = "";

    }
}
