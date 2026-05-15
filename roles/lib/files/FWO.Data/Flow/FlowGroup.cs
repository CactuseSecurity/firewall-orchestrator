using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public abstract class FlowGroup
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("state"), JsonPropertyName("state")]
        public string State { get; set; } = "requested";

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        public abstract long Id { get; set; }
        public abstract string Hash { get; set; }
    }

    public class FlowNwGroup : FlowGroup
    {
        [JsonProperty("nwgroup_id"), JsonPropertyName("nwgroup_id")]
        public override long Id { get; set; }

        [JsonProperty("nwgrp_hash"), JsonPropertyName("nwgrp_hash")]
        public override string Hash { get; set; } = "";

        [JsonProperty("nwgrp_members"), JsonPropertyName("nwgrp_members")]
        public List<FlowNwGroupMember> NwGroupMembers { get; set; } = new List<FlowNwGroupMember>();

        [JsonProperty("objects"), JsonPropertyName("objects")]
        public List<NetworkObject>? Objects { get; set; }
    }

    public class FlowSvcGroup : FlowGroup
    {
        [JsonProperty("svcgroup_id"), JsonPropertyName("svcgroup_id")]
        public override long Id { get; set; }

        [JsonProperty("svcgrp_hash"), JsonPropertyName("svcgrp_hash")]
        public override string Hash { get; set; } = "";

        [JsonProperty("svcgrp_members"), JsonPropertyName("svcgrp_members")]
        public List<FlowSvcGroupMember> SvcGroupMembers { get; set; } = new List<FlowSvcGroupMember>();

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<NetworkService>? Services { get; set; }
    }

    public class FlowNwGroupMember
    {
        [JsonProperty("nwgroup_id"), JsonPropertyName("nwgroup_id")]
        public long NwGroupId { get; set; }

        [JsonProperty("nwobj_id"), JsonPropertyName("nwobj_id")]
        public long NwObjectId { get; set; }

        [JsonProperty("nwobject"), JsonPropertyName("nwobject")]
        public FlowNwObject NwObject { get; set; } = new FlowNwObject();
    }

    public class FlowSvcGroupMember
    {
        [JsonProperty("svcgroup_id"), JsonPropertyName("svcgroup_id")]
        public long SvcGroupId { get; set; }

        [JsonProperty("svcobj_id"), JsonPropertyName("svcobj_id")]
        public long SvcObjectId { get; set; }

        [JsonProperty("svcobject"), JsonPropertyName("svcobject")]
        public FlowSvcObject SvcObject { get; set; } = new FlowSvcObject();
    }
}
