using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public abstract class FlowGroup
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = FlowState.Requested;

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        public abstract long Id { get; set; }
        public abstract string Hash { get; set; }
    }

    public class FlowNwGroup : FlowGroup
    {
        [JsonProperty("nwgrp_id"), JsonPropertyName("nwgrp_id")]
        public override long Id { get; set; }

        [JsonProperty("nwgrp_hash"), JsonPropertyName("nwgrp_hash")]
        public override string Hash { get; set; } = "";

        [JsonProperty("nwgroup_members"), JsonPropertyName("nwgroup_members")]
        public List<FlowNwGroupMember> NwGroupMembers { get; set; } = [];

        [JsonProperty("objects"), JsonPropertyName("objects")]
        public List<NetworkObject>? Objects { get; set; }
    }

    public class FlowSvcGroup : FlowGroup
    {
        [JsonProperty("svcgrp_id"), JsonPropertyName("svcgrp_id")]
        public override long Id { get; set; }

        [JsonProperty("svcgrp_hash"), JsonPropertyName("svcgrp_hash")]
        public override string Hash { get; set; } = "";

        [JsonProperty("svcgroup_members"), JsonPropertyName("svcgroup_members")]
        public List<FlowSvcGroupMember> SvcGroupMembers { get; set; } = [];

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<NetworkService>? Services { get; set; }
    }

    public class FlowNwGroupMember
    {
        [JsonProperty("nwgrp_id"), JsonPropertyName("nwgrp_id")]
        public long NwGroupId { get; set; }

        [JsonProperty("nwobj_id"), JsonPropertyName("nwobj_id")]
        public long NwObjectId { get; set; }

        [JsonProperty("nwobject"), JsonPropertyName("nwobject")]
        public FlowNwObject NwObject { get; set; } = new FlowNwObject();
    }

    public class FlowSvcGroupMember
    {
        [JsonProperty("svcgrp_id"), JsonPropertyName("svcgrp_id")]
        public long SvcGroupId { get; set; }

        [JsonProperty("svcobj_id"), JsonPropertyName("svcobj_id")]
        public long SvcObjectId { get; set; }

        [JsonProperty("svcobject"), JsonPropertyName("svcobject")]
        public FlowSvcObject SvcObject { get; set; } = new FlowSvcObject();
    }

    public class FlowNwGroupInsertResult
    {
        [JsonProperty("returning"), JsonPropertyName("returning")]
        public List<FlowNwGroup> Returning { get; set; } = [];
    }

    public class FlowSvcGroupInsertResult
    {
        [JsonProperty("returning"), JsonPropertyName("returning")]
        public List<FlowSvcGroup> Returning { get; set; } = [];
    }
}
