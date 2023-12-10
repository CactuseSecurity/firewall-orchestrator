using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingSvcObject: ModellingObject
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("is_global"), JsonPropertyName("is_global")]
        public bool IsGlobal { get; set; } = false;

        public override string DisplayHtml()
        {
            return $"<span>{(IsGlobal ? "<b>" : "")}{Display()}{(IsGlobal ? "</b>" : "")}</span>";
        }
    }
}
