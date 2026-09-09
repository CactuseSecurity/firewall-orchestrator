using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public enum NotificationLogStatus
    {
        Pending,
        Sent,
        Failed,
        Suppressed
    }

    /// <summary>
    /// Log entry for a notification send attempt.
    /// </summary>
    public class NotificationLogEntry
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("timestamp"), JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonProperty("notification_id"), JsonPropertyName("notification_id")]
        public int NotificationId { get; set; }

        [JsonProperty("notification_type"), JsonPropertyName("notification_type")]
        public string NotificationType { get; set; } = "";

        [JsonProperty("to"), JsonPropertyName("to")]
        public string To { get; set; } = "";

        [JsonProperty("cc"), JsonPropertyName("cc")]
        public string Cc { get; set; } = "";

        [JsonProperty("bcc"), JsonPropertyName("bcc")]
        public string Bcc { get; set; } = "";

        [JsonProperty("subject"), JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [JsonProperty("deadline_type"), JsonPropertyName("deadline_type")]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public NotificationDeadline DeadlineType { get; set; } = NotificationDeadline.None;

        [JsonProperty("deadline"), JsonPropertyName("deadline")]
        public DateTimeOffset? Deadline { get; set; }

        [JsonProperty("status"), JsonPropertyName("status")]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public NotificationLogStatus Status { get; set; } = NotificationLogStatus.Pending;

        [JsonProperty("error"), JsonPropertyName("error")]
        public string Error { get; set; } = "";
    }

    public class NotificationLogInsertEntry
    {
        [JsonProperty("timestamp"), JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }
        [JsonProperty("notification_id"), JsonPropertyName("notification_id")]
        public int NotificationId { get; set; }
        [JsonProperty("notification_type"), JsonPropertyName("notification_type")]
        public string NotificationType { get; set; } = "";
        [JsonProperty("to"), JsonPropertyName("to")]
        public string To { get; set; } = "";
        [JsonProperty("cc"), JsonPropertyName("cc")]
        public string Cc { get; set; } = "";
        [JsonProperty("bcc"), JsonPropertyName("bcc")]
        public string Bcc { get; set; } = "";
        [JsonProperty("subject"), JsonPropertyName("subject")]
        public string Subject { get; set; } = "";
        [JsonProperty("deadline_type"), JsonPropertyName("deadline_type")]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public NotificationDeadline DeadlineType { get; set; } = NotificationDeadline.None;
        [JsonProperty("deadline"), JsonPropertyName("deadline")]
        public DateTimeOffset? Deadline { get; set; }
        [JsonProperty("status"), JsonPropertyName("status")]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public NotificationLogStatus Status { get; set; } = NotificationLogStatus.Pending;
        [JsonProperty("error"), JsonPropertyName("error")]
        public string Error { get; set; } = "";
    }
}
