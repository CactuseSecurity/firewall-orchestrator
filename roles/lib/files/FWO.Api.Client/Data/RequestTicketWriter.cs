using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestTicketWriter
    {

        [JsonProperty("data"), JsonPropertyName("data")]
        public List<RequestReqTaskWriter> Tasks { get; set; } = new List<RequestReqTaskWriter>();


        public RequestTicketWriter(RequestTicket ticket)
        {
            foreach(var reqtask in ticket.Tasks)
            {
                Tasks.Add(new RequestReqTaskWriter(reqtask));
            }
        }
    }
}
