using System;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class LogEntry
    {
        [JsonProperty("data_issue_id"), JsonPropertyName("data_issue_id")]
        public long Id { get; set; }

        [JsonProperty("source"), JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonProperty("severity"), JsonPropertyName("severity")]
        public int Severity { get; set; }

        [JsonProperty("issue_timestamp"), JsonPropertyName("issue_timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("suspected_cause"), JsonPropertyName("suspected_cause")]
        public string? SuspectedCause { get; set; }

        [JsonProperty("issue_mgm_id"), JsonPropertyName("issue_mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("issue_dev_id"), JsonPropertyName("issue_dev_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("import_id"), JsonPropertyName("import_id")]
        public long? ImportId { get; set; }

        [JsonProperty("object_type"), JsonPropertyName("object_type")]
        public string? ObjectType { get; set; }

        [JsonProperty("object_name"), JsonPropertyName("object_name")]
        public string? ObjectName { get; set; }

        [JsonProperty("object_uid"), JsonPropertyName("object_uid")]
        public string? ObjectUid { get; set; }

        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string? RuleUid { get; set; }

        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long? RuleId { get; set; }

        [JsonProperty("description"), JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonProperty("user_id"), JsonPropertyName("user_id")]
        public int? UserId { get; set; }

        [JsonProperty("ack_by"), JsonPropertyName("ack_by")]
        public int? AcknowledgedBy { get; set; }

        [JsonProperty("ack_timestamp"), JsonPropertyName("ack_timestamp")]
        public DateTime? AckTimestamp { get; set; }

        [JsonProperty("json_data"), JsonPropertyName("json_data")]
        public String? JsonData { get; set; }
    }
}
