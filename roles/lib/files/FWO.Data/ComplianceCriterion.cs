using FWO.Basics;
using NetTools;
using Newtonsoft.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class ComplianceCriterion
    {

        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonProperty("criterion_type"), JsonPropertyName("criterion_type")]
        public string CriterionType { get; set; } = "";
        [JsonProperty("content"), JsonPropertyName("content")]
        public string Content { get; set; } = "";
        [JsonProperty("created"), JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        
    }
}