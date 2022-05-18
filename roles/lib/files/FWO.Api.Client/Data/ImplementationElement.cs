using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ImplementationElement : RequestElement
    {
        [JsonProperty("original_nat_id"), JsonPropertyName("original_nat_id")]
        public int? OriginalNATId { get; set; }


        public ImplementationElement()
        { }

        public ImplementationElement(RequestElement element)
        {
            Id = 0;
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
