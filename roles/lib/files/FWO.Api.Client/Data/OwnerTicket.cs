using FWO.Api.Data;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class OwnerTicket
    {
        [JsonProperty("owner"), JsonPropertyName("owner")]
        public FwoOwner Owner { get; set; } = new();

        [JsonProperty("ticket"), JsonPropertyName("ticket")]
        public WfTicket Ticket { get; set; } = new();
    }
}
