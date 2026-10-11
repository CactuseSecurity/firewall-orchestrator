namespace FWO.Data.Middleware
{
    /// <summary>
    /// Identifies an already-created interface request whose notification should be processed.
    /// </summary>
    public class InterfaceRequestNotificationParameters
    {
        /// <summary>
        /// Database ID of the requested modelling connection.
        /// </summary>
        public int ConnectionId { get; set; }
    }
}
