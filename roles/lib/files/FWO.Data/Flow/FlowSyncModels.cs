using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Flow
{
    public class FlowSyncManagementData
    {
        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int Id { get; set; }

        [JsonProperty("mgm_name"), JsonPropertyName("mgm_name")]
        public string Name { get; set; } = "";

        [JsonProperty("objects"), JsonPropertyName("objects")]
        public List<NetworkObject> NetworkObjects { get; set; } = [];

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<NetworkService> ServiceObjects { get; set; } = [];

        [JsonProperty("time_objects"), JsonPropertyName("time_objects")]
        public List<TimeObject> TimeObjects { get; set; } = [];

        [JsonProperty("rules"), JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = [];
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
        public FlowNwGroupInsertMembersContainer? NwGroupMembers { get; set; }
    }

    public class FlowNwGroupInsertMembersContainer
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
        public FlowSvcGroupInsertMembersContainer? SvcGroupMembers { get; set; }
    }

    public class FlowSvcGroupInsertMembersContainer
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

        [JsonProperty("allows_traffic"), JsonPropertyName("allows_traffic")]
        public bool AllowsTraffic { get; set; } = true;

        [JsonProperty("access_sources"), JsonPropertyName("access_sources")]
        public FlowAccessInsertMembersContainer? AccessSources { get; set; }

        [JsonProperty("access_source_grps"), JsonPropertyName("access_source_grps")]
        public FlowAccessInsertMembersContainer? AccessSourceGroups { get; set; }

        [JsonProperty("access_destinations"), JsonPropertyName("access_destinations")]
        public FlowAccessInsertMembersContainer? AccessDestinations { get; set; }

        [JsonProperty("access_destination_grps"), JsonPropertyName("access_destination_grps")]
        public FlowAccessInsertMembersContainer? AccessDestinationGroups { get; set; }

        [JsonProperty("access_services"), JsonPropertyName("access_services")]
        public FlowAccessInsertMembersContainer? AccessServices { get; set; }

        [JsonProperty("access_service_grps"), JsonPropertyName("access_service_grps")]
        public FlowAccessInsertMembersContainer? AccessServiceGroups { get; set; }

        [JsonProperty("access_timeobjects"), JsonPropertyName("access_timeobjects")]
        public FlowAccessInsertMembersContainer? AccessTimeObjects { get; set; }
    }

    public class FlowAccessInsertMembersContainer
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<object> Data { get; set; } = [];
    }

    public class FlowMappingUpdate
    {
        public long Id { get; set; }
        public long? FlowId { get; set; }
        public bool FlowActive { get; set; }

    }

    public class FlowRuleMappingUpdate
    {
        public long Id { get; set; }
        public long? FlowId { get; set; }
    }

    public class MutationResult
    {
        [JsonProperty("affected_rows"), JsonPropertyName("affected_rows")]
        public int AffectedRows { get; set; }
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

    public class FlowSyncFlowData
    {
        public readonly Dictionary<string, FlowNwObject> NwObjects = [];
        public readonly Dictionary<string, FlowNwGroup> NwGroups = [];
        public readonly Dictionary<string, FlowSvcObject> SvcObjects = [];
        public readonly Dictionary<string, FlowSvcGroup> SvcGroups = [];
        public readonly Dictionary<string, FlowTimeObject> TimeObjects = [];
        public readonly Dictionary<string, FlowAccess> Accesses = [];
        public readonly Dictionary<long, FlowNwObject> NwObjectsById = [];
        public readonly Dictionary<long, FlowNwGroup> NwGroupsById = [];
        public readonly Dictionary<long, FlowSvcObject> SvcObjectsById = [];
        public readonly Dictionary<long, FlowSvcGroup> SvcGroupsById = [];
        public readonly Dictionary<long, FlowTimeObject> TimeObjectsById = [];

        public Dictionary<long, string> NwObjectHashes { get; private set; } = [];
        public Dictionary<long, string> SvcObjectHashes { get; private set; } = [];
        public Dictionary<long, string> TimeObjectHashes { get; private set; } = [];
        public Dictionary<long, string> AccessHashes { get; private set; } = [];

        public FlowSyncFlowData(List<FlowNwObject> nwObjects, List<FlowNwGroup> nwGroups, List<FlowSvcObject> svcObjects, List<FlowSvcGroup> svcGroups, List<FlowTimeObject> timeObjects, List<FlowAccess> accesses)
        {
            NwObjects = nwObjects.ToDictionary(fo => fo.Hash, fo => fo);
            NwGroups = nwGroups.ToDictionary(fg => fg.Hash, fg => fg);
            SvcObjects = svcObjects.ToDictionary(fs => fs.Hash, fs => fs);
            SvcGroups = svcGroups.ToDictionary(fsg => fsg.Hash, fsg => fsg);
            TimeObjects = timeObjects.ToDictionary(fto => fto.Hash, fto => fto);
            Accesses = accesses.ToDictionary(fa => fa.Hash, fa => fa);
            NwObjectsById = nwObjects.ToDictionary(flowObject => flowObject.Id);
            NwGroupsById = nwGroups.ToDictionary(group => group.Id);
            SvcObjectsById = svcObjects.ToDictionary(flowObject => flowObject.Id);
            SvcGroupsById = svcGroups.ToDictionary(group => group.Id);
            TimeObjectsById = timeObjects.ToDictionary(timeObject => timeObject.Id);

            NwObjectHashes = nwObjects.SelectMany(fo => (fo.Objects ?? Enumerable.Empty<NetworkObject>())
                    .Select(o => new { o.Id, ParentHash = fo.Hash }))
                .ToDictionary(x => x.Id, x => x.ParentHash);
            SvcObjectHashes = svcObjects.SelectMany(fs => (fs.Services ?? Enumerable.Empty<NetworkService>())
                    .Select(s => new { s.Id, ParentHash = fs.Hash }))
                .ToDictionary(x => x.Id, x => x.ParentHash);
            TimeObjectHashes = timeObjects.SelectMany(fto => (fto.TimeObjects ?? Enumerable.Empty<TimeObject>())
                    .Select(to => new { to.Id, ParentHash = fto.Hash }))
                .ToDictionary(x => x.Id, x => x.ParentHash);
            AccessHashes = accesses.SelectMany(fa => (fa.Rules ?? Enumerable.Empty<Rule>())
                    .Select(r => new { r.Id, ParentHash = fa.Hash }))
                .ToDictionary(x => x.Id, x => x.ParentHash);

            // link group members to actual detailed objects from corresponding object lists
            nwGroups.ForEach(g => g.NwGroupMembers.ForEach(m => m.NwObject = NwObjectsById[m.NwObjectId]));
            svcGroups.ForEach(g => g.SvcGroupMembers.ForEach(m => m.SvcObject = SvcObjectsById[m.SvcObjectId]));
            accesses.ForEach(a =>
            {
                a.Sources?.ForEach(s => s.NwObject = NwObjectsById[s.NwObjectId]);
                a.SourceGroups?.ForEach(sg => sg.NwGroup = NwGroupsById[sg.NwGroupId]);
                a.Destinations?.ForEach(d => d.NwObject = NwObjectsById[d.NwObjectId]);
                a.DestinationGroups?.ForEach(dg => dg.NwGroup = NwGroupsById[dg.NwGroupId]);
                a.Services?.ForEach(s => s.SvcObject = SvcObjectsById[s.SvcObjectId]);
                a.ServiceGroups?.ForEach(sg => sg.SvcGroup = SvcGroupsById[sg.SvcGroupId]);
                a.TimeObjects?.ForEach(to => to.TimeObject = TimeObjectsById[to.TimeObjectId]);
            });
        }

        public void Add(FlowNwObject flowObject)
        {
            NwObjectsById[flowObject.Id] = flowObject;
            NwObjects[flowObject.Hash] = flowObject;
        }

        public void Add(FlowNwGroup group)
        {
            NwGroupsById[group.Id] = group;
            NwGroups[group.Hash] = group;
        }

        public void Add(FlowSvcObject flowObject)
        {
            SvcObjectsById[flowObject.Id] = flowObject;
            SvcObjects[flowObject.Hash] = flowObject;
        }

        public void Add(FlowSvcGroup group)
        {
            SvcGroupsById[group.Id] = group;
            SvcGroups[group.Hash] = group;
        }

        public void Add(FlowAccess access)
        {
            Accesses[access.Hash] = access;
        }

        /// <summary>
        /// Checks if there are any inconsistencies between the stored hashes and the hashes calculated from the
        /// current state of the objects using the current hash calculation logic.
        /// Network objects, service objects and time objects where the hash cannot be calculated automatically
        /// (manually created flow objects) are excluded from this check. For groups and accesses, all base
        /// objects contained within the group/access are expected to have valid hashes.
        /// </summary>
        /// <returns></returns>
        public bool HasHashInconsistencies()
        {
            return NwObjects.Values.Any(fo => fo.TryCalculateHash() is string h && h != fo.Hash)
                || SvcObjects.Values.Any(fs => fs.TryCalculateHash() is string h && h != fs.Hash)
                || TimeObjects.Values.Any(fto => fto.TryCalculateHash() is string h && h != fto.Hash)
                || NwGroups.Values.Any(g => g.TryCalculateHash() != g.Hash)
                || SvcGroups.Values.Any(g => g.TryCalculateHash() != g.Hash)
                || Accesses.Values.Any(fa => fa.TryCalculateHash() != fa.Hash);
        }
    }
}
