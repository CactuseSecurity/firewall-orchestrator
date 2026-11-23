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

        /// <summary>
        /// Set for an element that names an existing object on the firewall by its own name, e.g. an access role standing
        /// in for a modelling placeholder area: the firewall write sends the name as it is, without the vendor's name
        /// normalization for objects FWO creates. Only held in memory, never stored.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public bool KeepNameVerbatim { get; set; }

        public WfImplElement()
        { }

        public WfImplElement(WfImplElement element) : base(element)
        {
            Id = element.Id;
            ImplTaskId = element.ImplTaskId;
            ImplAction = element.ImplAction;
            Cidr = element.Cidr;
            CidrEnd = element.CidrEnd;
            KeepNameVerbatim = element.KeepNameVerbatim;
        }

        public WfImplElement(WfReqElement element)
        {
            Id = 0;
            ImplAction = element.RequestAction;
            IpString = element.IpString;
            IpEnd = element.IpEnd;
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
            Name = element.Name;
            StartTime = element.StartTime;
            EndTime = element.EndTime;
            TimeObjId = element.TimeObjId;
            FlowTimeObjectId = element.FlowTimeObjectId;
            ObjectTaskId = element.ObjectTaskId;
        }
    }
}
