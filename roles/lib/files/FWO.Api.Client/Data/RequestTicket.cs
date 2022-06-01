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

        public RequestTicket(RequestTicket ticket) : base(ticket)
        {
            Tasks = ticket.Tasks;
        }
    }
}
