using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Logging;

// Aliases for readability
using FlowNwObjectInsert = FWO.Data.Flow.FlowNwObjectInsert;
using FlowSvcObjectInsert = FWO.Data.Flow.FlowSvcObjectInsert;
using FlowTimeObjectInsert = FWO.Data.Flow.FlowTimeObjectInsert;
using FlowNwGroupInsert = FWO.Data.Flow.FlowNwGroupInsert;
using FlowSvcGroupInsert = FWO.Data.Flow.FlowSvcGroupInsert;
using FlowAccessInsert = FWO.Data.Flow.FlowAccessInsert;

namespace FWO.Services
{
    /// <summary>
    /// Synchronizes normalized firewall objects and rules to flow database tables.
    /// Implements a three-step process: (1) calculate hashes for normalized objects,
    /// (2) insert missing flow entries, (3) update mappings with flow_active based on uniqueness.
    /// </summary>
    public class FlowSync
    {
        private const string LogMessageTitle = "Flow sync";

        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        public FlowSync(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        private async Task<FlowSyncFlowDataContainer> GetFlowSyncDataAsync(int mgmId)
        {
            var nwObjects = await apiConnection.SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, new { mgmId }) ?? [];
            var nwGroups = await apiConnection.SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, new { mgmId }) ?? [];
            var svcObjects = await apiConnection.SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, new { mgmId }) ?? [];
            var svcGroups = await apiConnection.SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowSyncSvcGroups, new { mgmId }) ?? [];
            var timeObjects = await apiConnection.SendQueryAsync<List<FlowTimeObject>>(FlowQueries.getFlowSyncTimeObjects, new { mgmId }) ?? [];
            var accesses = await apiConnection.SendQueryAsync<List<FlowAccess>>(FlowQueries.getFlowSyncAccesses, new { mgmId }) ?? [];

            return new FlowSyncFlowDataContainer
            {
                NwObjects = nwObjects,
                NwGroups = nwGroups,
                SvcObjects = svcObjects,
                SvcGroups = svcGroups,
                TimeObjects = timeObjects,
                Accesses = accesses
            };
        }

        /// <summary>
        /// Main entry point: discovers pending imports and synchronizes each management.
        /// </summary>
        public async Task<bool> Run()
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(FlowQueries.getPendingFlowSyncImports);

            if (pendingImports == null || pendingImports.Count == 0)
            {
                Log.WriteInfo(LogMessageTitle, "No pending flow sync imports found.");
                return false;
            }

            var pendingByManagement = pendingImports
                .Where(import => import.MgmId.HasValue)
                .GroupBy(import => import.MgmId!.Value)
                .OrderBy(group => group.Max(import => import.ControlId))
                .ToList();

            if (!pendingByManagement.Any())
            {
                Log.WriteWarning(LogMessageTitle, "Pending imports do not contain a management id.");
                return false;
            }

            bool syncedAny = false;

            foreach (var managementGroup in pendingByManagement)
            {
                int mgmId = managementGroup.Key;
                var importsForManagement = managementGroup.OrderBy(import => import.ControlId).ToList();

                try
                {
                    await SyncManagementAsync(mgmId, importsForManagement);
                    syncedAny = true;
                }
                catch (Exception exception)
                {
                    Log.WriteError(LogMessageTitle, $"Flow sync failed for management {mgmId}.", exception);
                }
            }

            return syncedAny;
        }

        /// <summary>
        /// Synchronizes a single management: fetches normalized objects, calculates hashes,
        /// inserts missing flows, updates mappings, and marks imports as complete.
        /// </summary>
        private async Task SyncManagementAsync(int mgmId, List<ImportControl> importsForManagement)
        {
            var managementData = (await apiConnection.SendQueryAsync<List<FlowSyncManagementData>>(FlowQueries.getFlowSyncManagementData, new { mgmId }))?.FirstOrDefault();

            if (managementData == null)
            {
                Log.WriteWarning(LogMessageTitle, $"No management data returned for mgm_id {mgmId}.");
                return;
            }

            var flowData = await GetFlowSyncDataAsync(mgmId);
            int mgmFlowNamingSourceId = globalConfig.FlowNamingSourceManagementId ?? 0;
            bool useManagementNamesForFlow = mgmFlowNamingSourceId == mgmId;

            // Step 1: Calculate hashes and track uniqueness (hash -> list of normalized object IDs)
            var nwObjHashMap = CalculateNwObjectHashes(managementData.NetworkObjects.Where(o => o.Removed == null && o.Type.Name != ObjectType.Group).ToList());
            var svcObjHashMap = CalculateSvcObjectHashes(managementData.ServiceObjects.Where(s => s.Removed == null && s.Type.Name != ServiceType.Group).ToList());
            var timeObjHashMap = CalculateTimeObjectHashes(managementData.TimeObjects.Where(t => t.Removed == null).ToList());
            var nwGrpHashMap = CalculateNwGroupHashes(managementData.NetworkObjects.Where(o => o.Removed == null && o.Type.Name == ObjectType.Group).ToList(), flowData);
            var svcGrpHashMap = CalculateSvcGroupHashes(managementData.ServiceObjects.Where(s => s.Removed == null && s.Type.Name == ServiceType.Group).ToList(), flowData);
            var ruleHashMap = CalculateRuleHashes(managementData.Rules ?? [], flowData);

            // Step 2: Insert missing flows
            await SyncFlowNwObjectsAsync(nwObjHashMap, flowData);
            await SyncFlowSvcObjectsAsync(svcObjHashMap, flowData);
            await SyncFlowTimeObjectsAsync(timeObjHashMap, flowData);

            var omittedNwGroups = await SyncFlowNwGroupsAsync(nwGrpHashMap, flowData);
            var omittedSvcGroups = await SyncFlowSvcGroupsAsync(svcGrpHashMap, flowData);
            var skippedRules = await SyncFlowAccessesAsync(ruleHashMap, flowData);

            // Reload flow data after inserts to get new IDs
            flowData = await GetFlowSyncDataAsync(mgmId);

            // Step 3: Update normalized objects with flow mappings and flow_active status
            await UpdateNwObjectMappingsAsync(managementData.NetworkObjects, flowData, nwObjHashMap, nwGrpHashMap);
            await UpdateSvcObjectMappingsAsync(managementData.ServiceObjects, flowData, svcObjHashMap, svcGrpHashMap);
            await UpdateTimeObjectMappingsAsync(managementData.TimeObjects, flowData, timeObjHashMap);
            await UpdateRuleMappingsAsync(managementData.Rules ?? [], flowData, ruleHashMap);

            await UpdateImportControlsAsync(importsForManagement);

            Log.WriteInfo(LogMessageTitle, $"Management {mgmId} synchronized. Omitted network groups: {omittedNwGroups}, omitted service groups: {omittedSvcGroups}, skipped access rules: {skippedRules}.");
        }

        /// <summary>
        /// Checks if a hash already exists in the flow data (for any object type).
        /// </summary>
        private static bool HashExistsInFlowData(FlowSyncFlowData flowData, string hash)
        {
            return flowData.NwObjects.Any(o => o.Hash == hash) ||
                   flowData.SvcObjects.Any(o => o.Hash == hash) ||
                   flowData.TimeObjects.Any(o => o.Hash == hash) ||
                   flowData.NwGroups.Any(g => g.Hash == hash) ||
                   flowData.SvcGroups.Any(g => g.Hash == hash) ||
                   flowData.Accesses.Any(a => a.AccessHash == hash);
        }

        /// <summary>
        /// Maps normalized network objects to their calculated hashes.
        /// Result: hash -> list of normalized object IDs (to detect uniqueness).
        /// </summary>
        private Dictionary<string, List<long>> CalculateNwObjectHashes(List<NetworkObject> objects)
        {
            var hashMap = new Dictionary<string, List<long>>();
            foreach (var obj in objects)
            {
                var flowObj = new FlowNwObject { IpStart = obj.IP, IpEnd = obj.IpEnd, Name = obj.Name };
                flowObj.GenerateHash();
                if (!hashMap.ContainsKey(flowObj.Hash))
                    hashMap[flowObj.Hash] = [];
                hashMap[flowObj.Hash].Add(obj.Id);
            }
            return hashMap;
        }

        /// <summary>
        /// Maps normalized service objects to their calculated hashes.
        /// Result: hash -> list of normalized object IDs (to detect uniqueness).
        /// </summary>
        private Dictionary<string, List<long>> CalculateSvcObjectHashes(List<NetworkService> services)
        {
            var hashMap = new Dictionary<string, List<long>>();
            foreach (var svc in services)
            {
                var flowSvc = new FlowSvcObject { PortStart = svc.DestinationPort, PortEnd = svc.DestinationPortEnd, ProtoId = svc.ProtoId ?? 0, Name = svc.Name };
                flowSvc.GenerateHash();
                if (!hashMap.ContainsKey(flowSvc.Hash))
                    hashMap[flowSvc.Hash] = [];
                hashMap[flowSvc.Hash].Add(svc.Id);
            }
            return hashMap;
        }

        /// <summary>
        /// Maps normalized time objects to their calculated hashes.
        /// Result: hash -> list of normalized object IDs (to detect uniqueness).
        /// </summary>
        private Dictionary<string, List<long>> CalculateTimeObjectHashes(List<TimeObject> timeObjects)
        {
            var hashMap = new Dictionary<string, List<long>>();
            foreach (var timeObj in timeObjects)
            {
                var flowTime = new FlowTimeObject { StartTime = timeObj.StartTime, EndTime = timeObj.EndTime, Name = timeObj.Name };
                flowTime.GenerateHash();
                if (!hashMap.ContainsKey(flowTime.Hash))
                    hashMap[flowTime.Hash] = [];
                hashMap[flowTime.Hash].Add(timeObj.Id);
            }
            return hashMap;
        }

        /// <summary>
        /// Maps normalized network groups to their calculated hashes.
        /// Only groups with all technical members are included.
        /// Result: hash -> list of normalized group IDs (to detect uniqueness).
        /// </summary>
        private Dictionary<string, List<long>> CalculateNwGroupHashes(List<NetworkObject> groups, FlowSyncFlowData flowData)
        {
            var hashMap = new Dictionary<string, List<long>>();
            foreach (var group in groups)
            {
                if (TryBuildNwGroupHash(group, flowData, out var hash))
                {
                    if (!hashMap.ContainsKey(hash!))
                        hashMap[hash!] = [];
                    hashMap[hash!].Add(group.Id);
                }
            }
            return hashMap;
        }

        /// <summary>
        /// Maps normalized service groups to their calculated hashes.
        /// Only groups with all technical members are included.
        /// Result: hash -> list of normalized group IDs (to detect uniqueness).
        /// </summary>
        private Dictionary<string, List<long>> CalculateSvcGroupHashes(List<NetworkService> groups, FlowSyncFlowData flowData)
        {
            var hashMap = new Dictionary<string, List<long>>();
            foreach (var group in groups)
            {
                if (TryBuildSvcGroupHash(group, flowData, out var hash))
                {
                    if (!hashMap.ContainsKey(hash!))
                        hashMap[hash!] = [];
                    hashMap[hash!].Add(group.Id);
                }
            }
            return hashMap;
        }

        /// <summary>
        /// Maps normalized rules to their calculated access hashes.
        /// Only rules with all resolvable members are included.
        /// Result: hash -> list of normalized rule IDs (to detect uniqueness).
        /// </summary>
        private Dictionary<string, List<long>> CalculateRuleHashes(List<Rule> rules, FlowSyncFlowData flowData)
        {
            var hashMap = new Dictionary<string, List<long>>();
            foreach (var rule in rules.Where(r => r.Removed == null))
            {
                if (TryBuildAccessHash(rule, flowData, out var hash))
                {
                    if (!hashMap.ContainsKey(hash!))
                        hashMap[hash!] = [];
                    hashMap[hash!].Add(rule.Id);
                }
            }
            return hashMap;
        }

        /// <summary>
        /// Syncs network objects: inserts missing flow entries for new hashes.
        /// </summary>
        private async Task SyncFlowNwObjectsAsync(Dictionary<string, List<long>> nwObjHashMap, FlowSyncFlowData flowData)
        {
            var toInsert = new List<FlowNwObjectInsert>();
            foreach (var kvp in nwObjHashMap)
            {
                if (HashExistsInFlowData(flowData, kvp.Key))
                    continue;
                toInsert.Add(new FlowNwObjectInsert { NwObjHash = kvp.Key, State = "implemented", RemovedDate = null, ShowInRequestModule = false, Name = null });
            }
            if (toInsert.Any())
                await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.insertFlowNwObjects, new { objects = toInsert });
        }

        /// <summary>
        /// Syncs service objects: inserts missing flow entries for new hashes.
        /// </summary>
        private async Task SyncFlowSvcObjectsAsync(Dictionary<string, List<long>> svcObjHashMap, FlowSyncFlowData flowData)
        {
            var toInsert = new List<FlowSvcObjectInsert>();
            foreach (var kvp in svcObjHashMap)
            {
                if (HashExistsInFlowData(flowData, kvp.Key))
                    continue;
                toInsert.Add(new FlowSvcObjectInsert { SvcObjHash = kvp.Key, State = "implemented", RemovedDate = null, ShowInRequestModule = false, Name = null });
            }
            if (toInsert.Any())
                await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.insertFlowSvcObjects, new { objects = toInsert });
        }

        /// <summary>
        /// Syncs time objects: inserts missing flow entries for new hashes.
        /// </summary>
        private async Task SyncFlowTimeObjectsAsync(Dictionary<string, List<long>> timeObjHashMap, FlowSyncFlowData flowData)
        {
            var toInsert = new List<FlowTimeObjectInsert>();
            foreach (var kvp in timeObjHashMap)
            {
                if (HashExistsInFlowData(flowData, kvp.Key))
                    continue;
                toInsert.Add(new FlowTimeObjectInsert { TimeObjHash = kvp.Key, State = "implemented", RemovedDate = null, ShowInRequestModule = false, Name = null });
            }
            if (toInsert.Any())
                await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.insertFlowTimeObjects, new { objects = toInsert });
        }

        /// <summary>
        /// Syncs network groups: inserts missing flow entries. Returns count of omitted groups.
        /// </summary>
        private async Task<int> SyncFlowNwGroupsAsync(Dictionary<string, List<long>> nwGrpHashMap, FlowSyncFlowData flowData)
        {
            var toInsert = new List<FlowNwGroupInsert>();
            int omitted = 0;
            foreach (var kvp in nwGrpHashMap)
            {
                if (HashExistsInFlowData(flowData, kvp.Key))
                    continue;
                toInsert.Add(new FlowNwGroupInsert { NwGrpHash = kvp.Key, State = "implemented", RemovedDate = null, ShowInRequestModule = false, Name = null });
            }
            if (toInsert.Any())
                await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.insertFlowNwGroups, new { objects = toInsert });
            return omitted;
        }

        /// <summary>
        /// Syncs service groups: inserts missing flow entries. Returns count of omitted groups.
        /// </summary>
        private async Task<int> SyncFlowSvcGroupsAsync(Dictionary<string, List<long>> svcGrpHashMap, FlowSyncFlowData flowData)
        {
            var toInsert = new List<FlowSvcGroupInsert>();
            int omitted = 0;
            foreach (var kvp in svcGrpHashMap)
            {
                if (HashExistsInFlowData(flowData, kvp.Key))
                    continue;
                toInsert.Add(new FlowSvcGroupInsert { SvcGrpHash = kvp.Key, State = "implemented", RemovedDate = null, ShowInRequestModule = false, Name = null });
            }
            if (toInsert.Any())
                await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.insertFlowSvcGroups, new { objects = toInsert });
            return omitted;
        }

        /// <summary>
        /// Syncs rule accesses: inserts missing flow entries. Returns count of skipped rules.
        /// </summary>
        private async Task<int> SyncFlowAccessesAsync(Dictionary<string, List<long>> ruleHashMap, FlowSyncFlowData flowData)
        {
            var toInsert = new List<FlowAccessInsert>();
            int skipped = 0;
            foreach (var kvp in ruleHashMap)
            {
                if (HashExistsInFlowData(flowData, kvp.Key))
                    continue;
                toInsert.Add(new FlowAccessInsert { AccessHash = kvp.Key, RequesterId = null, OwnerId = null, State = "implemented", RemovedDate = null });
            }
            if (toInsert.Any())
                await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.insertFlowAccesses, new { objects = toInsert });
            return skipped;
        }

        /// <summary>
        /// Updates normalized network objects with their flow mappings.
        /// flow_active is true only if the mapping is unique (exactly one nw object maps to this hash).
        /// </summary>
        private async Task UpdateNwObjectMappingsAsync(List<NetworkObject> objects, FlowSyncFlowData flowData,
            Dictionary<string, List<long>> nwObjHashMap, Dictionary<string, List<long>> nwGrpHashMap)
        {
            var updates = new List<object>();

            foreach (var obj in objects)
            {
                if (obj.Removed.HasValue)
                {
                    updates.Add(new { where = new { obj_id = new { _eq = obj.Id } }, _set = new { flow_nwobj_id = (long?)null, flow_nwgrp_id = (long?)null, flow_active = false } });
                    continue;
                }

                if (obj.Type.Name == ObjectType.Group)
                {
                    if (!TryBuildNwGroupHash(obj, flowData, out var groupHash) || string.IsNullOrWhiteSpace(groupHash))
                    {
                        updates.Add(new { where = new { obj_id = new { _eq = obj.Id } }, _set = new { flow_nwobj_id = (long?)null, flow_nwgrp_id = (long?)null, flow_active = false } });
                        continue;
                    }
                    var flowGrpId = FindFlowIdByHash(flowData, groupHash);
                    bool isUnique = nwGrpHashMap.TryGetValue(groupHash, out var grpMapping) && grpMapping.Count == 1;
                    updates.Add(new { where = new { obj_id = new { _eq = obj.Id } }, _set = new { flow_nwobj_id = (long?)null, flow_nwgrp_id = flowGrpId, flow_active = (flowGrpId.HasValue && isUnique) } });
                    continue;
                }

                var flowObjHash = GetNwObjectHash(obj);
                var flowObjId = FindFlowIdByHash(flowData, flowObjHash);
                bool isObjUnique = nwObjHashMap.TryGetValue(flowObjHash, out var objMapping) && objMapping.Count == 1;
                updates.Add(new { where = new { obj_id = new { _eq = obj.Id } }, _set = new { flow_nwobj_id = flowObjId, flow_nwgrp_id = (long?)null, flow_active = (flowObjId.HasValue && isObjUnique) } });
            }

            await SendUpdateManyAsync(FlowQueries.updateObjectFlowMappings, updates);
        }

        /// <summary>
        /// Updates normalized service objects with their flow mappings.
        /// flow_active is true only if the mapping is unique (exactly one svc object maps to this hash).
        /// </summary>
        private async Task UpdateSvcObjectMappingsAsync(List<NetworkService> services, FlowSyncFlowData flowData,
            Dictionary<string, List<long>> svcObjHashMap, Dictionary<string, List<long>> svcGrpHashMap)
        {
            var updates = new List<object>();

            foreach (var svc in services)
            {
                if (svc.Removed.HasValue)
                {
                    updates.Add(new { where = new { svc_id = new { _eq = svc.Id } }, _set = new { flow_svcobj_id = (long?)null, flow_svcgrp_id = (long?)null, flow_active = false } });
                    continue;
                }

                if (svc.Type.Name == ServiceType.Group)
                {
                    if (!TryBuildSvcGroupHash(svc, flowData, out var groupHash) || string.IsNullOrWhiteSpace(groupHash))
                    {
                        updates.Add(new { where = new { svc_id = new { _eq = svc.Id } }, _set = new { flow_svcobj_id = (long?)null, flow_svcgrp_id = (long?)null, flow_active = false } });
                        continue;
                    }
                    var flowGrpId = FindFlowIdByHash(flowData, groupHash);
                    bool isUnique = svcGrpHashMap.TryGetValue(groupHash, out var grpMapping) && grpMapping.Count == 1;
                    updates.Add(new { where = new { svc_id = new { _eq = svc.Id } }, _set = new { flow_svcobj_id = (long?)null, flow_svcgrp_id = flowGrpId, flow_active = (flowGrpId.HasValue && isUnique) } });
                    continue;
                }

                var flowObjHash = GetSvcObjectHash(svc);
                var flowObjId = FindFlowIdByHash(flowData, flowObjHash);
                bool isObjUnique = svcObjHashMap.TryGetValue(flowObjHash, out var objMapping) && objMapping.Count == 1;
                updates.Add(new { where = new { svc_id = new { _eq = svc.Id } }, _set = new { flow_svcobj_id = flowObjId, flow_svcgrp_id = (long?)null, flow_active = (flowObjId.HasValue && isObjUnique) } });
            }

            await SendUpdateManyAsync(FlowQueries.updateServiceFlowMappings, updates);
        }

        /// <summary>
        /// Updates normalized time objects with their flow mappings.
        /// flow_active is true only if the mapping is unique (exactly one time object maps to this hash).
        /// </summary>
        private async Task UpdateTimeObjectMappingsAsync(List<TimeObject> timeObjects, FlowSyncFlowData flowData,
            Dictionary<string, List<long>> timeObjHashMap)
        {
            var updates = new List<object>();

            foreach (var timeObj in timeObjects)
            {
                if (timeObj.Removed.HasValue)
                {
                    updates.Add(new { where = new { time_obj_id = new { _eq = timeObj.Id } }, _set = new { flow_timeobj_id = (long?)null, flow_active = false } });
                    continue;
                }

                var flowObjHash = GetTimeObjectHash(timeObj);
                var flowObjId = FindFlowIdByHash(flowData, flowObjHash);
                bool isUnique = timeObjHashMap.TryGetValue(flowObjHash, out var objMapping) && objMapping.Count == 1;
                updates.Add(new { where = new { time_obj_id = new { _eq = timeObj.Id } }, _set = new { flow_timeobj_id = flowObjId, flow_active = (flowObjId.HasValue && isUnique) } });
            }

            await SendUpdateManyAsync(FlowQueries.updateTimeObjectFlowMappings, updates);
        }

        /// <summary>
        /// Updates normalized rules with their flow access mappings.
        /// flow_active is true only if the mapping is unique (exactly one rule maps to this hash).
        /// </summary>
        private async Task UpdateRuleMappingsAsync(List<Rule> rules, FlowSyncFlowData flowData,
            Dictionary<string, List<long>> ruleHashMap)
        {
            var updates = new List<object>();

            foreach (var rule in rules.Where(r => r.Removed == null))
            {
                if (rule.Removed.HasValue)
                {
                    updates.Add(new { where = new { rule_id = new { _eq = rule.Id } }, _set = new { flow_access_id = (long?)null, flow_active = false } });
                    continue;
                }

                if (!TryBuildAccessHash(rule, flowData, out var accessHash) || string.IsNullOrWhiteSpace(accessHash))
                {
                    updates.Add(new { where = new { rule_id = new { _eq = rule.Id } }, _set = new { flow_access_id = (long?)null, flow_active = false } });
                    continue;
                }

                var flowAccessId = FindFlowIdByHash(flowData, accessHash);
                bool isUnique = ruleHashMap.TryGetValue(accessHash, out var ruleMapping) && ruleMapping.Count == 1;
                updates.Add(new { where = new { rule_id = new { _eq = rule.Id } }, _set = new { flow_access_id = flowAccessId, flow_active = (flowAccessId.HasValue && isUnique) } });
            }

            await SendUpdateManyAsync(FlowQueries.updateRuleFlowMappings, updates);
        }

        /// <summary>
        /// Marks import controls as complete for the current sync run.
        /// </summary>
        private async Task UpdateImportControlsAsync(List<ImportControl> importsForManagement)
        {
            foreach (var importControl in importsForManagement)
            {
                await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.updateImportControlForFlowSync, new { controlId = importControl.ControlId, flowSyncDone = true });
            }
        }

        /// <summary>
        /// Batch sends update mutations to the database.
        /// </summary>
        private async Task SendUpdateManyAsync(string query, List<object> updates)
        {
            if (!updates.Any())
                return;
            await apiConnection.SendQueryAsync<List<MutationResult>>(query, new { updates });
        }

        /// <summary>
        /// Attempts to build a hash for a network group. Returns false if members cannot be resolved.
        /// </summary>
        private bool TryBuildNwGroupHash(NetworkObject group, FlowSyncFlowData flowData, out string? hash)
        {
            hash = null;
            var members = group.ObjectGroupFlats
                .Select(gf => gf.Object)
                .Where(m => m != null)
                .Cast<NetworkObject>()
                .ToList();
            if (!members.Any())
                return false;

            var memberHashes = new List<string>();
            foreach (var member in members)
            {
                if (member.Type.Name == ObjectType.Group || string.IsNullOrWhiteSpace(member.IP))
                    return false;
                string mHash = GetNwObjectHash(member);
                if (!flowData.NwObjects.Any(o => o.Hash == mHash))
                    return false;
                memberHashes.Add(mHash);
            }
            hash = FlowHashGenerator.GenerateGroupHash(memberHashes);
            return true;
        }

        /// <summary>
        /// Attempts to build a hash for a service group. Returns false if members cannot be resolved.
        /// </summary>
        private bool TryBuildSvcGroupHash(NetworkService group, FlowSyncFlowData flowData, out string? hash)
        {
            hash = null;
            var members = group.ServiceGroupFlats
                .Select(gf => gf.Object)
                .Where(m => m != null)
                .Cast<NetworkService>()
                .ToList();
            if (!members.Any())
                return false;

            var memberHashes = new List<string>();
            foreach (var member in members)
            {
                if (member.Type.Name == ServiceType.Group)
                    return false;
                string mHash = GetSvcObjectHash(member);
                if (!flowData.SvcObjects.Any(o => o.Hash == mHash))
                    return false;
                memberHashes.Add(mHash);
            }
            hash = FlowHashGenerator.GenerateGroupHash(memberHashes);
            return true;
        }

        /// <summary>
        /// Attempts to build a hash for a rule's access. Returns false if members cannot be resolved.
        /// </summary>
        private bool TryBuildAccessHash(Rule rule, FlowSyncFlowData flowData, out string? hash)
        {
            hash = null;
            var srcHashes = new List<string>();
            var dstHashes = new List<string>();
            var svcHashes = new List<string>();

            foreach (var from in rule.Froms)
            {
                if (!TryResolveObjectHash(from.Object, flowData, out var h))
                    return false;
                srcHashes.Add(h!);
            }
            foreach (var to in rule.Tos)
            {
                if (!TryResolveObjectHash(to.Object, flowData, out var h))
                    return false;
                dstHashes.Add(h!);
            }
            foreach (var svc in rule.Services)
            {
                if (!TryResolveServiceHash(svc.Content, flowData, out var h))
                    return false;
                svcHashes.Add(h!);
            }

            if (!srcHashes.Any() || !dstHashes.Any() || !svcHashes.Any())
                return false;

            hash = FlowHashGenerator.GenerateAccessHash(srcHashes, dstHashes, svcHashes);
            return true;
        }

        /// <summary>
        /// Tries to resolve a network object to its flow hash. Returns false if unresolvable.
        /// </summary>
        private bool TryResolveObjectHash(NetworkObject? obj, FlowSyncFlowData flowData, out string? hash)
        {
            hash = null;
            if (obj == null || obj.Removed.HasValue)
                return false;

            if (obj.Type.Name == ObjectType.Group)
            {
                return TryBuildNwGroupHash(obj, flowData, out hash) && !string.IsNullOrWhiteSpace(hash);
            }

            hash = GetNwObjectHash(obj);
            return !string.IsNullOrWhiteSpace(hash);
        }

        /// <summary>
        /// Tries to resolve a service object to its flow hash. Returns false if unresolvable.
        /// </summary>
        private bool TryResolveServiceHash(NetworkService? svc, FlowSyncFlowData flowData, out string? hash)
        {
            hash = null;
            if (svc == null || svc.Removed.HasValue)
                return false;

            if (svc.Type.Name == ServiceType.Group)
            {
                return TryBuildSvcGroupHash(svc, flowData, out hash) && !string.IsNullOrWhiteSpace(hash);
            }

            hash = GetSvcObjectHash(svc);
            return !string.IsNullOrWhiteSpace(hash);
        }

        /// <summary>
        /// Finds a flow object/group/access ID by hash in the flow data.
        /// </summary>
        private static long? FindFlowIdByHash(FlowSyncFlowData flowData, string hash)
        {
            return flowData.NwObjects.FirstOrDefault(o => o.Hash == hash)?.Id ??
                   flowData.SvcObjects.FirstOrDefault(o => o.Hash == hash)?.Id ??
                   flowData.TimeObjects.FirstOrDefault(o => o.Hash == hash)?.Id ??
                   flowData.NwGroups.FirstOrDefault(g => g.Hash == hash)?.Id ??
                   flowData.SvcGroups.FirstOrDefault(g => g.Hash == hash)?.Id ??
                   flowData.Accesses.FirstOrDefault(a => a.AccessHash == hash)?.Id;
        }

        /// <summary>
        /// Calculates hash for a network object.
        /// </summary>
        private string GetNwObjectHash(NetworkObject obj)
        {
            var flowObj = new FlowNwObject { IpStart = obj.IP, IpEnd = obj.IpEnd, Name = obj.Name };
            flowObj.GenerateHash();
            return flowObj.Hash;
        }

        /// <summary>
        /// Calculates hash for a service object.
        /// </summary>
        private string GetSvcObjectHash(NetworkService svc)
        {
            var flowSvc = new FlowSvcObject { PortStart = svc.DestinationPort, PortEnd = svc.DestinationPortEnd, ProtoId = svc.ProtoId ?? 0, Name = svc.Name };
            flowSvc.GenerateHash();
            return flowSvc.Hash;
        }

        /// <summary>
        /// Calculates hash for a time object.
        /// </summary>
        private string GetTimeObjectHash(TimeObject timeObj)
        {
            var flowTime = new FlowTimeObject { StartTime = timeObj.StartTime, EndTime = timeObj.EndTime, Name = timeObj.Name };
            flowTime.GenerateHash();
            return flowTime.Hash;
        }

        private sealed class MutationResult
        {
            [Newtonsoft.Json.JsonProperty("affected_rows")]
            public int AffectedRows { get; set; }
        }

    }
}