using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class WfImplElement : WfElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("implementation_task_id"), JsonPropertyName("implementation_task_id")]
        public long ImplTaskId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = "create";

        public Cidr Cidr { get; set; } = new();
        public Cidr CidrEnd { get; set; } = new();

        public WfImplElement()
        {}

        public WfImplElement(WfImplElement element) : base(element)
        {
            Id = element.Id;
            ImplTaskId = element.ImplTaskId;
            ImplAction = element.ImplAction;
            Cidr = new Cidr(element.Cidr != null ? element.Cidr.CidrString : "");
            CidrEnd = new Cidr(element.CidrEnd != null ? element.CidrEnd.CidrString : "");
        }

        public WfImplElement(WfReqElement element)
        {
            Id = 0;
            ImplAction = element.RequestAction;
            Cidr = new Cidr(element.Cidr != null ? element.Cidr.CidrString : "");
            CidrEnd = new Cidr(element.CidrEnd != null ? element.CidrEnd.CidrString : "");
            Port = element.Port;
            PortEnd = element.PortEnd;
            ProtoId = element.ProtoId;
            NetworkId = element.NetworkId;
            ServiceId = element.ServiceId;
            Field = element.Field;
            UserId = element.UserId;
            OriginalNatId = element.OriginalNatId;
            RuleUid = element.RuleUid;
            GroupName = element.GroupName;
        }
    }
}
