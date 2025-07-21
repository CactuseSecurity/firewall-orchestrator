using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public enum CriterionType
    {
        Matrix = 1,
        NoAnySource = 10
        
        // ...
    }

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
    }

    public class ComplianceCriterionWrapper
    {
        [JsonProperty("criterion"), JsonPropertyName("criterion")]
        public virtual ComplianceCriterion Content { get; set; } = new();
    }
}
