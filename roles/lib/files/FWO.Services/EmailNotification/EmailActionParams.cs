using FWO.Data;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Services
{
    public enum EmailAttachedContent
    {
        None = 0,
        RequestedConnections = 1
    }

    public class EmailActionParams
    {
        [JsonProperty("notification_ids"), JsonPropertyName("notification_ids")]
        public List<int> NotificationIds { get; set; } = [];

        [JsonProperty("attached_content"), JsonPropertyName("attached_content")]
        public EmailAttachedContent AttachedContent { get; set; } = EmailAttachedContent.None;

        [JsonProperty("confirm_sent_mail"), JsonPropertyName("confirm_sent_mail")]
        public bool ConfirmSentMail { get; set; }

        [JsonProperty("to"), JsonPropertyName("to")]
        public EmailRecipientOption RecipientTo { get; set; } = EmailRecipientOption.None;

        [JsonProperty("cc"), JsonPropertyName("cc")]
        public EmailRecipientOption? RecipientCC { get; set; }

        [JsonProperty("subject"), JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [JsonProperty("body"), JsonPropertyName("body")]
        public string Body { get; set; } = "";

        /// <summary>
        /// Converts workflow action email parameters into the shared notification model.
        /// </summary>
        public FwoNotification ToNotification()
        {
            return new FwoNotification
            {
                NotificationClient = NotificationClient.WfAction,
                Channel = NotificationChannel.Email,
                RecipientTo = RecipientTo,
                RecipientCc = RecipientCC ?? EmailRecipientOption.None,
                EmailSubject = Subject,
                EmailBody = Body,
                Deadline = NotificationDeadline.None
            };
        }
    }
}
