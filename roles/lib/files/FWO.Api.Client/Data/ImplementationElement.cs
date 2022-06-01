using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ImplementationElement : ElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("implementation_task_id"), JsonPropertyName("implementation_task_id")]
        public int ImplTaskId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = "create";

        public ImplementationElement()
        { }

        public ImplementationElement(ImplementationElement element) : base(element)
        {
            Id = element.Id;
            ImplTaskId = element.ImplTaskId;
            ImplAction = element.ImplAction;
        }

        public ImplementationElement(RequestElement element)
        {
            Id = 0;
            ImplAction = element.RequestAction;
            Ip = element.Ip;
            Port = element.Port;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
            UserId = element.UserId;
            OriginalNatId = element.OriginalNatId;
        }
    }
}
