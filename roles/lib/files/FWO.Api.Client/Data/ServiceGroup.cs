using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using NetTools;

namespace FWO.Api.Data
{
    public class ServiceGroup
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("is_global"), JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; }

        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }


        [JsonProperty("services"), JsonPropertyName("services")]
        public List<ServiceWrapper> NetworkServices { get; set; } = new List<ServiceWrapper>{};

    }
}
