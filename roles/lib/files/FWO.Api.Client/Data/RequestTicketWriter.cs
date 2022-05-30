using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicketWriter
    {

        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestTaskWriter> Tasks { get; set; } = new List<RequestTaskWriter>();


        public RequestTicketWriter(RequestTicket ticket)
        {
            foreach(var task in ticket.Tasks)
            {
                Tasks.Add(new RequestTaskWriter(task));
            }
        }
    }
}
