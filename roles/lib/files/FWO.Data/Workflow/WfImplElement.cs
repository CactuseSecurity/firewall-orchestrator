using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfImplElement : WfElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("implementation_task_id"), JsonPropertyName("implementation_task_id")]
        public long ImplTaskId { get; set; }

        [JsonProperty("implementation_action"), JsonPropertyName("implementation_action")]
        public string ImplAction { get; set; } = "create";

        public Cidr? Cidr { get; set; } = new();
        public Cidr? CidrEnd { get; set; } = new();

        public WfImplElement()
        {}

        public WfImplElement(WfImplElement element) : base(element)
        {
            Id = element.Id;
            ImplTaskId = element.ImplTaskId;
            ImplAction = element.ImplAction;
            Cidr = element.Cidr;
            CidrEnd = element.CidrEnd;
        }

        public WfImplElement(WfReqElement element)
        {
            Id = 0;
            ImplAction = element.RequestAction;
            Cidr = element.Cidr;
            CidrEnd = element.CidrEnd;
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
