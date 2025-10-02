using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data
{
    public enum NotificationClient
    {
        None = 0,
        Recertification = 1,
        ImportChange = 2,
        Compliance = 3
    }

    public enum NotificationChannel
    {
        Email = 1
    }

    public enum NotificationDeadline
    {
        None = 0,
        RecertDate = 1
    }

    public class FwoNotification
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("notification_client"), JsonPropertyName("notification_client")]
        public NotificationClient NotificationClient { get; set; } = NotificationClient.None;

        [JsonProperty("user_id"), JsonPropertyName("user_id")]
        public int? UserId { get; set; }

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int? OwnerId { get; set; }

        [JsonProperty("channel"), JsonPropertyName("channel")]
        public NotificationChannel Channel { get; set; } = NotificationChannel.Email;

        [JsonProperty("recipient_to"), JsonPropertyName("recipient_to")]
        public EmailRecipientOption RecipientTo { get; set; } = EmailRecipientOption.None;

        [JsonProperty("email_address_to"), JsonPropertyName("email_address_to")]
        public string EmailAddressTo { get; set; } = "";

        [JsonProperty("recipient_cc"), JsonPropertyName("recipient_cc")]
        public EmailRecipientOption RecipientCc { get; set; } = EmailRecipientOption.None;

        [JsonProperty("email_address_cc"), JsonPropertyName("email_address_cc")]
        public string EmailAddressCc { get; set; } = "";

        [JsonProperty("email_subject"), JsonPropertyName("email_subject")]
        public string EmailSubject { get; set; } = "";

        [JsonProperty("layout"), JsonPropertyName("layout")]
        public NotificationLayout Layout { get; set; } = NotificationLayout.SimpleText;

        [JsonProperty("deadline"), JsonPropertyName("deadline")]
        public NotificationDeadline Deadline { get; set; } = NotificationDeadline.None;

        [JsonProperty("interval_before_deadline"), JsonPropertyName("interval_before_deadline")]
        public SchedulerInterval IntervalBeforeDeadline { get; set; } = SchedulerInterval.Weeks;

        [JsonProperty("offset_before_deadline"), JsonPropertyName("offset_before_deadline")]
        public int? OffsetBeforeDeadline { get; set; }

        [JsonProperty("repeat_interval_after_deadline"), JsonPropertyName("repeat_interval_after_deadline")]
        public SchedulerInterval RepeatIntervalAfterDeadline { get; set; } = SchedulerInterval.Weeks;

        [JsonProperty("repeat_offset_after_deadline"), JsonPropertyName("repeat_offset_after_deadline")]
        public int? RepeatOffsetAfterDeadline { get; set; }

        [JsonProperty("repetitions_after_deadline"), JsonPropertyName("repetitions_after_deadline")]
        public int? RepetitionsAfterDeadline { get; set; }

        [JsonProperty("last_sent"), JsonPropertyName("last_sent")]
        public DateTime? LastSent { get; set; }
    }
}
