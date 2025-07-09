using FWO.Basics;
using NetTools;
using Newtonsoft.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class CompliancePolicy
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonProperty("created_date"), JsonPropertyName("created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        [JsonProperty("disabled"), JsonPropertyName("disabled")]
        public bool Disabled { get; set; } = false;
        
    }
}