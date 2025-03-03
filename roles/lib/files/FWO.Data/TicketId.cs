using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class TicketId
    {
        [JsonProperty("ticket_id"), JsonPropertyName("ticket_id")]
        public long Id { get; set; }
    }
}
