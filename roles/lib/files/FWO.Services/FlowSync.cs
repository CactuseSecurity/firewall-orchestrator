using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Config.Api;
using FWO.Logging;

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

        /// <summary>
        /// Creates a new flow sync service with API access.
        /// </summary>
        public FlowSync(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Fetches the normalized objects and existing flow data needed for synchronization.
        /// </summary>
        private async Task<FlowSyncFlowData> GetFlowSyncDataAsync(int mgmId)
        {
            var nwObjects = await apiConnection.SendQueryAsync<List<FlowNwObject>>(FlowQueries.getFlowSyncNwObjects, new { mgmId }) ?? [];
            var nwGroups = await apiConnection.SendQueryAsync<List<FlowNwGroup>>(FlowQueries.getFlowSyncNwGroups, new { mgmId }) ?? [];
            var svcObjects = await apiConnection.SendQueryAsync<List<FlowSvcObject>>(FlowQueries.getFlowSyncSvcObjects, new { mgmId }) ?? [];
            var svcGroups = await apiConnection.SendQueryAsync<List<FlowSvcGroup>>(FlowQueries.getFlowSyncSvcGroups, new { mgmId }) ?? [];
            var timeObjects = await apiConnection.SendQueryAsync<List<FlowTimeObject>>(FlowQueries.getFlowSyncTimeObjects, new { mgmId }) ?? [];
            var accesses = await apiConnection.SendQueryAsync<List<FlowAccess>>(FlowQueries.getFlowSyncAccesses, new { mgmId }) ?? [];

            return new FlowSyncFlowData(nwObjects, nwGroups, svcObjects, svcGroups, timeObjects, accesses);
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

            if (pendingByManagement.Count == 0)
            {
                Log.WriteWarning(LogMessageTitle, "Pending imports do not contain a management id.");
                return false;
            }

            List<int> configuredManagementRanking = FlowNamingHelper.ParseManagementRanking(globalConfig.FlowNamingSourceManagementRanking);
            List<int> preferredManagementRanking = FlowNamingHelper.NormalizeManagementRanking(
                configuredManagementRanking,
                pendingByManagement.Select(group => group.Key));
            bool useManagementNamesForFlow = configuredManagementRanking.Count > 0;
            if (useManagementNamesForFlow)
            {
                Dictionary<int, int> rankingPositions = preferredManagementRanking
                    .Select((managementId, index) => new { managementId, index })
                    .ToDictionary(item => item.managementId, item => item.index);

                pendingByManagement = [.. pendingByManagement
                    .OrderBy(group => rankingPositions.GetValueOrDefault(group.Key, int.MaxValue))
                    .ThenBy(group => group.Max(import => import.ControlId))];
            }

            bool syncedAny = false;

            foreach (var managementGroup in pendingByManagement)
            {
                int mgmId = managementGroup.Key;
                var importsForManagement = managementGroup.OrderBy(import => import.ControlId).ToList();

                try
                {
                    await SyncManagementAsync(mgmId, importsForManagement, useManagementNamesForFlow);
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
        private async Task SyncManagementAsync(int mgmId, List<ImportControl> importsForManagement, bool useManagementNamesForFlow)
        {
            var managementData = (await apiConnection.SendQueryAsync<List<FlowSyncManagementData>>(FlowQueries.getFlowSyncManagementData, new { mgmId }))?.FirstOrDefault();

            if (managementData == null)
            {
                Log.WriteWarning(LogMessageTitle, $"No management data returned for mgm_id {mgmId}.");
                return;
            }

            var flowData = await GetFlowSyncDataAsync(mgmId);

            // Process simple objects first, as they are used in groups and accesses
            await ProcessNetworkObjectsAsync(managementData.NetworkObjects.Where(o => o.Type.Name != ObjectType.Group), flowData, useManagementNamesForFlow);
            await ProcessServiceObjectsAsync(managementData.ServiceObjects.Where(s => s.Type.Name != ServiceType.Group), flowData, useManagementNamesForFlow);
            await ProcessTimeObjectsAsync(managementData.TimeObjects, flowData, useManagementNamesForFlow);
            // Refresh flow data to include newly inserted objects
            flowData = await GetFlowSyncDataAsync(mgmId);
            // Process groups next, as they are used in accesses
            await ProcessNetworkGroupsAsync(managementData.NetworkObjects.Where(o => o.Type.Name == ObjectType.Group), flowData, useManagementNamesForFlow);
            await ProcessServiceGroupsAsync(managementData.ServiceObjects.Where(s => s.Type.Name == ServiceType.Group), flowData, useManagementNamesForFlow);
            // Refresh flow data to include newly inserted groups
            flowData = await GetFlowSyncDataAsync(mgmId);
            // Finally, process accesses which reference all object types
            await ProcessRulesAsync(mgmId, managementData.Rules, flowData);

            // remove flow mappings from all normalized entries that are set to removed
            await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.updateFlowMappingsForRemoved, new { mgmId });

            // Mark imports as completed
            var maxImportId = importsForManagement.Max(i => i.ControlId);
            var updateCount = await apiConnection.SendQueryAsync<MutationResult>(FlowQueries.updateImportControlForFlowSync, new { controlId = maxImportId, mgmId, flowSyncDone = true });
        }

        /// <summary>
        /// Inserts missing flow network objects and updates normalized object mappings.
        /// </summary>
        private async Task ProcessNetworkObjectsAsync(IEnumerable<NetworkObject> nwObjects, FlowSyncFlowData flowData, bool useManagementNamesForFlow)
        {
            Dictionary<string, FlowNwObjectInsert> pendingNwObjInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> newFLowMappings = [];

            var skippedNwObjects = 0;

            foreach (var obj in nwObjects)
            {
                if (!TryBuildFlowNwObj(obj, flowData, pendingNwObjInserts, newFLowMappings, useManagementNamesForFlow))
                {
                    skippedNwObjects++;
                    continue;
                }
            }

            // make sure FlowActive is false for multiple mapping updates per hash
            newFLowMappings.Where(m => m.Value.Count > 1).ToList().ForEach(m => m.Value.ForEach(update => update.FlowActive = false));

            // insert newFlowNwObjects
            var newFlowNwObjects = pendingNwObjInserts.Values.ToList();
            if (newFlowNwObjects.Count != 0)
            {
                var insertResult = await apiConnection.SendQueryAsync<FlowNwObjectInsertResult>(FlowQueries.insertFlowNwObjects, new { objects = newFlowNwObjects });
                var insertedObjects = insertResult?.Returning ?? [];

                foreach (var inserted in insertedObjects)
                {
                    // update mapping updates needing flow id of newly inserted objects
                    newFLowMappings.GetValueOrDefault(inserted.Hash, []).ForEach(m => m.FlowId = inserted.Id);
                }

                Log.WriteInfo(LogMessageTitle, $"Inserted {insertedObjects.Count} new flow network objects for management. Skipped (non-technical): {skippedNwObjects}.");
            }

            // update normalized objects with flow mappings and flow_active status
            if (newFLowMappings.Count != 0)
            {
                var updates = new List<object>();
                foreach (var mapping in newFLowMappings.Values.SelectMany(m => m))
                {
                    updates.Add(new
                    {
                        where = new { obj_id = new { _eq = mapping.Id } },
                        _set = new { flow_nwobj_id = mapping.FlowId, flow_active = mapping.FlowActive }
                    });
                }

                var updateCount = await SendUpdateManyAsync(FlowQueries.updateObjectFlowMappings, updates);

                Log.WriteInfo(LogMessageTitle, $"Updated flow mappings for {updateCount} network objects");
            }
        }

        /// <summary>
        /// Builds or reuses a flow network object and prepares mapping updates for the normalized object.
        /// </summary>
        private static bool TryBuildFlowNwObj(NetworkObject obj, FlowSyncFlowData flowData, Dictionary<string, FlowNwObjectInsert> pendingInserts, Dictionary<string, List<FlowMappingUpdate>> newFlowMappings, bool useManagementNamesForFlow)
        {
            if (!TryGetFlowNwObjectHash(obj, flowData, out var hash))
            {
                return false;
            }

            var alreadyExists = flowData.NwObjects.TryGetValue(hash, out var existingFlowObj);
            var alreadyBeingInserted = pendingInserts.TryGetValue(hash, out var pendingInsert);

            if (!alreadyExists && !alreadyBeingInserted)
            {
                var newInsert = new FlowNwObjectInsert
                {
                    NwObjHash = hash,
                    IpStart = obj.IP,
                    IpEnd = obj.IpEnd,
                    State = FlowState.Implemented,
                    RemovedDate = null,
                    ShowInRequestModule = true,
                    Name = useManagementNamesForFlow ? obj.Name : null
                };
                pendingInserts.Add(hash, newInsert);
            }
            if (alreadyBeingInserted)
            {
                pendingInsert!.Name = null;
            }

            var flowActive = true;
            if (existingFlowObj != null && existingFlowObj.Objects?.Count > 0 || alreadyBeingInserted)
            {
                // a flow mapping exists for current management -> set flow_active to false
                flowActive = false;
            }

            if (!newFlowMappings.TryGetValue(hash, out var mappingUpdates))
            {
                mappingUpdates = [];
                newFlowMappings.Add(hash, mappingUpdates);
            }
            mappingUpdates.Add(new FlowMappingUpdate
            {
                Id = obj.Id,
                FlowId = existingFlowObj?.Id, // will be updated after insertion if object is new
                FlowActive = flowActive
            });

            return true;
        }


        /// <summary>
        /// Inserts missing flow service objects and updates normalized service mappings.
        /// </summary>
        private async Task ProcessServiceObjectsAsync(IEnumerable<NetworkService> svcObjects, FlowSyncFlowData flowData, bool useManagementNamesForFlow)
        {
            Dictionary<string, FlowSvcObjectInsert> pendingSvcObjInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> newFLowMappings = [];

            var skippedSvcObjects = 0;

            foreach (var svc in svcObjects)
            {
                if (!TryBuildFlowSvcObj(svc, flowData, pendingSvcObjInserts, newFLowMappings, useManagementNamesForFlow))
                {
                    skippedSvcObjects++;
                    continue;
                }
            }

            // make sure FlowActive is false for multiple mapping updates per hash
            newFLowMappings.Where(m => m.Value.Count > 1).ToList().ForEach(m => m.Value.ForEach(update => update.FlowActive = false));

            // insert newFlowSvcObjects
            var newFlowSvcObjects = pendingSvcObjInserts.Values.ToList();
            if (newFlowSvcObjects.Count != 0)
            {
                var insertResult = await apiConnection.SendQueryAsync<FlowSvcObjectInsertResult>(FlowQueries.insertFlowSvcObjects, new { objects = newFlowSvcObjects });
                var insertedObjects = insertResult?.Returning ?? [];

                foreach (var inserted in insertedObjects)
                {
                    newFLowMappings.GetValueOrDefault(inserted.Hash, []).ForEach(m => m.FlowId = inserted.Id);
                }

                Log.WriteInfo(LogMessageTitle, $"Inserted {insertedObjects.Count} new flow service objects for management. Skipped (missing proto): {skippedSvcObjects}.");
            }

            // update normalized services with flow mappings and flow_active status
            if (newFLowMappings.Count != 0)
            {
                var updates = new List<object>();
                foreach (var mapping in newFLowMappings.Values.SelectMany(m => m))
                {
                    updates.Add(new
                    {
                        where = new { svc_id = new { _eq = mapping.Id } },
                        _set = new { flow_svcobj_id = mapping.FlowId, flow_active = mapping.FlowActive }
                    });
                }

                var updateCount = await SendUpdateManyAsync(FlowQueries.updateServiceFlowMappings, updates);

                Log.WriteInfo(LogMessageTitle, $"Updated flow mappings for {updateCount} service objects");
            }
        }

        /// <summary>
        /// Builds or reuses a flow service object and prepares mapping updates for the normalized service.
        /// </summary>
        private static bool TryBuildFlowSvcObj(NetworkService svc, FlowSyncFlowData flowData, Dictionary<string, FlowSvcObjectInsert> pendingInserts, Dictionary<string, List<FlowMappingUpdate>> newFlowMappings, bool useManagementNamesForFlow)
        {
            if (!TryGetFlowSvcObjectHash(svc, flowData, out var hash))
            {
                return false;
            }

            var alreadyExists = flowData.SvcObjects.TryGetValue(hash, out var existingFlowObj);
            var alreadyBeingInserted = pendingInserts.TryGetValue(hash, out var pendingInsert);

            if (!alreadyExists && !alreadyBeingInserted)
            {
                var newInsert = new FlowSvcObjectInsert
                {
                    Name = useManagementNamesForFlow ? svc.Name : null,
                    PortStart = svc.DestinationPort,
                    PortEnd = svc.DestinationPortEnd,
                    IpProtoId = svc.ProtoId!.Value,
                    SvcObjHash = hash,
                    State = FlowState.Implemented,
                    RemovedDate = null,
                    ShowInRequestModule = true
                };
                pendingInserts.Add(hash, newInsert);
            }
            if (alreadyBeingInserted)
            {
                pendingInsert!.Name = null;
            }

            var flowActive = true;
            if (existingFlowObj != null && existingFlowObj.Services?.Count > 0 || alreadyBeingInserted)
            {
                // a flow mapping exists for current management -> set flow_active to false
                flowActive = false;
            }

            if (!newFlowMappings.TryGetValue(hash, out var mappingUpdates))
            {
                mappingUpdates = [];
                newFlowMappings.Add(hash, mappingUpdates);
            }
            mappingUpdates.Add(new FlowMappingUpdate
            {
                Id = svc.Id,
                FlowId = existingFlowObj?.Id, // will be updated after insertion if object is new
                FlowActive = flowActive
            });

            return true;
        }

        /// <summary>
        /// Inserts missing flow time objects and updates normalized time object mappings.
        /// </summary>
        private async Task ProcessTimeObjectsAsync(IEnumerable<TimeObject> timeObjects, FlowSyncFlowData flowData, bool useManagementNamesForFlow)
        {
            Dictionary<string, FlowTimeObjectInsert> pendingTimeObjInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> newFLowMappings = [];

            var skippedTimeObjects = 0;

            foreach (var timeObj in timeObjects)
            {
                if (!TryBuildFlowTimeObj(timeObj, flowData, pendingTimeObjInserts, newFLowMappings, useManagementNamesForFlow))
                {
                    skippedTimeObjects++;
                    continue;
                }
            }

            var newFlowTimeObjects = pendingTimeObjInserts.Values.ToList();
            if (newFlowTimeObjects.Count != 0)
            {
                var insertResult = await apiConnection.SendQueryAsync<FlowTimeObjectInsertResult>(FlowQueries.insertFlowTimeObjects, new { objects = newFlowTimeObjects });
                var insertedObjects = insertResult?.Returning ?? [];

                foreach (var inserted in insertedObjects)
                {
                    newFLowMappings.GetValueOrDefault(inserted.Hash, []).ForEach(m => m.FlowId = inserted.Id);
                }

                Log.WriteInfo(LogMessageTitle, $"Inserted {insertedObjects.Count} new flow time objects for management. Skipped (neither start nor end time specified): {skippedTimeObjects}.");
            }

            if (newFLowMappings.Count != 0)
            {
                var updates = new List<object>();
                foreach (var mapping in newFLowMappings.Values.SelectMany(m => m))
                {
                    updates.Add(new
                    {
                        where = new { time_obj_id = new { _eq = mapping.Id } },
                        _set = new { flow_timeobj_id = mapping.FlowId, flow_active = mapping.FlowActive }
                    });
                }

                var updateCount = await SendUpdateManyAsync(FlowQueries.updateTimeObjectFlowMappings, updates);

                Log.WriteInfo(LogMessageTitle, $"Updated flow mappings for {updateCount} time objects");
            }
        }

        /// <summary>
        /// Builds or reuses a flow time object and prepares mapping updates for the normalized time object.
        /// </summary>
        private static bool TryBuildFlowTimeObj(TimeObject timeObj, FlowSyncFlowData flowData, Dictionary<string, FlowTimeObjectInsert> pendingInserts, Dictionary<string, List<FlowMappingUpdate>> newFlowMappings, bool useManagementNamesForFlow)
        {
            if (!TryGetFlowTimeObjectHash(timeObj, flowData, out var hash))
            {
                return false;
            }

            var alreadyExists = flowData.TimeObjects.TryGetValue(hash, out var existingFlowObj);
            var alreadyBeingInserted = pendingInserts.TryGetValue(hash, out var pendingInsert);

            if (!alreadyExists && !alreadyBeingInserted)
            {
                var newInsert = new FlowTimeObjectInsert
                {
                    Name = useManagementNamesForFlow ? timeObj.Name : null,
                    StartTime = timeObj.StartTime,
                    EndTime = timeObj.EndTime,
                    TimeObjHash = hash,
                    State = FlowState.Implemented,
                    RemovedDate = null,
                    ShowInRequestModule = true
                };
                pendingInserts.Add(hash, newInsert);
            }

            if (alreadyBeingInserted)
            {
                pendingInsert!.Name = null;
            }

            var flowActive = true;
            if (existingFlowObj != null && existingFlowObj.TimeObjects?.Count > 0 || alreadyBeingInserted)
            {
                // a flow mapping exists for current management -> set flow_active to false
                flowActive = false;
            }

            if (!newFlowMappings.TryGetValue(hash, out var mappingUpdates))
            {
                mappingUpdates = [];
                newFlowMappings.Add(hash, mappingUpdates);
            }
            mappingUpdates.Add(new FlowMappingUpdate
            {
                Id = timeObj.Id,
                FlowId = existingFlowObj?.Id, // will be updated after insertion if object is new
                FlowActive = flowActive
            });

            return true;
        }


        /// <summary>
        /// Inserts missing flow network groups, including their member references, and updates normalized group mappings.
        /// </summary>
        private async Task ProcessNetworkGroupsAsync(IEnumerable<NetworkObject> nwGroups, FlowSyncFlowData flowData, bool useManagementNamesForFlow)
        {
            Dictionary<string, FlowNwGroupInsert> pendingNwGroupInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> newFLowMappings = [];

            var skippedNwGroups = 0;
            foreach (var group in nwGroups)
            {
                if (!TryBuildNwGroup(group, flowData, pendingNwGroupInserts, newFLowMappings, useManagementNamesForFlow))
                {
                    skippedNwGroups++;
                    continue; // Skip groups with non-technical members - they need to be manually created first
                }
            }

            // make sure FlowActive is false for multiple mapping updates per hash
            newFLowMappings.Where(m => m.Value.Count > 1).ToList().ForEach(m => m.Value.ForEach(update => update.FlowActive = false));

            // insert newFlowNwGroups and newFlowNwGroupMembers
            var newFlowNwGroups = pendingNwGroupInserts.Values.ToList();
            if (newFlowNwGroups.Count != 0)
            {
                var insertGroupResult = await apiConnection.SendQueryAsync<FlowNwGroupInsertResult>(FlowQueries.insertFlowNwGroups, new { objects = newFlowNwGroups });
                var insertedGroups = insertGroupResult?.Returning ?? [];

                foreach (var inserted in insertedGroups)
                {
                    // update mapping updates needing flow id of newly inserted groups
                    newFLowMappings.GetValueOrDefault(inserted.Hash, []).ForEach(m => m.FlowId = inserted.Id);
                }

                Log.WriteInfo(LogMessageTitle, $"Inserted {insertedGroups.Count} new flow network groups for management. Skipped (contains non-technical or empty): {skippedNwGroups}.");
            }

            // update normalized objects with flow mappings and flow_active status
            if (newFLowMappings.Count != 0)
            {
                var updates = new List<object>();
                foreach (var mapping in newFLowMappings.Values.SelectMany(m => m))
                {
                    updates.Add(new
                    {
                        where = new { obj_id = new { _eq = mapping.Id } },
                        _set = new { flow_nwgrp_id = mapping.FlowId, flow_active = mapping.FlowActive }
                    });
                }
                await SendUpdateManyAsync(FlowQueries.updateObjectFlowMappings, updates);

                Log.WriteInfo(LogMessageTitle, $"Updated flow mappings for {newFLowMappings.Count} network groups");
            }
        }

        /// <summary>
        /// Builds or reuses a flow network group and prepares mapping updates for the normalized group.
        /// </summary>
        private static bool TryBuildNwGroup(NetworkObject group, FlowSyncFlowData flowData, Dictionary<string, FlowNwGroupInsert> pendingInserts, Dictionary<string, List<FlowMappingUpdate>> newFlowMappings, bool useManagementNamesForFlow)
        {
            if (!TryBuildNwGroupMemberHashes(group, flowData, out var memberHashes))
            {
                return false;
            }

            var hash = FlowHashGenerator.GenerateGroupHash(memberHashes);
            var alreadyExists = flowData.NwGroups.TryGetValue(hash, out var existingFlowGroup);
            var alreadyBeingInserted = pendingInserts.TryGetValue(hash, out var pendingInsert);

            if (!alreadyExists && !alreadyBeingInserted)
            {
                var newInsert = new FlowNwGroupInsert
                {
                    Name = useManagementNamesForFlow ? group.Name : null,
                    NwGrpHash = hash,
                    State = FlowState.Implemented,
                    RemovedDate = null,
                    ShowInRequestModule = true,
                    NwGroupMembers = new FlowNwGroupInsertMembersContainer
                    {
                        Data = [.. memberHashes.Select(memberHash => new FlowNwGroupMemberInsert
                        {
                            NwObjId = flowData.NwObjects[memberHash].Id
                        })]
                    }
                };
                pendingInserts.Add(hash, newInsert);
            }
            if (alreadyBeingInserted)
            {
                pendingInsert!.Name = null;
            }

            var flowActive = true;
            if (existingFlowGroup != null && existingFlowGroup.Objects?.Count > 0 || alreadyBeingInserted)
            {
                // a flow mapping exists for current management -> set flow_active to false
                flowActive = false;
            }

            if (!newFlowMappings.TryGetValue(hash, out var mappingUpdates))
            {
                mappingUpdates = [];
                newFlowMappings.Add(hash, mappingUpdates);
            }
            mappingUpdates.Add(new FlowMappingUpdate
            {
                Id = group.Id,
                FlowId = existingFlowGroup?.Id, // will be updated after insertion if group is new
                FlowActive = flowActive
            });

            return true;
        }

        /// <summary>
        /// Inserts missing flow service groups, including their member references, and updates normalized group mappings.
        /// </summary>
        private async Task ProcessServiceGroupsAsync(IEnumerable<NetworkService> svcGroups, FlowSyncFlowData flowData, bool useManagementNamesForFlow)
        {
            Dictionary<string, FlowSvcGroupInsert> pendingSvcGroupInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> newFLowMappings = [];

            var skippedSvcGroups = 0;
            foreach (var group in svcGroups)
            {
                if (!TryBuildSvcGroup(group, flowData, pendingSvcGroupInserts, newFLowMappings, useManagementNamesForFlow))
                {
                    skippedSvcGroups++;
                    continue;
                }
            }

            // make sure FlowActive is false for multiple mapping updates per hash
            newFLowMappings.Where(m => m.Value.Count > 1).ToList().ForEach(m => m.Value.ForEach(update => update.FlowActive = false));

            // insert newFlowSvcGroups and newFlowSvcGroupMembers
            var newFlowSvcGroups = pendingSvcGroupInserts.Values.ToList();
            if (newFlowSvcGroups.Count != 0)
            {
                var insertGroupResult = await apiConnection.SendQueryAsync<FlowSvcGroupInsertResult>(FlowQueries.insertFlowSvcGroups, new { objects = newFlowSvcGroups });
                var insertedGroups = insertGroupResult?.Returning ?? [];

                foreach (var inserted in insertedGroups)
                {
                    newFLowMappings.GetValueOrDefault(inserted.Hash, []).ForEach(m => m.FlowId = inserted.Id);
                }

                Log.WriteInfo(LogMessageTitle, $"Inserted {insertedGroups.Count} new flow service groups for management. Skipped (contains non-technical or empty): {skippedSvcGroups}.");
            }

            // update normalized services with flow mappings and flow_active status
            if (newFLowMappings.Count != 0)
            {
                var updates = new List<object>();
                foreach (var mapping in newFLowMappings.Values.SelectMany(m => m))
                {
                    updates.Add(new
                    {
                        where = new { svc_id = new { _eq = mapping.Id } },
                        _set = new { flow_svcgrp_id = mapping.FlowId, flow_active = mapping.FlowActive }
                    });
                }
                await SendUpdateManyAsync(FlowQueries.updateServiceFlowMappings, updates);

                Log.WriteInfo(LogMessageTitle, $"Updated flow mappings for {newFLowMappings.Count} service groups");
            }
        }

        /// <summary>
        /// Builds or reuses a flow service group and prepares mapping updates for the normalized group.
        /// </summary>
        private static bool TryBuildSvcGroup(NetworkService group, FlowSyncFlowData flowData, Dictionary<string, FlowSvcGroupInsert> pendingInserts, Dictionary<string, List<FlowMappingUpdate>> newFlowMappings, bool useManagementNamesForFlow)
        {
            if (!TryBuildSvcGroupMemberHashes(group, flowData, out var memberHashes))
            {
                return false;
            }

            var hash = FlowHashGenerator.GenerateGroupHash(memberHashes);
            var alreadyExists = flowData.SvcGroups.TryGetValue(hash, out var existingFlowGroup);
            var alreadyBeingInserted = pendingInserts.TryGetValue(hash, out var pendingInsert);

            if (!alreadyExists && !alreadyBeingInserted)
            {
                var newInsert = new FlowSvcGroupInsert
                {
                    SvcGrpHash = hash,
                    Name = useManagementNamesForFlow ? group.Name : null,
                    State = FlowState.Implemented,
                    RemovedDate = null,
                    ShowInRequestModule = true,
                    SvcGroupMembers = new FlowSvcGroupInsertMembersContainer
                    {
                        Data = [.. memberHashes.Select(memberHash => new FlowSvcGroupMemberInsert
                        {
                            SvcObjId = flowData.SvcObjects[memberHash].Id
                        })]
                    }
                };
                pendingInserts.Add(hash, newInsert);
            }
            if (alreadyBeingInserted)
            {
                pendingInsert!.Name = null;
            }

            var flowActive = true;
            if (existingFlowGroup != null && existingFlowGroup.Services?.Count > 0 || alreadyBeingInserted)
            {
                // a flow mapping exists for current management -> set flow_active to false
                flowActive = false;
            }

            if (!newFlowMappings.TryGetValue(hash, out var mappingUpdates))
            {
                mappingUpdates = [];
                newFlowMappings.Add(hash, mappingUpdates);
            }
            mappingUpdates.Add(new FlowMappingUpdate
            {
                Id = group.Id,
                FlowId = existingFlowGroup?.Id, // will be updated after insertion if group is new
                FlowActive = flowActive
            });

            return true;
        }

        /// <summary>
        /// Inserts missing flow accesses and updates normalized rule mappings.
        /// </summary>
        private async Task ProcessRulesAsync(int mgmId, IEnumerable<Rule> rules, FlowSyncFlowData flowData)
        {
            Dictionary<string, FlowAccessInsert> pendingAccessInserts = [];
            Dictionary<string, List<FlowRuleMappingUpdate>> newFlowMappings = [];

            var skippedRules = 0;

            foreach (var rule in rules)
            {
                if (!TryBuildRuleAccess(rule, flowData, pendingAccessInserts, newFlowMappings))
                {
                    skippedRules++;
                    continue;
                }
            }

            // insert newFlowAccesses and newFlowAccessMembers
            var newFlowAccesses = pendingAccessInserts.Values.ToList();
            if (newFlowAccesses.Count != 0)
            {
                var insertResult = await apiConnection.SendQueryAsync<FlowAccessInsertResult>(FlowQueries.insertFlowAccesses, new { objects = newFlowAccesses });
                var insertedAccesses = insertResult?.Returning ?? [];

                foreach (var inserted in insertedAccesses)
                {
                    newFlowMappings.GetValueOrDefault(inserted.Hash, []).ForEach(m => m.FlowId = inserted.Id);
                }

                Log.WriteInfo(LogMessageTitle, $"Inserted {insertedAccesses.Count} new flow accesses for management {mgmId}. Skipped: {skippedRules}.");
            }

            // update normalized rules with flow mappings
            if (newFlowMappings.Count != 0)
            {
                var updates = new List<object>();
                foreach (var mapping in newFlowMappings.Values.SelectMany(m => m))
                {
                    updates.Add(new
                    {
                        where = new { rule_id = new { _eq = mapping.Id } },
                        _set = new { flow_access_id = mapping.FlowId }
                    });
                }

                var updateCount = await SendUpdateManyAsync(FlowQueries.updateRuleFlowMappings, updates);

                Log.WriteInfo(LogMessageTitle, $"Updated flow mappings for {updateCount} rules in management {mgmId}");
            }
        }

        /// <summary>
        /// Get the hash for a network object by first checking if it has technical properties (IP) to calculate the
        /// hash, and if not, looking up a stored hash in flow data. Returns false if no hash can be determined, which
        /// indicates that the object should be skipped (e.g. non-technical objects that need manual creation).
        /// </summary>
        /// <returns></returns>
        private static bool TryGetFlowNwObjectHash(NetworkObject obj, FlowSyncFlowData flowData, out string hash)
        {
            hash = "";
            if (string.IsNullOrWhiteSpace(obj.IP))
            {
                if (flowData.NwObjectHashes.TryGetValue(obj.Id, out var storedHash) && !string.IsNullOrWhiteSpace(storedHash))
                {
                    hash = storedHash;
                    return true;
                }
                return false; // Skip non-IP objects (e.g. FQDNs) - they need to be manually created
            }

            hash = FlowHashGenerator.GenerateNwObjectHash(obj.IP, obj.IpEnd);
            return true;
        }

        /// <summary>
        /// Resolves or calculates the hash for a service object when protocol and ports are available.
        /// </summary>
        private static bool TryGetFlowSvcObjectHash(NetworkService svc, FlowSyncFlowData flowData, out string hash)
        {
            hash = "";
            if (!svc.ProtoId.HasValue)
            {
                // objects without protocol are not supported - flow svcobjects require a protocol to be meaningful
                return false;
            }
            if (!svc.DestinationPort.HasValue || !svc.DestinationPortEnd.HasValue)
            {
                if (flowData.SvcObjectHashes.TryGetValue(svc.Id, out var storedHash) && !string.IsNullOrWhiteSpace(storedHash))
                {
                    hash = storedHash;
                    return true;
                }
                return false;
            }

            hash = FlowHashGenerator.GenerateSvcObjectHash(svc.ProtoId.Value, svc.DestinationPort.Value, svc.DestinationPortEnd.Value);
            return true;
        }

        /// <summary>
        /// Resolves or calculates the hash for a time object when start or end is present.
        /// </summary>
        private static bool TryGetFlowTimeObjectHash(TimeObject timeObj, FlowSyncFlowData flowData, out string hash)
        {
            hash = "";
            if (!timeObj.StartTime.HasValue && !timeObj.EndTime.HasValue)
            {
                if (flowData.TimeObjectHashes.TryGetValue(timeObj.Id, out var storedHash) && !string.IsNullOrWhiteSpace(storedHash))
                {
                    hash = storedHash;
                    return true;
                }
                return false;
            }

            hash = FlowHashGenerator.GenerateTimeObjectHash(timeObj.StartTime, timeObj.EndTime);
            return true;
        }

        /// <summary>
        /// Builds a list of member hashes for a network group by looking up each flat member's object hash. If any
        /// member is non-technical (no IP) and does not have a stored hash the method returns false indicating that
        /// the group should be skipped. Otherwise, it returns true and outputs the list of member hashes.
        /// </summary>
        private static bool TryBuildNwGroupMemberHashes(NetworkObject group, FlowSyncFlowData flowData, out HashSet<string> memberHashes)
        {
            memberHashes = [];
            if (group.ObjectGroupFlats == null || group.ObjectGroupFlats.Length == 0)
            {
                return false;
            }

            foreach (var member in group.ObjectGroupFlats)
            {
                if (member.Object == null)
                {
                    throw new InvalidOperationException($"Network group member {member.Id} does not have the object included in the query result");
                }
                if (member.Object.Type.Name == ObjectType.Group)
                {
                    continue;
                }
                if (!TryGetFlowNwObjectHash(member.Object, flowData, out var memberHash))
                {
                    return false;
                }
                if (!flowData.NwObjects.ContainsKey(memberHash))
                {
                    // technical member objects should have been previously inserted
                    throw new InvalidOperationException($"Network group member {member.Id} expected to have a corresponding flow object, but it was not found. Hash: {memberHash}");
                }
                memberHashes.Add(memberHash);
            }

            return memberHashes.Count > 0;
        }

        /// <summary>
        /// Builds a list of member hashes for a service group based on flat service members.
        /// </summary>
        private static bool TryBuildSvcGroupMemberHashes(NetworkService group, FlowSyncFlowData flowData, out HashSet<string> memberHashes)
        {
            memberHashes = [];
            if (group.ServiceGroupFlats == null || group.ServiceGroupFlats.Length == 0)
            {
                return false;
            }

            foreach (var member in group.ServiceGroupFlats)
            {
                if (member.Object == null)
                {
                    throw new InvalidOperationException($"Service group member {member.Id} does not have the service included in the query result");
                }
                if (member.Object.Type.Name == ServiceType.Group)
                {
                    continue;
                }
                if (!TryGetFlowSvcObjectHash(member.Object, flowData, out var memberHash))
                {
                    return false;
                }
                memberHashes.Add(memberHash);
            }

            return memberHashes.Count > 0;
        }

        /// <summary>
        /// Calculates all relevant hashes and collects the ids of all flow objects that technically define the rule
        /// access (src/dst/service), as well as time objects and group ids of any groups used in the rule. If any
        /// non-technical object is encountered that does not have a stored hash the method returns false indicating
        /// that the rule should be skipped. Otherwise, it returns true and outputs a FlowAccessInsert object if the
        /// access does not already exist, along with a flag indicating whether the access is new or already exists.
        /// The flow access insert object can then be used to insert a new flow access if needed, and the mapping
        /// updates can be used to link the rule to the flow access.
        /// </summary>
        private static bool TryBuildRuleAccess(
            Rule rule,
            FlowSyncFlowData flowData,
            Dictionary<string, FlowAccessInsert> pendingAccessInserts,
            Dictionary<string, List<FlowRuleMappingUpdate>> newFlowMappings)
        {
            var sourceIds = new HashSet<long>();
            var sourceGroupIds = new HashSet<long>();
            var destinationIds = new HashSet<long>();
            var destinationGroupIds = new HashSet<long>();
            var serviceIds = new HashSet<long>();
            var serviceGroupIds = new HashSet<long>();
            var timeIds = new HashSet<long>();

            var sourceHashes = new HashSet<string>();
            var destinationHashes = new HashSet<string>();
            var serviceHashes = new HashSet<string>();

            foreach (var location in rule.Froms)
            {
                if (!TryAddRuleNetworkLocation(location.Object, flowData, sourceIds, sourceGroupIds, sourceHashes))
                {
                    Log.WriteDebug(LogMessageTitle, $"Skipping rule {rule.Id} because source object {location.Object.Id} does not have a valid hash and cannot be added to the flow access.");
                    return false;
                }
            }

            foreach (var location in rule.Tos)
            {
                if (!TryAddRuleNetworkLocation(location.Object, flowData, destinationIds, destinationGroupIds, destinationHashes))
                {
                    Log.WriteDebug(LogMessageTitle, $"Skipping rule {rule.Id} because destination object {location.Object.Id} does not have a valid hash and cannot be added to the flow access.");
                    return false;
                }
            }

            foreach (var wrapper in rule.Services)
            {
                if (!TryAddRuleService(wrapper.Content, flowData, serviceIds, serviceGroupIds, serviceHashes))
                {
                    Log.WriteDebug(LogMessageTitle, $"Skipping rule {rule.Id} because service object {wrapper.Content.Id} does not have a valid hash and cannot be added to the flow access.");
                    return false;
                }
            }

            if (sourceHashes.Count == 0 || destinationHashes.Count == 0 || serviceHashes.Count == 0)
            {
                Log.WriteDebug(LogMessageTitle, $"Skipping rule {rule.Id} because one or more required objects do not have valid hashes.");
                return false;
            }

            if (!TryAddRuleTimeObjects(rule.RuleTimes, flowData, timeIds))
            {
                Log.WriteDebug(LogMessageTitle, $"Skipping rule {rule.Id} because time objects do not have valid hashes and cannot be added to the flow access.");
                return false;
            }

            string accessHash = FlowHashGenerator.GenerateAccessHash(sourceHashes, destinationHashes, serviceHashes);
            var alreadyExists = flowData.Accesses.TryGetValue(accessHash, out var existingAccess);
            var alreadyBeingInserted = pendingAccessInserts.ContainsKey(accessHash);

            if (!newFlowMappings.TryGetValue(accessHash, out var mappingUpdates))
            {
                mappingUpdates = [];
                newFlowMappings.Add(accessHash, mappingUpdates);
            }
            mappingUpdates.Add(new FlowRuleMappingUpdate
            {
                Id = rule.Id,
                FlowId = existingAccess?.Id
            });

            if (!alreadyExists && !alreadyBeingInserted)
            {
                var accessInsert = new FlowAccessInsert
                {
                    AccessHash = accessHash,
                    RequesterId = null,
                    OwnerId = rule.OwnerId,
                    State = FlowState.Implemented,
                    RemovedDate = null,
                    AccessSources = FlowAccessInsertHelper.BuildMembersContainer(sourceIds.Select(id => new NwRef { NwObjId = id })),
                    AccessSourceGroups = FlowAccessInsertHelper.BuildMembersContainer(sourceGroupIds.Select(id => new NwGroupRef { NwGroupId = id })),
                    AccessDestinations = FlowAccessInsertHelper.BuildMembersContainer(destinationIds.Select(id => new NwRef { NwObjId = id })),
                    AccessDestinationGroups = FlowAccessInsertHelper.BuildMembersContainer(destinationGroupIds.Select(id => new NwGroupRef { NwGroupId = id })),
                    AccessServices = FlowAccessInsertHelper.BuildMembersContainer(serviceIds.Select(id => new SvcRef { SvcObjId = id })),
                    AccessServiceGroups = FlowAccessInsertHelper.BuildMembersContainer(serviceGroupIds.Select(id => new SvcGroupRef { SvcGroupId = id })),
                    AccessTimeObjects = FlowAccessInsertHelper.BuildMembersContainer(timeIds.Select(id => new TimeRef { TimeObjId = id }))
                };

                pendingAccessInserts.Add(accessHash, accessInsert);
            }

            return true;
        }

        /// <summary>
        /// Collects the flow nwobj ids of the network objects that technically define the rule src/dst, as well as the
        /// flow nwgroup ids of groups used in the rule. If obj is a group, all non-group members' ids are added.
        /// Hashes of all involved non-group objects are added to the provided hash set for later access hash generation
        /// Returns false if any non-technical object is encountered that does not have a stored hash.
        /// An empty group in src/dst is not supported and will also lead to a false return value. An empty group
        /// within a group in src/dst is supported, as long as the top-level group has at least one technical member.
        /// </summary>
        private static bool TryAddRuleNetworkLocation(
            NetworkObject obj,
            FlowSyncFlowData flowData,
            HashSet<long> objectIds,
            HashSet<long> groupIds,
            HashSet<string> hashes)
        {
            if (obj.Type.Name == ObjectType.Group)
            {
                if (!TryBuildNwGroupMemberHashes(obj, flowData, out var memberHashes))
                {
                    return false;
                }
                var groupHash = FlowHashGenerator.GenerateGroupHash(memberHashes);
                if (!flowData.NwGroups.TryGetValue(groupHash, out var flowGroup))
                {
                    return false;
                }
                objectIds.UnionWith(flowGroup.NwGroupMembers.Select(m => m.NwObjectId));
                groupIds.Add(flowGroup.Id);
                hashes.UnionWith(memberHashes);
            }
            else
            {
                if (!TryGetFlowNwObjectHash(obj, flowData, out var objHash))
                {
                    return false;
                }
                if (!flowData.NwObjects.TryGetValue(objHash, out FlowNwObject? flowNwObj))
                {
                    // technical objects should have been previously inserted
                    throw new InvalidOperationException($"Network object {obj.Id} expected to have a corresponding flow object, but it was not found. Hash: {objHash}");
                }
                objectIds.Add(flowNwObj.Id);
                hashes.Add(objHash);
            }

            return true;
        }

        /// <summary>
        /// Collects the flow svcobj ids of the services that technically define the rule service, as well as the
        /// flow svcgroup ids of groups used in the rule. If a group is used, all non-group members' ids are added.
        /// Hashes of all involved non-group services are added to the provided hash set for later access hash
        /// generation. Returns false if any non-technical service is encountered that does not have a stored hash.
        /// An empty group in services is not supported and will also lead to a false return value. An empty group
        /// within a group in services is supported, as long as the top-level group has at least one technical member.
        /// </summary>
        private static bool TryAddRuleService(
            NetworkService svc,
            FlowSyncFlowData flowData,
            HashSet<long> serviceIds,
            HashSet<long> serviceGroupIds,
            HashSet<string> hashes)
        {
            if (svc.Type.Name == ServiceType.Group)
            {
                if (!TryBuildSvcGroupMemberHashes(svc, flowData, out var memberHashes))
                {
                    return false;
                }
                var groupHash = FlowHashGenerator.GenerateGroupHash(memberHashes);
                if (!flowData.SvcGroups.TryGetValue(groupHash, out var flowGroup))
                {
                    return false;
                }
                serviceIds.UnionWith(flowGroup.SvcGroupMembers.Select(m => m.SvcObjectId));
                serviceGroupIds.Add(flowGroup.Id);
                hashes.UnionWith(memberHashes);
            }
            else
            {
                if (!TryGetFlowSvcObjectHash(svc, flowData, out var svcHash))
                {
                    return false;
                }
                if (!flowData.SvcObjects.TryGetValue(svcHash, out FlowSvcObject? flowSvcObj))
                {
                    // technical services should have been previously inserted
                    throw new InvalidOperationException($"Service {svc.Id} expected to have a corresponding flow object, but it was not found. Hash: {svcHash}");
                }
                serviceIds.Add(flowSvcObj.Id);
                hashes.Add(svcHash);
            }

            return true;
        }

        /// <summary>
        /// Collects the flow timeobj ids of the time objects used in the rule.
        /// </summary>
        private static bool TryAddRuleTimeObjects(
            IEnumerable<RuleTime> ruleTimes,
            FlowSyncFlowData flowData,
            HashSet<long> timeIds)
        {
            foreach (var ruleTime in ruleTimes)
            {
                if (ruleTime.TimeObj == null)
                {
                    throw new InvalidOperationException($"RuleTime {ruleTime.Id} does not have the time object included in the query result");
                }
                if (!TryGetFlowTimeObjectHash(ruleTime.TimeObj, flowData, out var timeHash))
                {
                    return false;
                }
                if (!flowData.TimeObjects.TryGetValue(timeHash, out FlowTimeObject? flowTimeObj))
                {
                    // technical time objects should have been previously inserted
                    throw new InvalidOperationException($"Time object {ruleTime.TimeObj.Id} expected to have a corresponding flow object, but it was not found. Hash: {timeHash}");
                }
                timeIds.Add(flowTimeObj.Id);
            }

            return true;
        }

        /// <summary>
        /// Batch sends update mutations to the database.
        /// </summary>
        private async Task<int> SendUpdateManyAsync(string query, List<object> updates)
        {
            if (updates.Count == 0)
                return 0;
            var result = await apiConnection.SendQueryAsync<List<MutationResult>>(query, new { updates });
            return result?.Sum(r => r.AffectedRows) ?? 0;
        }

    }
}
