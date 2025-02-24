using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class OwnerIdModel
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }
    }
}
