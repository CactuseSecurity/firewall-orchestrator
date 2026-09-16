namespace FWO.Data.Middleware
{
    /// <summary>
    /// Rendered notification email handed to the trusted middleware sender.
    /// </summary>
    public class NotificationEmailSendParameters
    {
        public int NotificationId { get; set; }
        public List<string> To { get; set; } = [];
        public List<string> Cc { get; set; } = [];
        public List<string> Bcc { get; set; } = [];
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public bool Html { get; set; }
        public DateTimeOffset? Deadline { get; set; }
        public List<NotificationEmailAttachment> Attachments { get; set; } = [];
    }

    /// <summary>
    /// One attachment transported with a middleware notification email request.
    /// </summary>
    public class NotificationEmailAttachment
    {
        public string FileName { get; set; } = "attachment";
        public string ContentType { get; set; } = "application/octet-stream";
        public string ContentBase64 { get; set; } = "";
    }
}
