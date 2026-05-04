using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class RuleFlowMapping
    {
        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long RuleId { get; set; }

        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("rule"), JsonPropertyName("rule")]
        public Rule Rule { get; set; } = new Rule();

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();
    }

    public class FlowNwObjectMapping
    {
        [JsonProperty("flow_nwobj_id"), JsonPropertyName("flow_nwobj_id")]
        public long FlowNwObjectId { get; set; }

        [JsonProperty("obj_id"), JsonPropertyName("obj_id")]
        public long ObjId { get; set; }

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmId { get; set; }

        [JsonProperty("active_on_mgm"), JsonPropertyName("active_on_mgm")]
        public bool ActiveOnMgm { get; set; }

        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject Object { get; set; } = new NetworkObject();

        [JsonProperty("flow_nwobject"), JsonPropertyName("flow_nwobject")]
        public FlowNwObject FlowNwObject { get; set; } = new FlowNwObject();
    }

    public class FlowSvcObjectMapping
    {
        [JsonProperty("flow_svcobj_id"), JsonPropertyName("flow_svcobj_id")]
        public long FlowSvcObjectId { get; set; }

        [JsonProperty("svc_id"), JsonPropertyName("svc_id")]
        public long SvcId { get; set; }

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmId { get; set; }

        [JsonProperty("active_on_mgm"), JsonPropertyName("active_on_mgm")]
        public bool ActiveOnMgm { get; set; }

        [JsonProperty("service"), JsonPropertyName("service")]
        public NetworkService Service { get; set; } = new NetworkService();

        [JsonProperty("flow_svcobject"), JsonPropertyName("flow_svcobject")]
        public FlowSvcObject FlowSvcObject { get; set; } = new FlowSvcObject();
    }

    public class FlowTimeObjectMapping
    {
        [JsonProperty("flow_timeobj_id"), JsonPropertyName("flow_timeobj_id")]
        public long FlowTimeObjId { get; set; }

        [JsonProperty("time_obj_id"), JsonPropertyName("time_obj_id")]
        public long TimeObjId { get; set; }

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmId { get; set; }

        [JsonProperty("active_on_mgm"), JsonPropertyName("active_on_mgm")]
        public bool ActiveOnMgm { get; set; }

        [JsonProperty("time_object"), JsonPropertyName("time_object")]
        public TimeObject TimeObject { get; set; } = new TimeObject();

        [JsonProperty("flow_timeobject"), JsonPropertyName("flow_timeobject")]
        public FlowTimeObject FlowTimeObject { get; set; } = new FlowTimeObject();
    }

    public class FlowNwGroupMapping
    {
        [JsonProperty("flow_nwgroup_id"), JsonPropertyName("flow_nwgroup_id")]
        public long FlowNwGroupId { get; set; }

        [JsonProperty("objgrp_id"), JsonPropertyName("objgrp_id")]
        public long ObjGrpId { get; set; }

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmId { get; set; }

        [JsonProperty("active_on_mgm"), JsonPropertyName("active_on_mgm")]
        public bool ActiveOnMgm { get; set; }

        [JsonProperty("object"), JsonPropertyName("object")]
        public NetworkObject ObjectGroup { get; set; } = new NetworkObject();

        [JsonProperty("flow_nwgroup"), JsonPropertyName("flow_nwgroup")]
        public FlowNwGroup FlowNwGroup { get; set; } = new FlowNwGroup();
    }

    public class FlowSvcGroupMapping
    {
        [JsonProperty("flow_svcgroup_id"), JsonPropertyName("flow_svcgroup_id")]
        public long FlowSvcGroupId { get; set; }

        [JsonProperty("svcgrp_id"), JsonPropertyName("svcgrp_id")]
        public long SvcGrpId { get; set; }

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmId { get; set; }

        [JsonProperty("active_on_mgm"), JsonPropertyName("active_on_mgm")]
        public bool ActiveOnMgm { get; set; }

        [JsonProperty("service"), JsonPropertyName("service")]
        public NetworkService ServiceGroup { get; set; } = new NetworkService();

        [JsonProperty("flow_svcgroup"), JsonPropertyName("flow_svcgroup")]
        public FlowSvcGroup FlowSvcGroup { get; set; } = new FlowSvcGroup();
    }
}
