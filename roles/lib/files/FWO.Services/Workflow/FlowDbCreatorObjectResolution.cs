using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Resolves workflow object snapshots to Flow DB network and service references,
    /// creating the referenced Flow objects when they do not exist yet.
    /// </summary>
    public partial class FlowDbCreator
    {
        private static readonly List<string> kReusableFlowStates = [FlowState.Requested, FlowState.Implemented];

        /// <summary>
        /// Returns whether an existing Flow object may be bound to a new request. Denied and removed
        /// objects are left alone so that a new request does not silently inherit an earlier rejection.
        /// </summary>
        private static bool IsReusableFlowObject(string state, DateTime? removedDate)
        {
            return removedDate == null && kReusableFlowStates.Contains(state);
        }

        private async Task<List<FlowNetworkReference>> ResolveNetworkReferences(IEnumerable<FlowObjectSnapshot> snapshots, FlowSyncFlowData context,
            FlowGroupMaps groupMaps, bool allowGroupNameReference)
        {
            List<FlowNetworkReference> references = [];
            foreach (FlowObjectSnapshot snapshot in snapshots)
            {
                FlowNetworkReference? reference = await ResolveNetworkReference(snapshot, context, groupMaps, allowGroupNameReference);
                if (reference != null)
                {
                    references.Add(reference);
                }
            }
            return references;
        }

        private async Task<FlowNetworkReference?> ResolveNetworkReference(FlowObjectSnapshot snapshot, FlowSyncFlowData context,
            FlowGroupMaps groupMaps, bool allowGroupNameReference)
        {
            if (snapshot.FlowNetworkObjectId.HasValue)
            {
                return TryResolveNetworkObjectId(snapshot, context);
            }
            if (snapshot.FlowNetworkGroupId.HasValue)
            {
                return TryResolveNetworkGroupId(snapshot, context);
            }
            return TryResolveOriginalNetworkObject(snapshot, context)
                ?? TryResolveNetworkGroupName(snapshot, groupMaps, allowGroupNameReference)
                ?? await ResolveOrCreateNetworkObject(snapshot, context);
        }

        private static FlowNetworkReference? TryResolveNetworkObjectId(FlowObjectSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!snapshot.FlowNetworkObjectId.HasValue)
            {
                return null;
            }
            if (context.NwObjectsById.TryGetValue(snapshot.FlowNetworkObjectId.Value, out FlowNwObject? flowObject))
            {
                return FlowNetworkReference.FromObject(flowObject!);
            }
            Log.WriteWarning(LogMessageTitle, $"Could not resolve Flow network object id {snapshot.FlowNetworkObjectId.Value} for workflow element {snapshot.WorkflowElementId}.");
            return null;
        }

        private static FlowNetworkReference? TryResolveNetworkGroupId(FlowObjectSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!snapshot.FlowNetworkGroupId.HasValue)
            {
                return null;
            }
            FlowNetworkReference? groupReference = TryBuildNetworkGroupReference(snapshot.FlowNetworkGroupId.Value, context);
            if (groupReference == null)
            {
                Log.WriteWarning(LogMessageTitle, $"Could not resolve Flow network group id {snapshot.FlowNetworkGroupId.Value} for workflow element {snapshot.WorkflowElementId}.");
            }
            return groupReference;
        }

        private static FlowNetworkReference? TryResolveOriginalNetworkObject(FlowObjectSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!snapshot.OriginalNetworkObjectId.HasValue)
            {
                return null;
            }
            if (context.NwObjectHashes.TryGetValue(snapshot.OriginalNetworkObjectId.Value, out string? originalObjectHash)
                && context.NwObjects.TryGetValue(originalObjectHash, out FlowNwObject? originalFlowObject))
            {
                return FlowNetworkReference.FromObject(originalFlowObject!);
            }
            return null;
        }

        private static FlowNetworkReference? TryResolveNetworkGroupName(FlowObjectSnapshot snapshot, FlowGroupMaps groupMaps, bool allowGroupNameReference)
        {
            if (!allowGroupNameReference || !IsNetworkGroupReference(snapshot))
            {
                return null;
            }
            if (groupMaps.NetworkGroups.TryGetValue(snapshot.GroupName!, out FlowNetworkReference? mappedGroup))
            {
                return mappedGroup;
            }
            Log.WriteWarning(LogMessageTitle, $"Could not resolve Flow network group '{snapshot.GroupName}' for workflow element {snapshot.WorkflowElementId}.");
            return null;
        }

        private async Task<FlowNetworkReference?> ResolveOrCreateNetworkObject(FlowObjectSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!CanCreateNetworkObject(snapshot))
            {
                return null;
            }

            string? ipStart = ToHostAddress(snapshot.Ip, false, snapshot.WorkflowElementId);
            string? ipEnd = ToHostAddress(string.IsNullOrWhiteSpace(snapshot.IpEnd) ? snapshot.Ip : snapshot.IpEnd, true, snapshot.WorkflowElementId);
            if (!SharesAddressFamily(ipStart, ipEnd))
            {
                Log.WriteWarning(LogMessageTitle, $"Could not create a Flow network object for workflow element {snapshot.WorkflowElementId}: " +
                    $"its range starts at '{ipStart}' and ends at '{ipEnd}', which belong to different address families.");
                return null;
            }
            string name = BuildNetworkObjectName(snapshot);
            bool isTechnical = !string.IsNullOrWhiteSpace(ipStart);
            string hash = isTechnical
                ? FlowHashGenerator.GenerateNwObjectHash(ipStart, ipEnd)
                : FlowHashGenerator.GenerateRandomHash();
            FlowNwObject? existingObject = isTechnical
                ? FindNetworkObjectByHash(hash, context)
                : FindReusableNetworkObject(name, context);
            if (existingObject != null)
            {
                return FlowNetworkReference.FromObject(existingObject);
            }

            return FlowNetworkReference.FromObject(await InsertNetworkObject(name, ipStart, ipEnd, hash, context));
        }

        /// <summary>
        /// Reduces a range endpoint to the single host address flow.nwobject accepts: a network is replaced by its
        /// first address when it opens the range and by its last address when it closes it. Request elements carry
        /// whatever netmask the requester supplied, while the flow database only stores /32 and /128 endpoints.
        /// An endpoint which already addresses a single host is returned unchanged, so that the deterministic hash
        /// of an object created before this normalization stays the same and the object is still found by it.
        /// </summary>
        /// <param name="ip">The endpoint as requested, with or without netmask.</param>
        /// <param name="isRangeEnd">Whether the endpoint closes the range instead of opening it.</param>
        /// <param name="workflowElementId">Id of the workflow element the endpoint belongs to, for logging.</param>
        /// <returns>The host address to store, or the unchanged value when it is already one or cannot be parsed.</returns>
        private static string? ToHostAddress(string? ip, bool isRangeEnd, long workflowElementId)
        {
            if (string.IsNullOrWhiteSpace(ip) || AddressesSingleHost(ip))
            {
                return ip;
            }
            if (!ip.TryParseIPStringToRange(out (string start, string end) range))
            {
                Log.WriteWarning(LogMessageTitle, $"Could not read the network endpoint '{ip}' of workflow element {workflowElementId} as an address range, storing it unchanged.");
                return ip;
            }
            // the requested endpoint carried a netmask, so the host address keeps that notation: it is then the
            // string the database returns for the row created from it, which keeps its stored hash valid
            return (isRangeEnd ? range.end : range.start).IpAsCidr();
        }

        /// <summary>
        /// Returns whether both endpoints of a range belong to the same address family. A range from an IPv4 to an
        /// IPv6 address passes every single-endpoint rule but describes nothing, so it is refused instead of stored.
        /// An endpoint which is not set at all is not compared, the paired-null rule of the table covers that case.
        /// </summary>
        /// <param name="ipStart">The address opening the range.</param>
        /// <param name="ipEnd">The address closing the range.</param>
        private static bool SharesAddressFamily(string? ipStart, string? ipEnd)
        {
            return string.IsNullOrWhiteSpace(ipStart) || string.IsNullOrWhiteSpace(ipEnd)
                || ipStart.IsV6Address() == ipEnd.IsV6Address();
        }

        /// <summary>
        /// Returns whether an endpoint addresses exactly one host, either without a netmask at all or with the
        /// full-length mask of its address family. Only those two forms survive StripOffUnnecessaryNetmask
        /// without a netmask, so any endpoint keeping one spans more than a single address.
        /// </summary>
        /// <param name="ip">The endpoint to check, with or without netmask.</param>
        private static bool AddressesSingleHost(string ip)
        {
            return ip.StripOffUnnecessaryNetmask().GetNetmask().Length == 0;
        }

        /// <summary>
        /// Returns the Flow network object carrying the given deterministic hash, if it already exists.
        /// </summary>
        private static FlowNwObject? FindNetworkObjectByHash(string hash, FlowSyncFlowData context)
        {
            return context.NwObjects.TryGetValue(hash, out FlowNwObject? existingObject) ? existingObject : null;
        }

        /// <summary>
        /// Finds an existing name-only Flow network object that can be reused instead of creating a duplicate.
        /// Objects without an IP get a random hash, so the name is the only stable criterion to match them on.
        /// </summary>
        private static FlowNwObject? FindReusableNetworkObject(string name, FlowSyncFlowData context)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            return context.NwObjects.Values
                .Where(flowObject => IsReusableFlowObject(flowObject.State, flowObject.RemovedDate)
                    && string.IsNullOrWhiteSpace(flowObject.IpStart)
                    && string.Equals(flowObject.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(flowObject => flowObject.Id)
                .FirstOrDefault();
        }

        /// <summary>
        /// Inserts a new Flow network object and keeps the local context in sync with the persisted values.
        /// </summary>
        private async Task<FlowNwObject> InsertNetworkObject(string name, string? ipStart, string? ipEnd, string hash, FlowSyncFlowData context)
        {
            FlowNwObjectInsert insert = new()
            {
                Name = name,
                IpStart = ipStart,
                IpEnd = ipEnd,
                NwObjHash = hash,
                State = FlowState.Requested,
                RemovedDate = null,
                ShowInRequestModule = true
            };
            List<FlowNwObjectInsert> objects = [insert];
            FlowNwObject inserted = (await apiConnection.SendQueryAsync<FlowNwObjectInsertResult>(FlowQueries.insertFlowNwObjects, new { objects })).Returning.First();
            inserted.Name = name;
            inserted.IpStart = ipStart;
            inserted.IpEnd = ipEnd;
            inserted.Hash = hash;
            inserted.State = FlowState.Requested;
            inserted.ShowInRequestModule = true;
            context.Add(inserted);
            return inserted;
        }

        private static bool CanCreateNetworkObject(FlowObjectSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Ip) || !string.IsNullOrWhiteSpace(snapshot.Name))
            {
                return true;
            }
            string originalObjectMessage = snapshot.OriginalNetworkObjectId.HasValue ? $" selected network object id {snapshot.OriginalNetworkObjectId.Value}," : "";
            Log.WriteWarning(LogMessageTitle, $"Could not resolve network element {snapshot.WorkflowElementId}:{originalObjectMessage} no matching Flow object/group and no IP or name for creating a Flow object.");
            return false;
        }

        private static FlowNetworkReference? TryBuildNetworkGroupReference(long groupId, FlowSyncFlowData context)
        {
            if (!context.NwGroupsById.TryGetValue(groupId, out FlowNwGroup? group))
            {
                return null;
            }

            List<long> memberObjectIds = [];
            List<string> memberHashes = [];
            foreach (FlowNwGroupMember member in group.NwGroupMembers)
            {
                if (context.NwObjectsById.TryGetValue(member.NwObjectId, out FlowNwObject? memberObject))
                {
                    memberObjectIds.Add(member.NwObjectId);
                    memberHashes.Add(memberObject!.Hash);
                }
            }

            return memberHashes.Count == 0 ? null : FlowNetworkReference.FromGroup(group!, memberObjectIds, memberHashes);
        }

        private async Task<List<FlowServiceReference>> ResolveServiceReferences(IEnumerable<FlowServiceSnapshot> snapshots, FlowSyncFlowData context,
            FlowGroupMaps groupMaps, bool allowGroupNameReference)
        {
            List<FlowServiceReference> references = [];
            foreach (FlowServiceSnapshot snapshot in snapshots)
            {
                FlowServiceReference? reference = await ResolveServiceReference(snapshot, context, groupMaps, allowGroupNameReference);
                if (reference != null)
                {
                    references.Add(reference);
                }
            }
            return references;
        }

        private async Task<FlowServiceReference?> ResolveServiceReference(FlowServiceSnapshot snapshot, FlowSyncFlowData context,
            FlowGroupMaps groupMaps, bool allowGroupNameReference)
        {
            if (snapshot.FlowServiceObjectId.HasValue)
            {
                return TryResolveServiceObjectId(snapshot, context);
            }
            if (snapshot.FlowServiceGroupId.HasValue)
            {
                return TryResolveServiceGroupId(snapshot, context);
            }
            return TryResolveOriginalServiceObject(snapshot, context)
                ?? TryResolveServiceGroupName(snapshot, groupMaps, allowGroupNameReference)
                ?? await ResolveOrCreateServiceObject(snapshot, context);
        }

        private static FlowServiceReference? TryResolveServiceObjectId(FlowServiceSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!snapshot.FlowServiceObjectId.HasValue)
            {
                return null;
            }
            if (context.SvcObjectsById.TryGetValue(snapshot.FlowServiceObjectId.Value, out FlowSvcObject? flowObject))
            {
                return FlowServiceReference.FromObject(flowObject!);
            }
            Log.WriteWarning(LogMessageTitle, $"Could not resolve Flow service object id {snapshot.FlowServiceObjectId.Value} for workflow element {snapshot.WorkflowElementId}.");
            return null;
        }

        private static FlowServiceReference? TryResolveServiceGroupId(FlowServiceSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!snapshot.FlowServiceGroupId.HasValue)
            {
                return null;
            }
            FlowServiceReference? groupReference = TryBuildServiceGroupReference(snapshot.FlowServiceGroupId.Value, context);
            if (groupReference == null)
            {
                Log.WriteWarning(LogMessageTitle, $"Could not resolve Flow service group id {snapshot.FlowServiceGroupId.Value} for workflow element {snapshot.WorkflowElementId}.");
            }
            return groupReference;
        }

        private static FlowServiceReference? TryResolveOriginalServiceObject(FlowServiceSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!snapshot.OriginalServiceId.HasValue)
            {
                return null;
            }
            if (context.SvcObjectHashes.TryGetValue(snapshot.OriginalServiceId.Value, out string? originalServiceHash)
                && context.SvcObjects.TryGetValue(originalServiceHash, out FlowSvcObject? originalFlowObject))
            {
                return FlowServiceReference.FromObject(originalFlowObject!);
            }
            return null;
        }

        private static FlowServiceReference? TryResolveServiceGroupName(FlowServiceSnapshot snapshot, FlowGroupMaps groupMaps, bool allowGroupNameReference)
        {
            if (!allowGroupNameReference || !IsServiceGroupReference(snapshot))
            {
                return null;
            }
            if (groupMaps.ServiceGroups.TryGetValue(snapshot.GroupName!, out FlowServiceReference? mappedGroup))
            {
                return mappedGroup;
            }
            Log.WriteWarning(LogMessageTitle, $"Could not resolve Flow service group '{snapshot.GroupName}' for workflow element {snapshot.WorkflowElementId}.");
            return null;
        }

        private async Task<FlowServiceReference?> ResolveOrCreateServiceObject(FlowServiceSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!CanCreateServiceObject(snapshot))
            {
                return null;
            }

            int protoId = snapshot.ProtoId!.Value;
            int? portEnd = snapshot.PortEnd ?? snapshot.Port;
            string name = BuildServiceObjectName(snapshot, context);
            FlowSvcObject newFlowSvcObject = new()
            {
                ProtoId = protoId,
                PortStart = snapshot.Port,
                PortEnd = portEnd
            };
            string? deterministicHash = newFlowSvcObject.TryCalculateHash();
            string hash = deterministicHash ?? FlowHashGenerator.GenerateRandomHash();
            FlowSvcObject? existingObject = deterministicHash != null
                ? FindServiceObjectByHash(deterministicHash, context)
                : FindReusableServiceObject(name, protoId, context);
            if (existingObject != null)
            {
                return FlowServiceReference.FromObject(existingObject);
            }

            return FlowServiceReference.FromObject(await InsertServiceObject(name, protoId, snapshot.Port, portEnd, hash, context));
        }

        /// <summary>
        /// Returns the Flow service object carrying the given deterministic hash, if it already exists.
        /// </summary>
        private static FlowSvcObject? FindServiceObjectByHash(string hash, FlowSyncFlowData context)
        {
            return context.SvcObjects.TryGetValue(hash, out FlowSvcObject? existingObject) ? existingObject : null;
        }

        /// <summary>
        /// Finds an existing port-less Flow service object that can be reused instead of creating a duplicate.
        /// Services without ports get a random hash, so protocol and name are the only stable criteria to match them on.
        /// </summary>
        private static FlowSvcObject? FindReusableServiceObject(string name, int protoId, FlowSyncFlowData context)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            return context.SvcObjects.Values
                .Where(flowObject => IsReusableFlowObject(flowObject.State, flowObject.RemovedDate)
                    && flowObject.ProtoId == protoId
                    && !flowObject.PortStart.HasValue
                    && string.Equals(flowObject.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(flowObject => flowObject.Id)
                .FirstOrDefault();
        }

        /// <summary>
        /// Inserts a new Flow service object and keeps the local context in sync with the persisted values.
        /// </summary>
        private async Task<FlowSvcObject> InsertServiceObject(string name, int protoId, int? portStart, int? portEnd, string hash, FlowSyncFlowData context)
        {
            // we expect the canonical ANY service object to generally exist already, but as fallback, we need to create it as implemented (the any object may not be set to anything else)
            string state = IsCanonicalAnyServiceObject(protoId, portStart, portEnd) ? FlowState.Implemented : FlowState.Requested;
            FlowSvcObjectInsert insert = new()
            {
                Name = name,
                PortStart = portStart,
                PortEnd = portEnd,
                IpProtoId = protoId,
                SvcObjHash = hash,
                State = state,
                RemovedDate = null,
                ShowInRequestModule = true
            };
            List<FlowSvcObjectInsert> objects = [insert];
            FlowSvcObject inserted = (await apiConnection.SendQueryAsync<FlowSvcObjectInsertResult>(FlowQueries.insertFlowSvcObjects, new { objects })).Returning.First();
            inserted.Name = name;
            inserted.PortStart = portStart;
            inserted.PortEnd = portEnd;
            inserted.ProtoId = protoId;
            inserted.Hash = hash;
            inserted.State = state;
            inserted.ShowInRequestModule = true;
            context.Add(inserted);
            return inserted;
        }

        private static bool IsCanonicalAnyServiceObject(int protoId, int? portStart, int? portEnd)
        {
            return protoId == GlobalConst.kAnyIpProtocolId && !portStart.HasValue && !portEnd.HasValue;
        }

        private static bool CanCreateServiceObject(FlowServiceSnapshot snapshot)
        {
            if (snapshot.ProtoId.HasValue)
            {
                return true;
            }
            string originalServiceMessage = snapshot.OriginalServiceId.HasValue ? $" selected service id {snapshot.OriginalServiceId.Value}," : "";
            Log.WriteWarning(LogMessageTitle, $"Could not resolve service element {snapshot.WorkflowElementId}:{originalServiceMessage} no matching Flow service object/group and no protocol for creating a Flow service object.");
            return false;
        }

        private static FlowServiceReference? TryBuildServiceGroupReference(long groupId, FlowSyncFlowData context)
        {
            if (!context.SvcGroupsById.TryGetValue(groupId, out FlowSvcGroup? group))
            {
                return null;
            }

            List<long> memberObjectIds = [];
            List<string> memberHashes = [];
            foreach (FlowSvcGroupMember member in group.SvcGroupMembers)
            {
                if (context.SvcObjectsById.TryGetValue(member.SvcObjectId, out FlowSvcObject? memberObject))
                {
                    memberObjectIds.Add(member.SvcObjectId);
                    memberHashes.Add(memberObject!.Hash);
                }
            }

            return memberHashes.Count == 0 ? null : FlowServiceReference.FromGroup(group!, memberObjectIds, memberHashes);
        }
    }
}
