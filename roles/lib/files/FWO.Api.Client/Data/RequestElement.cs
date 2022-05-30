using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestElement : RequestElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }


        public RequestElement()
        { }

        public RequestElement(RequestElement element)
        {
            Id = element.Id;
            RequestAction = element.RequestAction;
            Ip = element.Ip;
            Port = element.Port;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
        }
    }
}
