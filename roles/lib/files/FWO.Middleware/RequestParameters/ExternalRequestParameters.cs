namespace FWO.Middleware.RequestParameters
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
    }
}
