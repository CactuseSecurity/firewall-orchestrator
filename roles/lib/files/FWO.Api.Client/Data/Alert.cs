using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum AlertCode
    {
        SampleDataExisting = 1,

        UiError = 2,

        DailyCheckError = 10,
        NoImport = 11,
        SuccessfulImportOverdue = 12,
        ImportRunningTooLong = 13,
        ImportErrorUnspecific = 14,
        ImportLocked = 15,
        ImportBrokenObjectReferences = 16,
        MalformedIpAddress = 17,
        RecursionLimitReached = 18,
        
        Autodiscovery = 21,
        AutoDiscoveryErrorUnspecific = 22,

        WorkflowAlert = 31,

        ImportAppData = 41,
        ImportAreaSubnetData = 42,

        ImportChangeNotify = 51,

        ExternalRequest = 61
    }

    public class Alert
    {
        [JsonProperty("alert_id"), JsonPropertyName("alert_id")]
        public long Id { get; set; }

        [JsonProperty("ref_alert_id"), JsonPropertyName("ref_alert_id")]
        public long? RefAlert { get; set; }

        [JsonProperty("ref_log_id"), JsonPropertyName("ref_log_id")]
        public long? RefLogId { get; set; }

        [JsonProperty("source"), JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonProperty("title"), JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonProperty("description"), JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonProperty("alert_mgm_id"), JsonPropertyName("alert_mgm_id")]
        public int? ManagementId { get; set; }

        [JsonProperty("alert_dev_id"), JsonPropertyName("alert_dev_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("user_id"), JsonPropertyName("user_id")]
        public int? UserId { get; set; }

        [JsonProperty("alert_timestamp"), JsonPropertyName("alert_timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("ack_by"), JsonPropertyName("ack_by")]
        public int? AcknowledgedBy { get; set; }

        [JsonProperty("ack_timestamp"), JsonPropertyName("ack_timestamp")]
        public DateTime? AckTimestamp { get; set; }

        [JsonProperty("json_data"), JsonPropertyName("json_data")]
        public object? JsonData { get; set; }

        [JsonProperty("alert_code"), JsonPropertyName("alert_code")]
        public AlertCode? AlertCode { get; set; }

    }
    // public class AlertInteractiveDiscovery
    // {
    //     public UiUser UiUser { get; set; } = new UiUser();
    //     public string Title { get; set; } = "";
    //     public string Description { get; set; } = "";
    //     public Management SuperManager { get; set; } = new Management();
    //     public Device Device { get; set; } = new Device();
    //     public Dictionary<string, string> JsonData { get; set; } = new Dictionary<string, string>();
    //     public int RefAlertId { get; set; }
    // }
}
