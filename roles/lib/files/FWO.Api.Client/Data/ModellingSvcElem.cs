using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingSvcElem
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int? AppId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; } = "";

        [JsonProperty("is_global"), JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; } = false;


        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeOpt(Name, ref shortened);
            return shortened;
        }
    }
}
