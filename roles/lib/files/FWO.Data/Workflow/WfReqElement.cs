using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfReqElement : WfElementBase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("task_id"), JsonPropertyName("task_id")]
        public long TaskId { get; set; }

        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = Workflow.RequestAction.create.ToString();

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        [JsonProperty("flow_nwobj_id"), JsonPropertyName("flow_nwobj_id")]
        public long? FlowNetworkObjectId { get; set; }

        [JsonProperty("flow_nwgrp_id"), JsonPropertyName("flow_nwgrp_id")]
        public long? FlowNetworkGroupId { get; set; }

        [JsonProperty("flow_svcobj_id"), JsonPropertyName("flow_svcobj_id")]
        public long? FlowServiceObjectId { get; set; }

        [JsonProperty("flow_svcgrp_id"), JsonPropertyName("flow_svcgrp_id")]
        public long? FlowServiceGroupId { get; set; }

        public Cidr? Cidr { get; set; } = new();
        public Cidr? CidrEnd { get; set; } = new();


        public WfReqElement()
        { }

        public WfReqElement(WfReqElement element) : base(element)
        {
            Id = element.Id;
            TaskId = element.TaskId;
            RequestAction = element.RequestAction;
            DeviceId = element.DeviceId;
            FlowNetworkObjectId = element.FlowNetworkObjectId;
            FlowNetworkGroupId = element.FlowNetworkGroupId;
            FlowServiceObjectId = element.FlowServiceObjectId;
            FlowServiceGroupId = element.FlowServiceGroupId;
            Cidr = element.Cidr;
            CidrEnd = element.CidrEnd;
        }
    }
}
