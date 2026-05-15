using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class FlowAccessSource
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("nwobj_id"), JsonPropertyName("nwobj_id")]
        public long NwObjectId { get; set; }

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();

        [JsonProperty("nwobject"), JsonPropertyName("nwobject")]
        public FlowNwObject NwObject { get; set; } = new FlowNwObject();
    }

    public class FlowAccessSourceGroup
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("nwgroup_id"), JsonPropertyName("nwgroup_id")]
        public long NwGroupId { get; set; }

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();

        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public FlowNwGroup NwGroup { get; set; } = new FlowNwGroup();
    }

    public class FlowAccessDestination
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("nwobj_id"), JsonPropertyName("nwobj_id")]
        public long NwObjectId { get; set; }

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();

        [JsonProperty("nwobject"), JsonPropertyName("nwobject")]
        public FlowNwObject NwObject { get; set; } = new FlowNwObject();
    }

    public class FlowAccessDestinationGroup
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("nwgroup_id"), JsonPropertyName("nwgroup_id")]
        public long NwGroupId { get; set; }

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();

        [JsonProperty("nwgroup"), JsonPropertyName("nwgroup")]
        public FlowNwGroup NwGroup { get; set; } = new FlowNwGroup();
    }

    public class FlowAccessService
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("svcobj_id"), JsonPropertyName("svcobj_id")]
        public long SvcObjectId { get; set; }

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();

        [JsonProperty("svcobject"), JsonPropertyName("svcobject")]
        public FlowSvcObject SvcObject { get; set; } = new FlowSvcObject();

    }

    public class FlowAccessServiceGroup
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("svcgroup_id"), JsonPropertyName("svcgroup_id")]
        public long SvcGroupId { get; set; }

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();

        [JsonProperty("svcgroup"), JsonPropertyName("svcgroup")]
        public FlowSvcGroup SvcGroup { get; set; } = new FlowSvcGroup();
    }

    public class FlowAccessTimeObject
    {
        [JsonProperty("access_id"), JsonPropertyName("access_id")]
        public long AccessId { get; set; }

        [JsonProperty("timeobj_id"), JsonPropertyName("timeobj_id")]
        public long TimeObjectId { get; set; }

        [JsonProperty("access"), JsonPropertyName("access")]
        public FlowAccess Access { get; set; } = new FlowAccess();

        [JsonProperty("timeobject"), JsonPropertyName("timeobject")]
        public FlowTimeObject TimeObject { get; set; } = new FlowTimeObject();
    }
}
