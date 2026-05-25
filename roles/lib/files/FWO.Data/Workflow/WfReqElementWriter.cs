using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfReqElementWriter : WfElementBase
    {
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

        public WfReqElementWriter(WfReqElement element) : base(element)
        {
            RequestAction = element.RequestAction;
            DeviceId = element.DeviceId;
            FlowNetworkObjectId = element.FlowNetworkObjectId;
            FlowNetworkGroupId = element.FlowNetworkGroupId;
            FlowServiceObjectId = element.FlowServiceObjectId;
            FlowServiceGroupId = element.FlowServiceGroupId;
            IpString = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null;
            IpEnd = element.CidrEnd != null && element.CidrEnd.Valid ? element.CidrEnd.CidrString : null;
        }
    }
}
