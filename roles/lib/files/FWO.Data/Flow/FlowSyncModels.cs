using Newtonsoft.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace FWO.Data.Flow
{
    public class FlowSyncManagementData
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("networkObjects"), JsonPropertyName("networkObjects")]
        public List<FWO.Data.NetworkObject> NetworkObjects { get; set; } = [];

        [JsonProperty("serviceObjects"), JsonPropertyName("serviceObjects")]
        public List<FWO.Data.NetworkService> ServiceObjects { get; set; } = [];

        [JsonProperty("timeObjects"), JsonPropertyName("timeObjects")]
        public List<FWO.Data.TimeObject> TimeObjects { get; set; } = [];

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public List<FWO.Data.Rule> Rules { get; set; } = [];
    }

    // Insert DTOs used when creating missing flow entries via GraphQL mutations.

    public class FlowSvcObjectInsert
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("port_start"), JsonPropertyName("port_start")]
        public int? PortStart { get; set; }

        [JsonProperty("port_end"), JsonPropertyName("port_end")]
        public int? PortEnd { get; set; }

        [JsonProperty("ip_proto_id"), JsonPropertyName("ip_proto_id")]
        public int IpProtoId { get; set; }

        [JsonProperty("svcobj_hash"), JsonPropertyName("svcobj_hash")]
        public string? SvcObjHash { get; set; }

        [JsonProperty("state"), JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }
    }

    public class FlowTimeObjectInsert
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("start_time"), JsonPropertyName("start_time")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("end_time"), JsonPropertyName("end_time")]
        public DateTime? EndTime { get; set; }

        [JsonProperty("timeobj_hash"), JsonPropertyName("timeobj_hash")]
        public string? TimeObjHash { get; set; }

        [JsonProperty("state"), JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }
    }

    public class FlowNwGroupMemberInsert
    {
        [JsonProperty("nwobj_id"), JsonPropertyName("nwobj_id")]
        public long NwObjId { get; set; }
    }

    public class FlowNwGroupInsert
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("nwgrp_hash"), JsonPropertyName("nwgrp_hash")]
        public string? NwGrpHash { get; set; }

        [JsonProperty("state"), JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        [JsonProperty("nwgroup_members"), JsonPropertyName("nwgroup_members")]
        public FlowNwGroupMembersContainer? NwGroupMembers { get; set; }
    }

    public class FlowNwGroupMembersContainer
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<FlowNwGroupMemberInsert> Data { get; set; } = [];
    }

    public class FlowSvcGroupMemberInsert
    {
        [JsonProperty("svcobj_id"), JsonPropertyName("svcobj_id")]
        public long SvcObjId { get; set; }
    }

    public class FlowSvcGroupInsert
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("svcgrp_hash"), JsonPropertyName("svcgrp_hash")]
        public string? SvcGrpHash { get; set; }

        [JsonProperty("state"), JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("show_in_request_module"), JsonPropertyName("show_in_request_module")]
        public bool ShowInRequestModule { get; set; }

        [JsonProperty("svcgroup_members"), JsonPropertyName("svcgroup_members")]
        public FlowSvcGroupMembersContainer? SvcGroupMembers { get; set; }
    }

    public class FlowSvcGroupMembersContainer
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<FlowSvcGroupMemberInsert> Data { get; set; } = [];
    }

    public class FlowAccessInsert
    {
        [JsonProperty("access_hash"), JsonPropertyName("access_hash")]
        public string? AccessHash { get; set; }

        [JsonProperty("requester_id"), JsonPropertyName("requester_id")]
        public int? RequesterId { get; set; }

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int? OwnerId { get; set; }

        [JsonProperty("state"), JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonProperty("removed_date"), JsonPropertyName("removed_date")]
        public DateTime? RemovedDate { get; set; }

        [JsonProperty("access_sources"), JsonPropertyName("access_sources")]
        public FlowAccessMembersContainer? AccessSources { get; set; }

        [JsonProperty("access_source_grps"), JsonPropertyName("access_source_grps")]
        public FlowAccessMembersContainer? AccessSourceGroups { get; set; }

        [JsonProperty("access_destinations"), JsonPropertyName("access_destinations")]
        public FlowAccessMembersContainer? AccessDestinations { get; set; }

        [JsonProperty("access_destination_grps"), JsonPropertyName("access_destination_grps")]
        public FlowAccessMembersContainer? AccessDestinationGroups { get; set; }

        [JsonProperty("access_services"), JsonPropertyName("access_services")]
        public FlowAccessMembersContainer? AccessServices { get; set; }

        [JsonProperty("access_service_grps"), JsonPropertyName("access_service_grps")]
        public FlowAccessMembersContainer? AccessServiceGroups { get; set; }

        [JsonProperty("access_timeobjects"), JsonPropertyName("access_timeobjects")]
        public FlowAccessMembersContainer? AccessTimeObjects { get; set; }
    }

    public class FlowAccessMembersContainer
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<object> Data { get; set; } = [];
    }

    // Small reference DTOs used in access member lists
    public class NwRef
    {
        [JsonProperty("nwobj_id"), JsonPropertyName("nwobj_id")]
        public long NwObjId { get; set; }
    }

    public class NwGroupRef
    {
        [JsonProperty("nwgrp_id"), JsonPropertyName("nwgrp_id")]
        public long NwGroupId { get; set; }
    }

    public class SvcRef
    {
        [JsonProperty("svcobj_id"), JsonPropertyName("svcobj_id")]
        public long SvcObjId { get; set; }
    }

    public class SvcGroupRef
    {
        [JsonProperty("svcgrp_id"), JsonPropertyName("svcgrp_id")]
        public long SvcGroupId { get; set; }
    }

    public class TimeRef
    {
        [JsonProperty("timeobj_id"), JsonPropertyName("timeobj_id")]
        public long TimeObjId { get; set; }
    }

    [Newtonsoft.Json.JsonConverter(typeof(FlowSyncFlowDataContainerConverter))]
    public class FlowSyncFlowDataContainer : FlowSyncFlowData
    {
    }

    public class FlowSyncFlowData
    {
        [JsonProperty("flow_nwobject"), JsonPropertyName("flow_nwobject")]
        public List<FlowNwObject> NwObjects { get; set; } = [];

        [JsonProperty("flow_nwgroup"), JsonPropertyName("flow_nwgroup")]
        public List<FlowNwGroup> NwGroups { get; set; } = [];

        [JsonProperty("flow_svcobject"), JsonPropertyName("flow_svcobject")]
        public List<FlowSvcObject> SvcObjects { get; set; } = [];

        [JsonProperty("flow_svcgroup"), JsonPropertyName("flow_svcgroup")]
        public List<FlowSvcGroup> SvcGroups { get; set; } = [];

        [JsonProperty("flow_timeobject"), JsonPropertyName("flow_timeobject")]
        public List<FlowTimeObject> TimeObjects { get; set; } = [];

        [JsonProperty("flow_access"), JsonPropertyName("flow_access")]
        public List<FlowAccess> Accesses { get; set; } = [];

        public Dictionary<long, string> CustomNwObjectHashes => NwObjects.Where(o => o.IpStart == null).ToDictionary(o => o.Id, o => o.Hash);
        public Dictionary<long, string> CustomSvcObjectHashes => SvcObjects.Where(o => o.PortStart == null).ToDictionary(o => o.Id, o => o.Hash);
        public Dictionary<long, string> CustomTimeObjectHashes => TimeObjects.Where(o => o.StartTime == null).ToDictionary(o => o.Id, o => o.Hash);
    }

    /// <summary>
    /// Custom converter for FlowSyncFlowDataContainer to handle flat JSON structures.
    /// Allows deserializing from a flat object with flow_nwobject, flow_nwgroup, etc. properties.
    /// </summary>
    public class FlowSyncFlowDataContainerConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FlowSyncFlowDataContainer);
        }

        public override object? ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            var result = new FlowSyncFlowDataContainer
            {
                NwObjects = jObject["flow_nwobject"]?.ToObject<List<FlowNwObject>>(serializer) ?? [],
                NwGroups = jObject["flow_nwgroup"]?.ToObject<List<FlowNwGroup>>(serializer) ?? [],
                SvcObjects = jObject["flow_svcobject"]?.ToObject<List<FlowSvcObject>>(serializer) ?? [],
                SvcGroups = jObject["flow_svcgroup"]?.ToObject<List<FlowSvcGroup>>(serializer) ?? [],
                TimeObjects = jObject["flow_timeobject"]?.ToObject<List<FlowTimeObject>>(serializer) ?? [],
                Accesses = jObject["flow_access"]?.ToObject<List<FlowAccess>>(serializer) ?? []
            };

            return result;
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}