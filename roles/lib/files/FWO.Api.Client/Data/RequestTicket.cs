using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicket : RequestTicketBase
    {
        [JsonProperty("tasks"), JsonPropertyName("tasks")]
        public List<RequestTask> Tasks { get; set; } = new List<RequestTask>();


        public RequestTicket()
        { }

        public RequestTicket(RequestTicket ticket)
        {
            Id = ticket.Id;
            Title = ticket.Title;
            CreationDate = ticket.CreationDate;
            CompletionDate = ticket.CompletionDate;
            StateId = ticket.StateId;
            Requester = ticket.Requester;
            RequesterDn = ticket.RequesterDn;
            RequesterGroup = ticket.RequesterGroup;
            TenantId = ticket.TenantId;
            Reason = ticket.Reason;
            Tasks = ticket.Tasks;
        }
    }
}
