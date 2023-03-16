using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class RequestImplElement : RequestElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("implementation_task_id"), JsonPropertyName("implementation_task_id")]
        public long ImplTaskId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = "create";

        public Cidr Cidr { get; set; } = new Cidr();

        public RequestImplElement()
        {}

        public RequestImplElement(RequestImplElement element) : base(element)
        {
            Id = element.Id;
            ImplTaskId = element.ImplTaskId;
            ImplAction = element.ImplAction;
            Cidr = new Cidr(element.Cidr != null ? element.Cidr.CidrString : "");
        }

        public RequestImplElement(RequestReqElement element)
        {
            Id = 0;
            ImplAction = element.RequestAction;
            Cidr = new Cidr(element.Cidr != null ? element.Cidr.CidrString : "");
            Port = element.Port;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
            UserId = element.UserId;
            OriginalNatId = element.OriginalNatId;
            RuleUid = element.RuleUid;
        }
    }
}
