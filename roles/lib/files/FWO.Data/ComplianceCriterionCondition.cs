using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public static class ComplianceConditionFields
    {
        public const string ServiceUid = "service_uid";
        public const string Protocol = "protocol";
        public const string Port = "port";
    }

    public static class ComplianceConditionOperators
    {
        public const string Equal = "equal";
        public const string In = "in";
        public const string Overlaps = "overlaps";
    }

    public class ComplianceCriterionCondition
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("criterion_id"), JsonPropertyName("criterion_id")]
        public int CriterionId { get; set; }

        [JsonProperty("group_order"), JsonPropertyName("group_order")]
        public int GroupOrder { get; set; } = 1;

        [JsonProperty("position"), JsonPropertyName("position")]
        public int Position { get; set; } = 1;

        [JsonProperty("field"), JsonPropertyName("field")]
        public string Field { get; set; } = "";

        [JsonProperty("operator"), JsonPropertyName("operator")]
        public string Operator { get; set; } = "";

        [JsonProperty("value_string"), JsonPropertyName("value_string")]
        public string? ValueString { get; set; }

        [JsonProperty("value_int"), JsonPropertyName("value_int")]
        public int? ValueInt { get; set; }

        [JsonProperty("value_int_end"), JsonPropertyName("value_int_end")]
        public int? ValueIntEnd { get; set; }

        [JsonProperty("value_ref"), JsonPropertyName("value_ref")]
        public long? ValueRef { get; set; }

        [JsonProperty("created"), JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public DateTime? Removed { get; set; }
    }
}
