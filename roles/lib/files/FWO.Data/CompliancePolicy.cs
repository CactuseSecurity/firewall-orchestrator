using Newtonsoft.Json;
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

        [JsonProperty("criteria"), JsonPropertyName("criteria")]
        public List<ComplianceCriterionWrapper> Criteria { get; set; } = [];
    }
    
    public struct LinkedPolicy
    {
        [JsonProperty("criterion_id"), JsonPropertyName("criterion_id")]
        public int CriterionId { get; set; }

        [JsonProperty("policy_id"), JsonPropertyName("policy_id")]
        public int PolicyId { get; set; }
    }
}
