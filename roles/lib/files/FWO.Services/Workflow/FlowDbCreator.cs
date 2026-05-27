using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Creates Flow DB entries from workflow request task data.
    /// </summary>
    public class FlowDbCreator
    {
        private const string LogMessageTitle = "Create Flow";
        private readonly ApiConnection apiConnection;

        public FlowDbCreator(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }

        public async Task<bool?> CreateFlowInFlowDb(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope,
            FwoOwner? owner, long? ticketId)
        {
            List<FlowCreationPayload> payloads = BuildFlowCreationPayloads(statefulObject, scope, owner, ticketId);
            if (payloads.Count == 0)
            {
                Log.WriteWarning(LogMessageTitle, $"Flow creation action '{action.Name}' found no request task flow data.");
                return false;
            }

            payloads = new FlowPayloadMerger().MergeBundled(payloads);

            return await PersistFlowCreationPayloads(payloads);
        }

        public static List<FlowCreationPayload> BuildFlowCreationPayloads(WfStatefulObject statefulObject, WfObjectScopes scope,
            FwoOwner? owner, long? ticketId)
        {
            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => BuildTicketFlowPayloads(ticket, owner, ticketId),
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => [BuildRequestTaskFlowPayload(reqTask, owner, ticketId)],
                _ => []
            };
        }

        private static List<FlowCreationPayload> BuildTicketFlowPayloads(WfTicket ticket, FwoOwner? owner, long? ticketId)
        {
            return [.. ticket.Tasks.Where(IsFlowRelevantTask).Select(task => BuildRequestTaskFlowPayload(task, owner, ticketId ?? ticket.Id))];
        }

        private static FlowCreationPayload BuildRequestTaskFlowPayload(WfReqTask task, FwoOwner? owner, long? ticketId)
        {
            return new FlowCreationPayload
            {
                TicketId = ticketId ?? task.TicketId,
                OwnerId = owner?.Id ?? task.Owners.FirstOrDefault()?.Owner.Id,
                TaskType = task.TaskType,
                TaskAction = task.RequestAction,
                RuleActionId = task.RuleAction,
                ManagementId = task.ManagementId,
                BundleId = task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId),
                GroupName = task.GetAddInfoValue(AdditionalInfoKeys.GrpName),
                Sources = BuildFlowObjects(task.Elements, ElemFieldType.source),
                Destinations = BuildFlowObjects(task.Elements, ElemFieldType.destination),
                Services = BuildFlowServices(task.Elements),
                OriginRequestTaskIds = task.Id > 0 ? [task.Id] : []
            };
        }

        private static List<FlowObjectSnapshot> BuildFlowObjects(IEnumerable<WfReqElement> elements, ElemFieldType field)
        {
            return
            [
                .. elements
                    .Where(element => element.Field == field.ToString())
                    .Select(element => new FlowObjectSnapshot
                    {
                        WorkflowElementId = element.Id,
                        Field = field,
                        OriginalNetworkObjectId = element.NetworkId,
                        FlowNetworkObjectId = element.FlowNetworkObjectId,
                        FlowNetworkGroupId = element.FlowNetworkGroupId,
                        Ip = element.IpString,
                        IpEnd = element.IpEnd,
                        Name = element.Name,
                        GroupName = element.GroupName,
                        RequestAction = element.RequestAction
                    })
            ];
        }

        private static List<FlowServiceSnapshot> BuildFlowServices(IEnumerable<WfReqElement> elements)
        {
            return
            [
                .. elements
                    .Where(element => element.Field == ElemFieldType.service.ToString())
                    .Select(element => new FlowServiceSnapshot
                    {
                        WorkflowElementId = element.Id,
                        OriginalServiceId = element.ServiceId,
                        FlowServiceObjectId = element.FlowServiceObjectId,
                        FlowServiceGroupId = element.FlowServiceGroupId,
                        ProtoId = element.ProtoId,
                        Port = element.Port,
                        PortEnd = element.PortEnd,
                        Name = element.Name,
                        GroupName = element.GroupName,
                        RequestAction = element.RequestAction
                    })
            ];
        }

        private async Task<bool?> PersistFlowCreationPayloads(List<FlowCreationPayload> payloads)
        {
            int persistedPayloads = 0;

            foreach (IGrouping<int, FlowCreationPayload> managementPayloads in payloads.GroupBy(GetManagementGroupId))
            {
                List<FlowCreationPayload> groupedPayloads = [.. managementPayloads];
                FlowSyncFlowData context = await LoadFlowSyncData(managementPayloads.Key);
                FlowGroupMaps groupMaps = BuildGroupMaps(context);

                foreach (FlowCreationPayload payload in groupedPayloads.Where(IsGroupTask))
                {
                    if (await PersistGroupPayload(payload, context, groupMaps))
                    {
                        persistedPayloads++;
                    }
                }

                foreach (FlowCreationPayload payload in groupedPayloads.Where(payload => !IsGroupTask(payload)))
                {
                    if (await PersistAccessPayload(payload, context, groupMaps))
                    {
                        persistedPayloads++;
                    }
                }
            }

            Log.WriteInfo(LogMessageTitle, $"Persisted {persistedPayloads} of {payloads.Count} prepared Flow DB payloads.");
            return persistedPayloads == payloads.Count;
        }

        private static int GetManagementGroupId(FlowCreationPayload payload)
        {
            return payload.ManagementId ?? 0;
        }

        private async Task<FlowSyncFlowData> LoadFlowSyncData(int mgmId)
        {
            List<FlowNwObject> nwObjects = await apiConnection.SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, new { mgmId }) ?? [];
            List<FlowNwGroup> nwGroups = await apiConnection.SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, new { mgmId }) ?? [];
            List<FlowSvcObject> svcObjects = await apiConnection.SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, new { mgmId }) ?? [];
            List<FlowSvcGroup> svcGroups = await apiConnection.SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowSyncSvcGroups, new { mgmId }) ?? [];
            List<FlowAccess> accesses = await apiConnection.SendQueryAsync<List<FlowAccess>>(FlowQueries.getFlowSyncAccesses, new { mgmId }) ?? [];

            return new FlowSyncFlowData(nwObjects, nwGroups, svcObjects, svcGroups, [], accesses);
        }

        private async Task<bool> PersistGroupPayload(FlowCreationPayload payload, FlowSyncFlowData context, FlowGroupMaps groupMaps)
        {
            if (payload.TaskType == WfTaskType.group_delete.ToString())
            {
                Log.WriteInfo(LogMessageTitle, $"Skipping Flow DB group delete payload for requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)} because group removal is not yet mapped to Flow DB state updates.");
                return false;
            }

            if (payload.Services.Count > 0)
            {
                return await PersistServiceGroupPayload(payload, context, groupMaps);
            }
            return await PersistNetworkGroupPayload(payload, context, groupMaps);
        }

        private async Task<bool> PersistNetworkGroupPayload(FlowCreationPayload payload, FlowSyncFlowData context, FlowGroupMaps groupMaps)
        {
            string groupName = GetPayloadGroupName(payload);
            List<FlowObjectSnapshot> memberSnapshots = [.. payload.Sources.Concat(payload.Destinations).Where(IsActiveGroupMember)];
            List<FlowNetworkReference> members = await ResolveNetworkReferences(memberSnapshots, context, groupMaps, allowGroupNameReference: false);
            if (string.IsNullOrWhiteSpace(groupName) || members.Count == 0 || members.Count != memberSnapshots.Count
                || members.Any(member => !member.ObjectId.HasValue))
            {
                Log.WriteWarning(LogMessageTitle, $"Skipping network group Flow DB payload for requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)} because group name or member flow data is incomplete.");
                return false;
            }

            string hash = FlowHashGenerator.GenerateGroupHash(members.SelectMany(member => member.Hashes).Distinct());
            FlowNetworkReference groupReference;
            if (context.NwGroups.TryGetValue(hash, out FlowNwGroup? existingGroup))
            {
                groupReference = FlowNetworkReference.FromGroup(existingGroup!, members.SelectMany(member => member.Hashes).Distinct());
            }
            else
            {
                FlowNwGroupInsert insert = new()
                {
                    Name = groupName,
                    NwGrpHash = hash,
                    State = FlowState.Requested,
                    RemovedDate = null,
                    ShowInRequestModule = true,
                    NwGroupMembers = new FlowNwGroupInsertMembersContainer
                    {
                        Data = [.. members.Select(member => member.ObjectId!.Value).Distinct().Select(id => new FlowNwGroupMemberInsert { NwObjId = id })]
                    }
                };
                FlowNwGroup inserted = (await apiConnection.SendQueryAsync<FlowNwGroupInsertResult>(FlowQueries.insertFlowNwGroups, new { objects = new[] { insert } })).Returning.First();
                inserted.Name = groupName;
                inserted.Hash = hash;
                context.Add(inserted);
                groupReference = FlowNetworkReference.FromGroup(inserted, members.SelectMany(member => member.Hashes).Distinct());
            }

            groupMaps.NetworkGroups[groupName] = groupReference;
            await UpdateNetworkElementFlowIds(memberSnapshots, members, groupReference.GroupId);
            Log.WriteInfo(LogMessageTitle, $"Persisted Flow DB network group {groupReference.GroupId} for requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)}.");
            return true;
        }

        private async Task<bool> PersistServiceGroupPayload(FlowCreationPayload payload, FlowSyncFlowData context, FlowGroupMaps groupMaps)
        {
            string groupName = GetPayloadGroupName(payload);
            List<FlowServiceSnapshot> memberSnapshots = [.. payload.Services.Where(IsActiveGroupMember)];
            List<FlowServiceReference> members = await ResolveServiceReferences(memberSnapshots, context, groupMaps, allowGroupNameReference: false);
            if (string.IsNullOrWhiteSpace(groupName) || members.Count == 0 || members.Count != memberSnapshots.Count
                || members.Any(member => !member.ObjectId.HasValue))
            {
                Log.WriteWarning(LogMessageTitle, $"Skipping service group Flow DB payload for requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)} because group name or member flow data is incomplete.");
                return false;
            }

            string hash = FlowHashGenerator.GenerateGroupHash(members.SelectMany(member => member.Hashes).Distinct());
            FlowServiceReference groupReference;
            if (context.SvcGroups.TryGetValue(hash, out FlowSvcGroup? existingGroup))
            {
                groupReference = FlowServiceReference.FromGroup(existingGroup!, members.SelectMany(member => member.Hashes).Distinct());
            }
            else
            {
                FlowSvcGroupInsert insert = new()
                {
                    Name = groupName,
                    SvcGrpHash = hash,
                    State = FlowState.Requested,
                    RemovedDate = null,
                    ShowInRequestModule = true,
                    SvcGroupMembers = new FlowSvcGroupInsertMembersContainer
                    {
                        Data = [.. members.Select(member => member.ObjectId!.Value).Distinct().Select(id => new FlowSvcGroupMemberInsert { SvcObjId = id })]
                    }
                };
                FlowSvcGroup inserted = (await apiConnection.SendQueryAsync<FlowSvcGroupInsertResult>(FlowQueries.insertFlowSvcGroups, new { objects = new[] { insert } })).Returning.First();
                inserted.Name = groupName;
                inserted.Hash = hash;
                context.Add(inserted);
                groupReference = FlowServiceReference.FromGroup(inserted, members.SelectMany(member => member.Hashes).Distinct());
            }

            groupMaps.ServiceGroups[groupName] = groupReference;
            await UpdateServiceElementFlowIds(memberSnapshots, members, groupReference.GroupId);
            Log.WriteInfo(LogMessageTitle, $"Persisted Flow DB service group {groupReference.GroupId} for requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)}.");
            return true;
        }

        private async Task<bool> PersistAccessPayload(FlowCreationPayload payload, FlowSyncFlowData context, FlowGroupMaps groupMaps)
        {
            List<FlowNetworkReference> sources = await ResolveNetworkReferences(payload.Sources, context, groupMaps, allowGroupNameReference: true);
            List<FlowNetworkReference> destinations = await ResolveNetworkReferences(payload.Destinations, context, groupMaps, allowGroupNameReference: true);
            List<FlowServiceReference> services = await ResolveServiceReferences(payload.Services, context, groupMaps, allowGroupNameReference: true);

            if (sources.Count != payload.Sources.Count || destinations.Count != payload.Destinations.Count || services.Count != payload.Services.Count
                || sources.Count == 0 || destinations.Count == 0 || services.Count == 0)
            {
                Log.WriteWarning(LogMessageTitle, $"Skipping requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)} because source, destination, or service flow data is incomplete.");
                return false;
            }

            long accessId = await ResolveAccessId(payload, sources, destinations, services, context);
            foreach (long requestTaskId in payload.OriginRequestTaskIds.Distinct())
            {
                await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTaskFlowId, new { id = requestTaskId, flowAccessId = accessId });
            }

            Log.WriteInfo(LogMessageTitle, $"Persisted Flow DB access {accessId} for requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)}.");
            return true;
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

            string? ipEnd = string.IsNullOrWhiteSpace(snapshot.IpEnd) ? snapshot.Ip : snapshot.IpEnd;
            string hash = string.IsNullOrWhiteSpace(snapshot.Ip)
                ? FlowHashGenerator.GenerateRandomHash()
                : FlowHashGenerator.GenerateNwObjectHash(snapshot.Ip, ipEnd);
            if (context.NwObjects.TryGetValue(hash, out FlowNwObject? existingObject))
            {
                return FlowNetworkReference.FromObject(existingObject!);
            }

            FlowNwObjectInsert insert = new()
            {
                Name = BuildNetworkObjectName(snapshot),
                IpStart = snapshot.Ip,
                IpEnd = ipEnd,
                NwObjHash = hash,
                State = FlowState.Requested,
                RemovedDate = null,
                ShowInRequestModule = true
            };
            FlowNwObject inserted = (await apiConnection.SendQueryAsync<FlowNwObjectInsertResult>(FlowQueries.insertFlowNwObjects, new { objects = new[] { insert } })).Returning.First();
            context.Add(inserted);
            return FlowNetworkReference.FromObject(inserted);
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

            List<string> memberHashes = [];
            foreach (FlowNwGroupMember member in group.NwGroupMembers)
            {
                if (context.NwObjectsById.TryGetValue(member.NwObjectId, out FlowNwObject? memberObject))
                {
                    memberHashes.Add(memberObject!.Hash);
                }
            }

            return memberHashes.Count == 0 ? null : FlowNetworkReference.FromGroup(group!, memberHashes);
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
            string hash = snapshot.Port.HasValue && portEnd.HasValue
                ? FlowHashGenerator.GenerateSvcObjectHash(protoId, snapshot.Port.Value, portEnd.Value)
                : FlowHashGenerator.GenerateRandomHash();
            if (context.SvcObjects.TryGetValue(hash, out FlowSvcObject? existingObject))
            {
                return FlowServiceReference.FromObject(existingObject!);
            }

            FlowSvcObjectInsert insert = new()
            {
                Name = BuildServiceObjectName(snapshot),
                PortStart = snapshot.Port,
                PortEnd = portEnd,
                IpProtoId = protoId,
                SvcObjHash = hash,
                State = FlowState.Requested,
                RemovedDate = null,
                ShowInRequestModule = true
            };
            FlowSvcObject inserted = (await apiConnection.SendQueryAsync<FlowSvcObjectInsertResult>(FlowQueries.insertFlowSvcObjects, new { objects = new[] { insert } })).Returning.First();
            context.Add(inserted);
            return FlowServiceReference.FromObject(inserted);
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

            List<string> memberHashes = [];
            foreach (FlowSvcGroupMember member in group.SvcGroupMembers)
            {
                if (context.SvcObjectsById.TryGetValue(member.SvcObjectId, out FlowSvcObject? memberObject))
                {
                    memberHashes.Add(memberObject!.Hash);
                }
            }

            return memberHashes.Count == 0 ? null : FlowServiceReference.FromGroup(group!, memberHashes);
        }

        private async Task<long> ResolveAccessId(FlowCreationPayload payload, List<FlowNetworkReference> sources,
            List<FlowNetworkReference> destinations, List<FlowServiceReference> services, FlowSyncFlowData context)
        {
            string hash = FlowHashGenerator.GenerateAccessHash(
                sources.SelectMany(reference => reference.Hashes).Distinct(),
                destinations.SelectMany(reference => reference.Hashes).Distinct(),
                services.SelectMany(reference => reference.Hashes).Distinct());

            if (context.Accesses.TryGetValue(hash, out FlowAccess? existingAccess))
            {
                return existingAccess!.Id;
            }

            FlowAccessInsert insert = new()
            {
                AccessHash = hash,
                RequesterId = null,
                OwnerId = payload.OwnerId,
                State = FlowState.Requested,
                RemovedDate = null,
                AccessSources = FlowAccessInsertHelper.BuildMembersContainer(sources.Where(reference => reference.ObjectId.HasValue).Select(reference => reference.ObjectId!.Value).Distinct().Select(id => new NwRef { NwObjId = id })),
                AccessSourceGroups = FlowAccessInsertHelper.BuildMembersContainer(sources.Where(reference => reference.GroupId.HasValue).Select(reference => reference.GroupId!.Value).Distinct().Select(id => new NwGroupRef { NwGroupId = id })),
                AccessDestinations = FlowAccessInsertHelper.BuildMembersContainer(destinations.Where(reference => reference.ObjectId.HasValue).Select(reference => reference.ObjectId!.Value).Distinct().Select(id => new NwRef { NwObjId = id })),
                AccessDestinationGroups = FlowAccessInsertHelper.BuildMembersContainer(destinations.Where(reference => reference.GroupId.HasValue).Select(reference => reference.GroupId!.Value).Distinct().Select(id => new NwGroupRef { NwGroupId = id })),
                AccessServices = FlowAccessInsertHelper.BuildMembersContainer(services.Where(reference => reference.ObjectId.HasValue).Select(reference => reference.ObjectId!.Value).Distinct().Select(id => new SvcRef { SvcObjId = id })),
                AccessServiceGroups = FlowAccessInsertHelper.BuildMembersContainer(services.Where(reference => reference.GroupId.HasValue).Select(reference => reference.GroupId!.Value).Distinct().Select(id => new SvcGroupRef { SvcGroupId = id })),
                AccessTimeObjects = FlowAccessInsertHelper.BuildMembersContainer(Array.Empty<TimeRef>())
            };

            FlowAccess inserted = (await apiConnection.SendQueryAsync<FlowAccessInsertResult>(FlowQueries.insertFlowAccesses, new { objects = new[] { insert } })).Returning.First();
            context.Add(inserted);
            return inserted.Id;
        }

        private async Task UpdateNetworkElementFlowIds(List<FlowObjectSnapshot> snapshots, List<FlowNetworkReference> references, long? parentGroupId = null)
        {
            foreach ((FlowObjectSnapshot snapshot, FlowNetworkReference reference) in snapshots.Zip(references))
            {
                if (snapshot.WorkflowElementId <= 0)
                {
                    continue;
                }

                await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestElementFlowIds, new
                {
                    id = snapshot.WorkflowElementId,
                    flowNwObjId = reference.ObjectId,
                    flowNwGrpId = parentGroupId ?? reference.GroupId,
                    flowSvcObjId = (long?)null,
                    flowSvcGrpId = (long?)null
                });
            }
        }

        private async Task UpdateServiceElementFlowIds(List<FlowServiceSnapshot> snapshots, List<FlowServiceReference> references, long? parentGroupId = null)
        {
            foreach ((FlowServiceSnapshot snapshot, FlowServiceReference reference) in snapshots.Zip(references))
            {
                if (snapshot.WorkflowElementId <= 0)
                {
                    continue;
                }

                await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestElementFlowIds, new
                {
                    id = snapshot.WorkflowElementId,
                    flowNwObjId = (long?)null,
                    flowNwGrpId = (long?)null,
                    flowSvcObjId = reference.ObjectId,
                    flowSvcGrpId = parentGroupId ?? reference.GroupId
                });
            }
        }

        private static bool IsGroupTask(FlowCreationPayload payload)
        {
            return payload.TaskType == WfTaskType.group_create.ToString()
                || payload.TaskType == WfTaskType.group_modify.ToString()
                || payload.TaskType == WfTaskType.group_delete.ToString();
        }

        private static bool IsFlowRelevantTask(WfReqTask task)
        {
            return task.TaskType == WfTaskType.access.ToString()
                || task.TaskType == WfTaskType.group_create.ToString()
                || task.TaskType == WfTaskType.group_modify.ToString()
                || task.TaskType == WfTaskType.group_delete.ToString();
        }

        private static bool IsActiveGroupMember(FlowObjectSnapshot snapshot)
        {
            return snapshot.RequestAction != RequestAction.delete.ToString();
        }

        private static bool IsActiveGroupMember(FlowServiceSnapshot snapshot)
        {
            return snapshot.RequestAction != RequestAction.delete.ToString();
        }

        private static bool IsNetworkGroupReference(FlowObjectSnapshot snapshot)
        {
            return !string.IsNullOrWhiteSpace(snapshot.GroupName) && string.IsNullOrWhiteSpace(snapshot.Ip);
        }

        private static bool IsServiceGroupReference(FlowServiceSnapshot snapshot)
        {
            return !string.IsNullOrWhiteSpace(snapshot.GroupName) && !snapshot.ProtoId.HasValue;
        }

        private static string GetPayloadGroupName(FlowCreationPayload payload)
        {
            return payload.GroupName;
        }

        private static FlowGroupMaps BuildGroupMaps(FlowSyncFlowData context)
        {
            FlowGroupMaps maps = new();
            foreach (FlowNwGroup group in context.NwGroups.Values.Where(group => !string.IsNullOrWhiteSpace(group.Name)))
            {
                FlowNetworkReference? reference = TryBuildNetworkGroupReference(group.Id, context);
                if (reference != null)
                {
                    maps.NetworkGroups[group.Name] = reference;
                }
            }
            foreach (FlowSvcGroup group in context.SvcGroups.Values.Where(group => !string.IsNullOrWhiteSpace(group.Name)))
            {
                FlowServiceReference? reference = TryBuildServiceGroupReference(group.Id, context);
                if (reference != null)
                {
                    maps.ServiceGroups[group.Name] = reference;
                }
            }
            return maps;
        }

        private static string BuildNetworkObjectName(FlowObjectSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Name))
            {
                return snapshot.Name!;
            }
            return string.IsNullOrWhiteSpace(snapshot.IpEnd) || snapshot.IpEnd == snapshot.Ip ? snapshot.Ip ?? "" : $"{snapshot.Ip}-{snapshot.IpEnd}";
        }

        private static string BuildServiceObjectName(FlowServiceSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Name))
            {
                return snapshot.Name!;
            }
            string portLabel = snapshot.PortEnd.HasValue && snapshot.PortEnd != snapshot.Port ? $"{snapshot.Port}-{snapshot.PortEnd}" : $"{snapshot.Port}";
            return $"{snapshot.ProtoId}/{portLabel}";
        }

        private sealed class FlowNetworkReference
        {
            public long? ObjectId { get; private set; }
            public long? GroupId { get; private set; }
            public List<string> Hashes { get; private set; } = [];

            public static FlowNetworkReference FromObject(FlowNwObject flowObject)
            {
                return new FlowNetworkReference { ObjectId = flowObject.Id, Hashes = [flowObject.Hash] };
            }

            public static FlowNetworkReference FromGroup(FlowNwGroup group, IEnumerable<string> memberHashes)
            {
                return new FlowNetworkReference { GroupId = group.Id, Hashes = [.. memberHashes] };
            }
        }

        private sealed class FlowServiceReference
        {
            public long? ObjectId { get; private set; }
            public long? GroupId { get; private set; }
            public List<string> Hashes { get; private set; } = [];

            public static FlowServiceReference FromObject(FlowSvcObject flowObject)
            {
                return new FlowServiceReference { ObjectId = flowObject.Id, Hashes = [flowObject.Hash] };
            }

            public static FlowServiceReference FromGroup(FlowSvcGroup group, IEnumerable<string> memberHashes)
            {
                return new FlowServiceReference { GroupId = group.Id, Hashes = [.. memberHashes] };
            }
        }

        private sealed class FlowGroupMaps
        {
            public Dictionary<string, FlowNetworkReference> NetworkGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, FlowServiceReference> ServiceGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
