using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingHistoryEntry
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("app_id"), JsonPropertyName("app_id")]
        public int AppId { get; set; }

        [JsonProperty("change_type"), JsonPropertyName("change_type")]
        public int ChangeType { get; set; }

        [JsonProperty("object_type"), JsonPropertyName("object_type")]
        public int ObjectType { get; set; }

        [JsonProperty("object_id"), JsonPropertyName("object_id")]
        public long ObjectId { get; set; }

        [JsonProperty("change_text"), JsonPropertyName("change_text")]
        public string ChangeText { get; set; } = "";

        [JsonProperty("changer"), JsonPropertyName("changer")]
        public string Changer { get; set; } = "";

        [JsonProperty("change_time"), JsonPropertyName("change_time")]
        public DateTime? ChangeTime { get; set; }
    }
}
