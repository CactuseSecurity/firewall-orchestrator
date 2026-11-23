namespace FWO.Data.Middleware
{
    public class ExternalRequestAddParameters
    {
        public long TicketId { get; set; }
    }

    public class ExternalRequestPatchStateParameters
    {
        public long ExtRequestId { get; set; }
        public long TicketId { get; set; }
        public int TaskNumber { get; set; }
        public string ExtQueryVariables { get; set; } = "";
        public string ExtRequestState { get; set; } = "";
        /// <summary>Implementation task of a request writing it to the firewall; null for a request to an external ticket system.</summary>
        public long? ImplTaskId { get; set; }
    }
}
