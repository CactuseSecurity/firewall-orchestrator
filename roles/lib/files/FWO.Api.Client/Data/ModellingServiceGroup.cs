using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingServiceGroup
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
        public List<ModellingServiceWrapper> Services { get; set; } = new();
    }

    public class ModellingServiceGroupWrapper
    {
        [JsonProperty("service_group"), JsonPropertyName("service_group")]
        public ModellingServiceGroup Content { get; set; } = new();

        public static ModellingServiceGroup[] Resolve(List<ModellingServiceGroupWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}
