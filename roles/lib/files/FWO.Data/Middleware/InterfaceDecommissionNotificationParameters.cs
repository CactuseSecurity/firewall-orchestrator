namespace FWO.Data.Middleware
{
    /// <summary>
    /// Identifies an interface decommission notification and its optional replacement.
    /// </summary>
    public class InterfaceDecommissionNotificationParameters
    {
        /// <summary>
        /// Database ID of the decommissioned modelling connection.
        /// </summary>
        public int ConnectionId { get; set; }

        /// <summary>
        /// Database ID of the proposed replacement connection, if any.
        /// </summary>
        public int? ReplacementConnectionId { get; set; }

        /// <summary>
        /// Reason supplied by the user for the decommission.
        /// </summary>
        public string Reason { get; set; } = "";
    }
}
