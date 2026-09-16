namespace FWO.Data.Middleware
{
    /// <summary>
    /// Notification log status update sent to the middleware server.
    /// </summary>
    public class NotificationLogUpdateParameters
    {
        public int Id { get; set; }
        public NotificationLogStatus Status { get; set; }
        public string Error { get; set; } = "";
    }
}
