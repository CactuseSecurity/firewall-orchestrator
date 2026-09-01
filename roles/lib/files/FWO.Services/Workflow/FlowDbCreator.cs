using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Logging;
using System.Globalization;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Creates Flow DB entries from workflow request task data.
    /// </summary>
    public partial class FlowDbCreator
    {
        private const string LogMessageTitle = "Create Flow";
        private readonly ApiConnection apiConnection;
        private readonly string timeObjectPrecision;

        public FlowDbCreator(ApiConnection apiConnection, string timeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds)
        {
            this.apiConnection = apiConnection;
            this.timeObjectPrecision = NormalizeTimeObjectPrecision(timeObjectPrecision);
        }

        public async Task<bool?> CreateFlowInFlowDb(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope,
            FwoOwner? owner, long? ticketId)
        {
            List<FlowCreationPayload> payloads = BuildFlowCreationPayloads(statefulObject, scope, owner, ticketId, timeObjectPrecision);
            if (payloads.Count == 0)
            {
                Log.WriteWarning(LogMessageTitle, $"Flow creation action '{action.Name}' found no request task flow data.");
                return false;
            }

            payloads = new FlowPayloadMerger().MergeBundled(payloads);

            return await PersistFlowCreationPayloads(payloads);
        }

        public static List<FlowCreationPayload> BuildFlowCreationPayloads(WfStatefulObject statefulObject, WfObjectScopes scope,
            FwoOwner? owner, long? ticketId, string timeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds)
        {
            string normalizedPrecision = NormalizeTimeObjectPrecision(timeObjectPrecision);
            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => BuildTicketFlowPayloads(ticket, owner, ticketId, normalizedPrecision),
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => [BuildRequestTaskFlowPayload(reqTask, owner, ticketId, normalizedPrecision)],
                _ => []
            };
        }

        private static List<FlowCreationPayload> BuildTicketFlowPayloads(WfTicket ticket, FwoOwner? owner, long? ticketId, string timeObjectPrecision)
        {
            return [.. ticket.Tasks.Where(IsFlowRelevantTask).Select(task => BuildRequestTaskFlowPayload(task, owner, ticketId ?? ticket.Id, timeObjectPrecision))];
        }

        private static FlowCreationPayload BuildRequestTaskFlowPayload(WfReqTask task, FwoOwner? owner, long? ticketId, string timeObjectPrecision)
        {
            DateTime? timeStart = NormalizeTimeObjectDate(task.TargetBeginDate, timeObjectPrecision);
            DateTime? timeEnd = NormalizeTimeObjectDate(task.TargetEndDate, timeObjectPrecision);
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
                TimeStart = timeStart,
                TimeEnd = timeEnd,
                TimeName = BuildTimeObjectName(timeStart, timeEnd, timeObjectPrecision),
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
            List<FlowTimeObject> timeObjects = await apiConnection.SendQueryAsync<List<FlowTimeObject>>(FlowQueries.getFlowSyncTimeObjects, new { mgmId }) ?? [];
            List<FlowAccess> accesses = await apiConnection.SendQueryAsync<List<FlowAccess>>(FlowQueries.getFlowSyncAccesses, new { mgmId }) ?? [];
            List<IpProtocol> ipProtocols = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols) ?? [];
            List<RuleAction> ruleActions = await apiConnection.SendQueryAsync<List<RuleAction>>(StmQueries.getRuleActions) ?? [];

            return new FlowSyncFlowData(new FlowSyncFlowDataInput
            {
                NwObjects = nwObjects,
                NwGroups = nwGroups,
                SvcObjects = svcObjects,
                SvcGroups = svcGroups,
                TimeObjects = timeObjects,
                Accesses = accesses,
                IpProtocols = ipProtocols,
                RuleActions = ruleActions
            });
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

            List<long> memberObjectIds = [.. members.SelectMany(member => member.ObjectIds).Distinct()];
            List<string> memberHashes = [.. members.SelectMany(member => member.Hashes).Distinct()];
            string hash = FlowHashGenerator.GenerateGroupHash(memberHashes);
            FlowNwGroup group;
            if (context.NwGroups.TryGetValue(hash, out FlowNwGroup? existingGroup))
            {
                // the group hash is derived from the member hashes, so an existing group holds exactly
                // the members resolved above, no matter whether it was loaded or created in this run
                group = existingGroup!;
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
                        Data = [.. memberObjectIds.Select(id => new FlowNwGroupMemberInsert { NwObjId = id })]
                    }
                };
                group = (await apiConnection.SendQueryAsync<FlowNwGroupInsertResult>(FlowQueries.insertFlowNwGroups, new { objects = new[] { insert } })).Returning.First();
                group.Name = groupName;
                group.Hash = hash;
                group.NwGroupMembers = BuildNwGroupMembers(group.Id, memberObjectIds, context);
                context.Add(group);
            }

            FlowNetworkReference groupReference = FlowNetworkReference.FromGroup(group, memberObjectIds, memberHashes);

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

            List<long> memberObjectIds = [.. members.SelectMany(member => member.ObjectIds).Distinct()];
            List<string> memberHashes = [.. members.SelectMany(member => member.Hashes).Distinct()];
            string hash = FlowHashGenerator.GenerateGroupHash(memberHashes);
            FlowSvcGroup group;
            if (context.SvcGroups.TryGetValue(hash, out FlowSvcGroup? existingGroup))
            {
                // the group hash is derived from the member hashes, so an existing group holds exactly
                // the members resolved above, no matter whether it was loaded or created in this run
                group = existingGroup!;
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
                        Data = [.. memberObjectIds.Select(id => new FlowSvcGroupMemberInsert { SvcObjId = id })]
                    }
                };
                group = (await apiConnection.SendQueryAsync<FlowSvcGroupInsertResult>(FlowQueries.insertFlowSvcGroups, new { objects = new[] { insert } })).Returning.First();
                group.Name = groupName;
                group.Hash = hash;
                group.SvcGroupMembers = BuildSvcGroupMembers(group.Id, memberObjectIds, context);
                context.Add(group);
            }

            FlowServiceReference groupReference = FlowServiceReference.FromGroup(group, memberObjectIds, memberHashes);

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
            await UpdateNetworkElementFlowIds(payload.Sources, sources);
            await UpdateNetworkElementFlowIds(payload.Destinations, destinations);
            await UpdateServiceElementFlowIds(payload.Services, services);

            foreach (long requestTaskId in payload.OriginRequestTaskIds.Distinct())
            {
                await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.updateRequestTaskFlowId, new { id = requestTaskId, flowAccessId = accessId });
            }

            Log.WriteInfo(LogMessageTitle, $"Persisted Flow DB access {accessId} for requestTaskIds={string.Join(",", payload.OriginRequestTaskIds)}.");
            return true;
        }

        private async Task<long> ResolveAccessId(FlowCreationPayload payload, List<FlowNetworkReference> sources,
            List<FlowNetworkReference> destinations, List<FlowServiceReference> services, FlowSyncFlowData context)
        {
            FlowTimeObject? timeObject = await ResolveOrCreateTimeObject(payload, context);
            List<string> timeObjectHashes = timeObject == null ? [] : [timeObject.Hash];
            bool allowsTraffic = AllowsTraffic(payload, context);
            string hash = FlowHashGenerator.GenerateAccessHash(
                sources.SelectMany(reference => reference.Hashes).Distinct(),
                destinations.SelectMany(reference => reference.Hashes).Distinct(),
                services.SelectMany(reference => reference.Hashes).Distinct(),
                timeObjectHashes: timeObjectHashes,
                allowsTraffic: allowsTraffic
                );

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
                AllowsTraffic = allowsTraffic,
                AccessSources = FlowAccessInsertHelper.BuildMembersContainer(sources.SelectMany(reference => reference.ObjectIds).Distinct().Select(id => new NwRef { NwObjId = id })),
                AccessSourceGroups = FlowAccessInsertHelper.BuildMembersContainer(sources.Where(reference => reference.GroupId.HasValue).Select(reference => reference.GroupId!.Value).Distinct().Select(id => new NwGroupRef { NwGroupId = id })),
                AccessDestinations = FlowAccessInsertHelper.BuildMembersContainer(destinations.SelectMany(reference => reference.ObjectIds).Distinct().Select(id => new NwRef { NwObjId = id })),
                AccessDestinationGroups = FlowAccessInsertHelper.BuildMembersContainer(destinations.Where(reference => reference.GroupId.HasValue).Select(reference => reference.GroupId!.Value).Distinct().Select(id => new NwGroupRef { NwGroupId = id })),
                AccessServices = FlowAccessInsertHelper.BuildMembersContainer(services.SelectMany(reference => reference.ObjectIds).Distinct().Select(id => new SvcRef { SvcObjId = id })),
                AccessServiceGroups = FlowAccessInsertHelper.BuildMembersContainer(services.Where(reference => reference.GroupId.HasValue).Select(reference => reference.GroupId!.Value).Distinct().Select(id => new SvcGroupRef { SvcGroupId = id })),
                AccessTimeObjects = FlowAccessInsertHelper.BuildMembersContainer(BuildTimeRefs(timeObject))
            };

            FlowAccess inserted = (await apiConnection.SendQueryAsync<FlowAccessInsertResult>(FlowQueries.insertFlowAccesses, new { objects = new[] { insert } })).Returning.First();
            context.Add(inserted);
            return inserted.Id;
        }

        /// <summary>
        /// Returns whether the workflow rule action represents allowed traffic.
        /// </summary>
        private static bool AllowsTraffic(FlowCreationPayload payload, FlowSyncFlowData context)
        {
            return !payload.RuleActionId.HasValue
                || !context.RuleActionsById.TryGetValue(payload.RuleActionId.Value, out RuleAction? ruleAction)
                || ruleAction.Allowed;
        }

        /// <summary>
        /// Resolves or creates the Flow time object for a workflow payload. Only the persisted time bounds
        /// are converted to UTC: the hash is unaffected by the conversion, because GenerateTimeObjectHash
        /// normalizes to UTC itself, so it stays the same as the one generated for local time bounds.
        /// </summary>
        private async Task<FlowTimeObject?> ResolveOrCreateTimeObject(FlowCreationPayload payload, FlowSyncFlowData context)
        {
            if (!payload.TimeStart.HasValue && !payload.TimeEnd.HasValue)
            {
                return null;
            }

            DateTime? utcStartTime = payload.TimeStart?.ToUniversalTime();
            DateTime? utcEndTime = payload.TimeEnd?.ToUniversalTime();
            string hash = FlowHashGenerator.GenerateTimeObjectHash(utcStartTime, utcEndTime);
            if (context.TimeObjects.TryGetValue(hash, out FlowTimeObject? existingTimeObject))
            {
                return existingTimeObject;
            }

            FlowTimeObjectInsert insert = new()
            {
                Name = payload.TimeName,
                StartTime = utcStartTime,
                EndTime = utcEndTime,
                TimeObjHash = hash,
                State = FlowState.Requested,
                RemovedDate = null,
                ShowInRequestModule = true
            };

            FlowTimeObject inserted = (await apiConnection.SendQueryAsync<FlowTimeObjectInsertResult>(FlowQueries.insertFlowTimeObjects, new { objects = new[] { insert } })).Returning.First();
            inserted.Name = payload.TimeName;
            inserted.StartTime = utcStartTime;
            inserted.EndTime = utcEndTime;
            inserted.Hash = hash;
            inserted.State = FlowState.Requested;
            inserted.ShowInRequestModule = true;
            context.Add(inserted);
            return inserted;
        }

        private static IEnumerable<TimeRef> BuildTimeRefs(FlowTimeObject? timeObject)
        {
            return timeObject == null ? Array.Empty<TimeRef>() : new[] { new TimeRef { TimeObjId = timeObject.Id } };
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

        /// <summary>
        /// Builds the members of a newly inserted network group. The insert mutation returns id and hash only,
        /// so without this the group would sit in the context without members and every later resolution of it
        /// within the same run would come up empty.
        /// </summary>
        private static List<FlowNwGroupMember> BuildNwGroupMembers(long groupId, List<long> memberObjectIds, FlowSyncFlowData context)
        {
            return [.. memberObjectIds.Select(memberId => new FlowNwGroupMember
            {
                NwGroupId = groupId,
                NwObjectId = memberId,
                NwObject = context.NwObjectsById.TryGetValue(memberId, out FlowNwObject? memberObject) ? memberObject : new FlowNwObject()
            })];
        }

        /// <summary>
        /// Builds the members of a newly inserted service group, see <see cref="BuildNwGroupMembers"/>.
        /// </summary>
        private static List<FlowSvcGroupMember> BuildSvcGroupMembers(long groupId, List<long> memberObjectIds, FlowSyncFlowData context)
        {
            return [.. memberObjectIds.Select(memberId => new FlowSvcGroupMember
            {
                SvcGroupId = groupId,
                SvcObjectId = memberId,
                SvcObject = context.SvcObjectsById.TryGetValue(memberId, out FlowSvcObject? memberObject) ? memberObject : new FlowSvcObject()
            })];
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

        private static string BuildServiceObjectName(FlowServiceSnapshot snapshot, FlowSyncFlowData context)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Name))
            {
                return snapshot.Name!;
            }
            string portLabel = snapshot.PortEnd.HasValue && snapshot.PortEnd != snapshot.Port ? $"{snapshot.Port}-{snapshot.PortEnd}" : $"{snapshot.Port}";
            string protocolLabel = snapshot.ProtoId.HasValue && context.ProtocolNamesById.TryGetValue(snapshot.ProtoId.Value, out string? protocolName)
                ? protocolName
                : $"{snapshot.ProtoId}";
            return $"{portLabel}/{protocolLabel}";
        }

        /// <summary>
        /// Builds the display name of a Flow time object from the time bounds as they were requested, which is
        /// deliberately the local time of the request while start_time and end_time are persisted as UTC. The
        /// name shows requesters the period they asked for, so with a time zone offset it can name a different
        /// day than the stored timestamps. Consumers that need the exact period have to use the timestamps.
        /// </summary>
        private static string BuildTimeObjectName(DateTime? timeStart, DateTime? timeEnd, string timeObjectPrecision)
        {
            if (timeStart.HasValue && timeEnd.HasValue)
            {
                return $"{FormatTimeObjectDate(timeStart.Value, timeObjectPrecision)} - {FormatTimeObjectDate(timeEnd.Value, timeObjectPrecision)}";
            }
            if (timeStart.HasValue)
            {
                return $">= {FormatTimeObjectDate(timeStart.Value, timeObjectPrecision)}";
            }
            if (timeEnd.HasValue)
            {
                return $"<= {FormatTimeObjectDate(timeEnd.Value, timeObjectPrecision)}";
            }
            return "";
        }

        private static string FormatTimeObjectDate(DateTime date, string timeObjectPrecision)
        {
            return timeObjectPrecision switch
            {
                FlowIntegrationTimePrecisionOptions.Date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FlowIntegrationTimePrecisionOptions.Hours => date.ToString("yyyy-MM-dd HH 'h'", CultureInfo.InvariantCulture),
                FlowIntegrationTimePrecisionOptions.Minutes => date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                _ => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            };
        }

        private static DateTime? NormalizeTimeObjectDate(DateTime? date, string timeObjectPrecision)
        {
            if (!date.HasValue)
            {
                return null;
            }

            DateTime value = date.Value;
            return timeObjectPrecision switch
            {
                FlowIntegrationTimePrecisionOptions.Date => new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, value.Kind),
                FlowIntegrationTimePrecisionOptions.Hours => new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind),
                FlowIntegrationTimePrecisionOptions.Minutes => new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind),
                _ => new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind)
            };
        }

        private static string NormalizeTimeObjectPrecision(string timeObjectPrecision)
        {
            return FlowIntegrationTimePrecisionOptions.All.Contains(timeObjectPrecision)
                ? timeObjectPrecision
                : FlowIntegrationTimePrecisionOptions.Seconds;
        }

        private sealed class FlowNetworkReference
        {
            public long? ObjectId { get; private set; }
            public long? GroupId { get; private set; }
            public List<long> ObjectIds { get; private set; } = [];
            public List<string> Hashes { get; private set; } = [];

            /// <summary>
            /// Builds a reference to a direct network object.
            /// </summary>
            public static FlowNetworkReference FromObject(FlowNwObject flowObject)
            {
                return new FlowNetworkReference
                {
                    ObjectId = flowObject.Id,
                    ObjectIds = [flowObject.Id],
                    Hashes = [flowObject.Hash]
                };
            }

            /// <summary>
            /// Builds a group reference that also exposes its flattened member objects.
            /// </summary>
            public static FlowNetworkReference FromGroup(FlowNwGroup group, IEnumerable<long> memberObjectIds, IEnumerable<string> memberHashes)
            {
                return new FlowNetworkReference
                {
                    GroupId = group.Id,
                    ObjectIds = [.. memberObjectIds.Distinct()],
                    Hashes = [.. memberHashes.Distinct()]
                };
            }
        }

        private sealed class FlowServiceReference
        {
            public long? ObjectId { get; private set; }
            public long? GroupId { get; private set; }
            public List<long> ObjectIds { get; private set; } = [];
            public List<string> Hashes { get; private set; } = [];

            /// <summary>
            /// Builds a reference to a direct service object.
            /// </summary>
            public static FlowServiceReference FromObject(FlowSvcObject flowObject)
            {
                return new FlowServiceReference
                {
                    ObjectId = flowObject.Id,
                    ObjectIds = [flowObject.Id],
                    Hashes = [flowObject.Hash]
                };
            }

            /// <summary>
            /// Builds a group reference that also exposes its flattened member objects.
            /// </summary>
            public static FlowServiceReference FromGroup(FlowSvcGroup group, IEnumerable<long> memberObjectIds, IEnumerable<string> memberHashes)
            {
                return new FlowServiceReference
                {
                    GroupId = group.Id,
                    ObjectIds = [.. memberObjectIds.Distinct()],
                    Hashes = [.. memberHashes.Distinct()]
                };
            }
        }

        private sealed class FlowGroupMaps
        {
            public Dictionary<string, FlowNetworkReference> NetworkGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, FlowServiceReference> ServiceGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
