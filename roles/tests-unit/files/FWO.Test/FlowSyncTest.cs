using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Services;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowSyncTest
    {
        private sealed class FlowSyncTestApiConn : SimulatedApiConnection
        {
            private long nextFlowNetworkObjectId = 100;
            private long nextFlowNetworkGroupId = 150;
            private long nextFlowServiceObjectId = 200;
            private long nextFlowServiceGroupId = 240;
            private long nextFlowTimeObjectId = 250;
            private long nextFlowAccessId = 300;

            public List<FlowNwObject> FlowNetworkObjects { get; } = [];
            public List<FlowNwGroup> FlowNetworkGroups { get; } = [];
            public List<FlowSvcObject> FlowServiceObjects { get; } = [];
            public List<FlowSvcGroup> FlowServiceGroups { get; } = [];
            public List<FlowTimeObject> FlowTimeObjects { get; } = [];
            public List<FlowAccess> FlowAccesses { get; } = [];
            public List<FlowNwObjectInsert> InsertedNetworkObjects { get; } = [];
            public List<FlowNwGroupInsert> InsertedNetworkGroups { get; } = [];
            public List<FlowSvcObjectInsert> InsertedServiceObjects { get; } = [];
            public List<FlowSvcGroupInsert> InsertedServiceGroups { get; } = [];
            public List<FlowTimeObjectInsert> InsertedTimeObjects { get; } = [];
            public List<FlowAccessInsert> InsertedAccesses { get; } = [];
            public List<object> NetworkObjectMappingUpdates { get; } = [];
            public List<object> ServiceObjectMappingUpdates { get; } = [];
            public List<object> TimeObjectMappingUpdates { get; } = [];
            public List<object> RuleMappingUpdates { get; } = [];
            public bool RemovedMappingsCleared { get; private set; }
            public long? CompletedImportControlId { get; private set; }

            public FlowSyncManagementData ManagementData { get; set; } = new();
            public Dictionary<int, FlowSyncManagementData> ManagementDataById { get; set; } = [];
            public HashSet<int> ManagementIdsWithoutData { get; } = [];
            public List<ImportControl> PendingImports { get; set; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FlowQueries.getPendingFlowSyncImports)
                {
                    return Task.FromResult((T)(object)PendingImports);
                }
                if (query == FlowQueries.getFlowSyncManagementData)
                {
                    int mgmId = GetVariable<int>(variables, "mgmId");
                    if (ManagementIdsWithoutData.Contains(mgmId))
                    {
                        return Task.FromResult((T)(object)new List<FlowSyncManagementData>());
                    }
                    if (ManagementDataById.TryGetValue(mgmId, out FlowSyncManagementData? managementData))
                    {
                        return Task.FromResult((T)(object)new List<FlowSyncManagementData> { managementData });
                    }

                    return Task.FromResult((T)(object)new List<FlowSyncManagementData> { ManagementData });
                }
                if (query == FlowQueries.getFlowSyncNwObjects)
                {
                    return Task.FromResult((T)(object)FlowNetworkObjects);
                }
                if (query == FlowQueries.getFlowSyncNwGroups)
                {
                    return Task.FromResult((T)(object)FlowNetworkGroups);
                }
                if (query == FlowQueries.getFlowSyncSvcObjects)
                {
                    return Task.FromResult((T)(object)FlowServiceObjects);
                }
                if (query == FlowQueries.getFlowSyncSvcGroups)
                {
                    return Task.FromResult((T)(object)FlowServiceGroups);
                }
                if (query == FlowQueries.getFlowSyncTimeObjects)
                {
                    return Task.FromResult((T)(object)FlowTimeObjects);
                }
                if (query == FlowQueries.getFlowSyncAccesses)
                {
                    return Task.FromResult((T)(object)FlowAccesses);
                }
                if (query == FlowQueries.insertFlowNwObjects)
                {
                    List<FlowNwObjectInsert> inserts = GetObjects<FlowNwObjectInsert>(variables);
                    InsertedNetworkObjects.AddRange(inserts);
                    List<FlowNwObject> inserted = [.. inserts.Select(insert => new FlowNwObject
                    {
                        Id = ++nextFlowNetworkObjectId,
                        Name = insert.Name,
                        IpStart = insert.IpStart,
                        IpEnd = insert.IpEnd,
                        Hash = insert.NwObjHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule
                    })];
                    FlowNetworkObjects.AddRange(inserted);
                    return Task.FromResult((T)(object)new FlowNwObjectInsertResult { Returning = inserted });
                }
                if (query == FlowQueries.insertFlowNwGroups)
                {
                    List<FlowNwGroupInsert> inserts = GetObjects<FlowNwGroupInsert>(variables);
                    InsertedNetworkGroups.AddRange(inserts);
                    List<FlowNwGroup> inserted = [.. inserts.Select(insert => new FlowNwGroup
                    {
                        Id = ++nextFlowNetworkGroupId,
                        Name = insert.Name ?? "",
                        Hash = insert.NwGrpHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule,
                        NwGroupMembers = [.. (insert.NwGroupMembers?.Data ?? []).Select(member => new FlowNwGroupMember
                        {
                            NwGroupId = nextFlowNetworkGroupId,
                            NwObjectId = member.NwObjId
                        })]
                    })];
                    FlowNetworkGroups.AddRange(inserted);
                    return Task.FromResult((T)(object)new FlowNwGroupInsertResult { Returning = inserted });
                }
                if (query == FlowQueries.insertFlowSvcObjects)
                {
                    List<FlowSvcObjectInsert> inserts = GetObjects<FlowSvcObjectInsert>(variables);
                    InsertedServiceObjects.AddRange(inserts);
                    List<FlowSvcObject> inserted = [.. inserts.Select(insert => new FlowSvcObject
                    {
                        Id = ++nextFlowServiceObjectId,
                        Name = insert.Name ?? "",
                        PortStart = insert.PortStart,
                        PortEnd = insert.PortEnd,
                        ProtoId = insert.IpProtoId,
                        Hash = insert.SvcObjHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule
                    })];
                    FlowServiceObjects.AddRange(inserted);
                    return Task.FromResult((T)(object)new FlowSvcObjectInsertResult { Returning = inserted });
                }
                if (query == FlowQueries.insertFlowSvcGroups)
                {
                    List<FlowSvcGroupInsert> inserts = GetObjects<FlowSvcGroupInsert>(variables);
                    InsertedServiceGroups.AddRange(inserts);
                    List<FlowSvcGroup> inserted = [.. inserts.Select(insert => new FlowSvcGroup
                    {
                        Id = ++nextFlowServiceGroupId,
                        Name = insert.Name ?? "",
                        Hash = insert.SvcGrpHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule,
                        SvcGroupMembers = [.. (insert.SvcGroupMembers?.Data ?? []).Select(member => new FlowSvcGroupMember
                        {
                            SvcGroupId = nextFlowServiceGroupId,
                            SvcObjectId = member.SvcObjId
                        })]
                    })];
                    FlowServiceGroups.AddRange(inserted);
                    return Task.FromResult((T)(object)new FlowSvcGroupInsertResult { Returning = inserted });
                }
                if (query == FlowQueries.insertFlowTimeObjects)
                {
                    List<FlowTimeObjectInsert> inserts = GetObjects<FlowTimeObjectInsert>(variables);
                    InsertedTimeObjects.AddRange(inserts);
                    List<FlowTimeObject> inserted = [.. inserts.Select(insert => new FlowTimeObject
                    {
                        Id = ++nextFlowTimeObjectId,
                        Name = insert.Name ?? "",
                        StartTime = insert.StartTime,
                        EndTime = insert.EndTime,
                        Hash = insert.TimeObjHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule
                    })];
                    FlowTimeObjects.AddRange(inserted);
                    return Task.FromResult((T)(object)new FlowTimeObjectInsertResult { Returning = inserted });
                }
                if (query == FlowQueries.insertFlowAccesses)
                {
                    List<FlowAccessInsert> inserts = GetObjects<FlowAccessInsert>(variables);
                    InsertedAccesses.AddRange(inserts);
                    List<FlowAccess> inserted = [.. inserts.Select(insert => new FlowAccess
                    {
                        Id = ++nextFlowAccessId,
                        Hash = insert.AccessHash ?? "",
                        OwnerId = insert.OwnerId,
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        AllowsTraffic = insert.AllowsTraffic
                    })];
                    FlowAccesses.AddRange(inserted);
                    return Task.FromResult((T)(object)new FlowAccessInsertResult { Returning = inserted });
                }
                if (query == FlowQueries.updateObjectFlowMappings)
                {
                    NetworkObjectMappingUpdates.AddRange(GetUpdates(variables));
                    return Task.FromResult((T)(object)new List<MutationResult> { new() { AffectedRows = NetworkObjectMappingUpdates.Count } });
                }
                if (query == FlowQueries.updateServiceFlowMappings)
                {
                    ServiceObjectMappingUpdates.AddRange(GetUpdates(variables));
                    return Task.FromResult((T)(object)new List<MutationResult> { new() { AffectedRows = ServiceObjectMappingUpdates.Count } });
                }
                if (query == FlowQueries.updateTimeObjectFlowMappings)
                {
                    TimeObjectMappingUpdates.AddRange(GetUpdates(variables));
                    return Task.FromResult((T)(object)new List<MutationResult> { new() { AffectedRows = TimeObjectMappingUpdates.Count } });
                }
                if (query == FlowQueries.updateRuleFlowMappings)
                {
                    RuleMappingUpdates.AddRange(GetUpdates(variables));
                    return Task.FromResult((T)(object)new List<MutationResult> { new() { AffectedRows = RuleMappingUpdates.Count } });
                }
                if (query == FlowQueries.updateFlowMappingsForRemoved)
                {
                    RemovedMappingsCleared = true;
                    return Task.FromResult((T)(object)new MutationResult { AffectedRows = 0 });
                }
                if (query == FlowQueries.updateImportControlForFlowSync)
                {
                    CompletedImportControlId = GetVariable<long>(variables, "controlId");
                    return Task.FromResult((T)(object)new MutationResult { AffectedRows = 1 });
                }

                throw new AssertionException($"Unexpected query: {query}");
            }

            private static List<TObject> GetObjects<TObject>(object? variables)
            {
                object? objects = variables?.GetType().GetProperty("objects")?.GetValue(variables);
                return objects switch
                {
                    IEnumerable<TObject> typedObjects => [.. typedObjects],
                    _ => throw new AssertionException("Mutation variables did not contain the expected objects list.")
                };
            }

            private static List<object> GetUpdates(object? variables)
            {
                object? updates = variables?.GetType().GetProperty("updates")?.GetValue(variables);
                return updates switch
                {
                    IEnumerable<object> typedUpdates => [.. typedUpdates],
                    _ => throw new AssertionException("Mutation variables did not contain the expected updates list.")
                };
            }
        }

        [Test]
        public async Task Run_ReturnsFalseWhenNoPendingImportsExist()
        {
            FlowSyncTestApiConn apiConn = new();
            FlowSync flowSync = new(apiConn, new GlobalConfig());

            bool result = await flowSync.Run();

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedNetworkObjects, Is.Empty);
            Assert.That(apiConn.CompletedImportControlId, Is.Null);
        }

        [Test]
        public async Task Run_ReturnsFalseWhenPendingImportsHaveNoManagementId()
        {
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports = [new ImportControl { ControlId = 9, MgmId = null }]
            };
            FlowSync flowSync = new(apiConn, new GlobalConfig());

            bool result = await flowSync.Run();

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedNetworkObjects, Is.Empty);
            Assert.That(apiConn.CompletedImportControlId, Is.Null);
        }

        [Test]
        public async Task Run_SkipsManagementWhenNoManagementDataIsReturned()
        {
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports = [new ImportControl { ControlId = 9, MgmId = 7 }]
            };
            apiConn.ManagementIdsWithoutData.Add(7);
            FlowSync flowSync = new(apiConn, new GlobalConfig());

            bool result = await flowSync.Run();

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkObjects, Is.Empty);
            Assert.That(apiConn.RemovedMappingsCleared, Is.False);
            Assert.That(apiConn.CompletedImportControlId, Is.Null);
        }

        [Test]
        public async Task Run_InsertsMissingFlowObjectsAccessAndUpdatesMappings()
        {
            NetworkObject source = CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1");
            NetworkObject destination = CreateNetworkObject(2, "dst", "10.0.1.1", "10.0.1.1");
            NetworkService service = CreateService(3, "https", 6, 443, 443);
            Rule rule = new()
            {
                Id = 4,
                OwnerId = 5,
                Froms = [new NetworkLocation(new NetworkUser(), source)],
                Tos = [new NetworkLocation(new NetworkUser(), destination)],
                Services = [new ServiceWrapper { Content = service }]
            };
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports = [new ImportControl { ControlId = 9, MgmId = 7 }],
                ManagementData = new FlowSyncManagementData
                {
                    Id = 7,
                    NetworkObjects = [source, destination],
                    ServiceObjects = [service],
                    Rules = [rule]
                }
            };
            FlowSync flowSync = new(apiConn, new GlobalConfig());

            bool result = await flowSync.Run();

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkObjects, Has.Count.EqualTo(2));
            Assert.That(apiConn.InsertedNetworkObjects.Select(insert => insert.State), Is.All.EqualTo(FlowState.Implemented));
            Assert.That(apiConn.InsertedServiceObjects, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedServiceObjects[0].State, Is.EqualTo(FlowState.Implemented));
            Assert.That(apiConn.InsertedAccesses, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccesses[0].OwnerId, Is.EqualTo(5));
            Assert.That(apiConn.InsertedAccesses[0].State, Is.EqualTo(FlowState.Implemented));
            Assert.That(apiConn.InsertedAccesses[0].AllowsTraffic, Is.True);
            Assert.That(apiConn.NetworkObjectMappingUpdates, Has.Count.EqualTo(2));
            Assert.That(apiConn.ServiceObjectMappingUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConn.RuleMappingUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConn.RemovedMappingsCleared, Is.True);
            Assert.That(apiConn.CompletedImportControlId, Is.EqualTo(9));
        }

        [Test]
        public async Task Run_CreatesBlockingFlowAccessForDenyRule()
        {
            NetworkObject source = CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1");
            NetworkObject destination = CreateNetworkObject(2, "dst", "10.0.1.1", "10.0.1.1");
            NetworkService service = CreateService(3, "https", 6, 443, 443);
            Rule rule = new()
            {
                Id = 4,
                OwnerId = 5,
                Action = "deny",
                Froms = [new NetworkLocation(new NetworkUser(), source)],
                Tos = [new NetworkLocation(new NetworkUser(), destination)],
                Services = [new ServiceWrapper { Content = service }]
            };
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports = [new ImportControl { ControlId = 9, MgmId = 7 }],
                ManagementData = new FlowSyncManagementData
                {
                    Id = 7,
                    NetworkObjects = [source, destination],
                    ServiceObjects = [service],
                    Rules = [rule]
                }
            };
            FlowSync flowSync = new(apiConn, new GlobalConfig());

            bool result = await flowSync.Run();

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedAccesses, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccesses[0].AllowsTraffic, Is.False);
        }

        [Test]
        public async Task Run_InsertsTimeObjectAndCreatesTimedAccess()
        {
            NetworkObject source = CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1");
            NetworkObject destination = CreateNetworkObject(2, "dst", "10.0.1.1", "10.0.1.1");
            NetworkService service = CreateService(3, "https", 6, 443, 443);
            DateTime endTime = new(2026, 7, 31, 23, 59, 0, DateTimeKind.Utc);
            TimeObject timeObj = new() { Id = 4, Name = "july", EndTime = endTime };
            Rule rule = new()
            {
                Id = 5,
                OwnerId = 6,
                Froms = [new NetworkLocation(new NetworkUser(), source)],
                Tos = [new NetworkLocation(new NetworkUser(), destination)],
                Services = [new ServiceWrapper { Content = service }],
                RuleTimes = [new RuleTime { Id = 7, TimeObjId = timeObj.Id, TimeObj = timeObj }]
            };
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports = [new ImportControl { ControlId = 9, MgmId = 7 }],
                ManagementData = new FlowSyncManagementData
                {
                    Id = 7,
                    NetworkObjects = [source, destination],
                    ServiceObjects = [service],
                    TimeObjects = [timeObj],
                    Rules = [rule]
                }
            };
            FlowSync flowSync = new(apiConn, new GlobalConfig());

            bool result = await flowSync.Run();

            string timeHash = FlowHashGenerator.GenerateTimeObjectHash(null, endTime);
            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedTimeObjects, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedTimeObjects[0].TimeObjHash, Is.EqualTo(timeHash));
            Assert.That(apiConn.TimeObjectMappingUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccesses, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccesses[0].AccessTimeObjects!.Data.OfType<TimeRef>().Single().TimeObjId, Is.EqualTo(apiConn.FlowTimeObjects.Single().Id));
            Assert.That(apiConn.RuleMappingUpdates, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Run_InsertsGroupsCreatesGroupedAccessAndCompletesLatestImport()
        {
            NetworkObject source = CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1");
            NetworkObject destination = CreateNetworkObject(2, "dst", "10.0.1.1", "10.0.1.1");
            NetworkObject sourceGroup = CreateNetworkGroup(10, "source-group", source);
            NetworkService service = CreateService(3, "https", 6, 443, 443);
            NetworkService serviceGroup = CreateServiceGroup(20, "service-group", service);
            Rule rule = CreateRule(30, sourceGroup, destination, serviceGroup);
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports =
                [
                    new ImportControl { ControlId = 8, MgmId = 7 },
                    new ImportControl { ControlId = 9, MgmId = 7 }
                ],
                ManagementData = new FlowSyncManagementData
                {
                    Id = 7,
                    NetworkObjects = [source, destination, sourceGroup],
                    ServiceObjects = [service, serviceGroup],
                    Rules = [rule]
                }
            };
            FlowSync flowSync = new(apiConn, new GlobalConfig { FlowNamingSourceManagementRanking = "[7]" });

            bool result = await flowSync.Run();

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkObjects, Has.Count.EqualTo(2));
            Assert.That(apiConn.InsertedServiceObjects, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedNetworkGroups, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedNetworkGroups[0].Name, Is.EqualTo("source-group"));
            Assert.That(apiConn.InsertedServiceGroups, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedServiceGroups[0].Name, Is.EqualTo("service-group"));
            Assert.That(apiConn.InsertedAccesses, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccesses[0].AccessSourceGroups!.Data.OfType<NwGroupRef>().Single().NwGroupId, Is.EqualTo(apiConn.FlowNetworkGroups.Single().Id));
            Assert.That(apiConn.InsertedAccesses[0].AccessServiceGroups!.Data.OfType<SvcGroupRef>().Single().SvcGroupId, Is.EqualTo(apiConn.FlowServiceGroups.Single().Id));
            Assert.That(apiConn.NetworkObjectMappingUpdates, Has.Count.EqualTo(3));
            Assert.That(apiConn.ServiceObjectMappingUpdates, Has.Count.EqualTo(2));
            Assert.That(apiConn.RuleMappingUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConn.CompletedImportControlId, Is.EqualTo(9));
        }

        [Test]
        public void FlowSyncFlowData_IndexesExistingFlowObjectsAndReverseMappings()
        {
            string nwHash = FlowHashGenerator.GenerateNwObjectHash("10.0.0.1", "10.0.0.1");
            string svcHash = FlowHashGenerator.GenerateSvcObjectHash(6, 443, 443);
            DateTime timeEnd = new(2026, 7, 9, 23, 59, 0, DateTimeKind.Utc);
            string timeHash = FlowHashGenerator.GenerateTimeObjectHash(null, timeEnd);
            FlowNwObject nwObject = new()
            {
                Id = 10,
                Hash = nwHash,
                Objects = [CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1")]
            };
            FlowSvcObject svcObject = new()
            {
                Id = 20,
                Hash = svcHash,
                Services = [CreateService(2, "https", 6, 443, 443)]
            };
            FlowTimeObject timeObject = new()
            {
                Id = 30,
                EndTime = timeEnd,
                Hash = timeHash,
                TimeObjects = [new TimeObject { Id = 3 }]
            };
            RuleAction blockedAction = new() { Id = 99, Name = "block", Allowed = false };

            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [nwObject], svcObjects: [svcObject], timeObjects: [timeObject], ruleActions: [blockedAction]);

            Assert.That(flowData.NwObjects[nwHash].Id, Is.EqualTo(10));
            Assert.That(flowData.NwObjectsById[10].Hash, Is.EqualTo(nwHash));
            Assert.That(flowData.NwObjectHashes[1], Is.EqualTo(nwHash));
            Assert.That(flowData.SvcObjects[svcHash].Id, Is.EqualTo(20));
            Assert.That(flowData.SvcObjectsById[20].Hash, Is.EqualTo(svcHash));
            Assert.That(flowData.SvcObjectHashes[2], Is.EqualTo(svcHash));
            Assert.That(flowData.TimeObjects[timeHash].Id, Is.EqualTo(30));
            Assert.That(flowData.TimeObjectsById[30].Hash, Is.EqualTo(timeHash));
            Assert.That(flowData.TimeObjectHashes[3], Is.EqualTo(timeHash));
            Assert.That(flowData.RuleActionsById[99].Allowed, Is.False);
        }

        [Test]
        public void FlowSyncFlowData_AddTimeObject_UpdatesHashAndIdIndexes()
        {
            DateTime timeEnd = new(2026, 7, 9, 23, 59, 0, DateTimeKind.Utc);
            string timeHash = FlowHashGenerator.GenerateTimeObjectHash(null, timeEnd);
            FlowSyncFlowData flowData = CreateFlowData();
            FlowTimeObject timeObject = new()
            {
                Id = 30,
                EndTime = timeEnd,
                Hash = timeHash
            };

            flowData.Add(timeObject);

            Assert.That(flowData.TimeObjects[timeHash], Is.SameAs(timeObject));
            Assert.That(flowData.TimeObjectsById[30], Is.SameAs(timeObject));
        }

        [Test]
        public void FlowAccess_TryCalculateHash_IncludesTimeObjectAndAllowsTraffic()
        {
            string sourceHash = FlowHashGenerator.GenerateNwObjectHash("10.0.0.1", "10.0.0.1");
            string destinationHash = FlowHashGenerator.GenerateNwObjectHash("10.0.1.1", "10.0.1.1");
            string serviceHash = FlowHashGenerator.GenerateSvcObjectHash(6, 443, 443);
            DateTime timeEnd = new(2026, 7, 9, 23, 59, 0, DateTimeKind.Utc);
            string timeHash = FlowHashGenerator.GenerateTimeObjectHash(null, timeEnd);
            FlowAccess access = new()
            {
                AllowsTraffic = false,
                Sources = [new FlowAccessSource { NwObject = new FlowNwObject { Hash = sourceHash } }],
                Destinations = [new FlowAccessDestination { NwObject = new FlowNwObject { Hash = destinationHash } }],
                Services = [new FlowAccessService { SvcObject = new FlowSvcObject { Hash = serviceHash } }],
                TimeObjects = [new FlowAccessTimeObject { TimeObject = new FlowTimeObject { Hash = timeHash } }]
            };

            string? hash = access.TryCalculateHash();

            Assert.That(hash, Is.EqualTo(FlowHashGenerator.GenerateAccessHash([sourceHash], [destinationHash], [serviceHash], [timeHash], false)));
            Assert.That(hash, Is.Not.EqualTo(FlowHashGenerator.GenerateAccessHash([sourceHash], [destinationHash], [serviceHash], [], true)));
        }

        [Test]
        public void FlowAccess_TryCalculateHash_ReturnsNullForIncompleteAccess()
        {
            FlowAccess access = new()
            {
                Sources = [new FlowAccessSource { NwObject = new FlowNwObject { Hash = "source" } }]
            };

            string? hash = access.TryCalculateHash();

            Assert.That(hash, Is.Null);
        }

        [Test]
        public void TryBuildFlowNwObj_UsesStoredHashForNonTechnicalExistingObject()
        {
            string storedHash = "manual-hash";
            FlowNwObject existingFlowObject = new()
            {
                Id = 10,
                Hash = storedHash,
                Objects = [new NetworkObject { Id = 1 }]
            };
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [existingFlowObject]);
            Dictionary<string, FlowNwObjectInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];
            NetworkObject nonTechnical = new()
            {
                Id = 1,
                Name = "fqdn-object",
                IP = "",
                Type = new NetworkObjectType { Name = "host" }
            };

            bool result = InvokePrivateStatic<bool>("TryBuildFlowNwObj", nonTechnical, flowData, pendingInserts, mappings, false);

            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Is.Empty);
            Assert.That(mappings[storedHash].Single().FlowId, Is.EqualTo(10));
            Assert.That(mappings[storedHash].Single().FlowActive, Is.False);
        }

        [Test]
        public void TryBuildFlowSvcObj_MarksDuplicatePendingMappingsInactive()
        {
            FlowSyncFlowData flowData = CreateFlowData();
            Dictionary<string, FlowSvcObjectInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];
            NetworkService first = CreateService(1, "https-a", 6, 443, 443);
            NetworkService second = CreateService(2, "https-b", 6, 443, 443);

            bool firstResult = InvokePrivateStatic<bool>("TryBuildFlowSvcObj", first, flowData, pendingInserts, mappings, true);
            bool secondResult = InvokePrivateStatic<bool>("TryBuildFlowSvcObj", second, flowData, pendingInserts, mappings, true);

            string hash = FlowHashGenerator.GenerateSvcObjectHash(6, 443, 443);
            Assert.That(firstResult, Is.True);
            Assert.That(secondResult, Is.True);
            Assert.That(pendingInserts, Has.Count.EqualTo(1));
            Assert.That(pendingInserts[hash].Name, Is.Null);
            Assert.That(mappings[hash].Select(mapping => mapping.Id), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(mappings[hash].Last().FlowActive, Is.False);
        }

        [Test]
        public void TryBuildFlowTimeObj_CreatesInsertAndMapping()
        {
            DateTime startTime = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime endTime = new(2026, 7, 31, 23, 59, 0, DateTimeKind.Utc);
            TimeObject timeObj = new() { Id = 7, Name = "maintenance-window", StartTime = startTime, EndTime = endTime };
            FlowSyncFlowData flowData = CreateFlowData();
            Dictionary<string, FlowTimeObjectInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildFlowTimeObj", timeObj, flowData, pendingInserts, mappings, true);

            string hash = FlowHashGenerator.GenerateTimeObjectHash(startTime, endTime);
            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Has.Count.EqualTo(1));
            Assert.That(pendingInserts[hash].Name, Is.EqualTo("maintenance-window"));
            Assert.That(pendingInserts[hash].StartTime, Is.EqualTo(startTime));
            Assert.That(pendingInserts[hash].EndTime, Is.EqualTo(endTime));
            Assert.That(pendingInserts[hash].State, Is.EqualTo(FlowState.Implemented));
            Assert.That(pendingInserts[hash].ShowInRequestModule, Is.True);
            Assert.That(mappings[hash].Single().Id, Is.EqualTo(7));
            Assert.That(mappings[hash].Single().FlowId, Is.Null);
            Assert.That(mappings[hash].Single().FlowActive, Is.True);
        }

        [Test]
        public void TryBuildFlowTimeObj_UsesStoredHashForExistingNonTechnicalTimeObject()
        {
            TimeObject timeObj = new() { Id = 7, Name = "vendor-calendar" };
            FlowTimeObject existingFlowObject = new()
            {
                Id = 30,
                Hash = "stored-time-hash",
                TimeObjects = [timeObj]
            };
            FlowSyncFlowData flowData = CreateFlowData(timeObjects: [existingFlowObject]);
            Dictionary<string, FlowTimeObjectInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildFlowTimeObj", timeObj, flowData, pendingInserts, mappings, false);

            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Is.Empty);
            Assert.That(mappings["stored-time-hash"].Single().FlowId, Is.EqualTo(30));
            Assert.That(mappings["stored-time-hash"].Single().FlowActive, Is.False);
        }

        [Test]
        public void TryBuildFlowTimeObj_ReturnsFalseForUnmappedTimeObjectWithoutBounds()
        {
            TimeObject timeObj = new() { Id = 7, Name = "vendor-calendar" };
            FlowSyncFlowData flowData = CreateFlowData();
            Dictionary<string, FlowTimeObjectInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildFlowTimeObj", timeObj, flowData, pendingInserts, mappings, true);

            Assert.That(result, Is.False);
            Assert.That(pendingInserts, Is.Empty);
            Assert.That(mappings, Is.Empty);
        }

        [Test]
        public void TryBuildFlowTimeObj_MarksDuplicatePendingMappingsInactive()
        {
            DateTime endTime = new(2026, 7, 31, 23, 59, 0, DateTimeKind.Utc);
            TimeObject first = new() { Id = 7, Name = "first", EndTime = endTime };
            TimeObject second = new() { Id = 8, Name = "second", EndTime = endTime };
            FlowSyncFlowData flowData = CreateFlowData();
            Dictionary<string, FlowTimeObjectInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool firstResult = InvokePrivateStatic<bool>("TryBuildFlowTimeObj", first, flowData, pendingInserts, mappings, true);
            bool secondResult = InvokePrivateStatic<bool>("TryBuildFlowTimeObj", second, flowData, pendingInserts, mappings, true);

            string hash = FlowHashGenerator.GenerateTimeObjectHash(null, endTime);
            Assert.That(firstResult, Is.True);
            Assert.That(secondResult, Is.True);
            Assert.That(pendingInserts, Has.Count.EqualTo(1));
            Assert.That(pendingInserts[hash].Name, Is.Null);
            Assert.That(mappings[hash].Select(mapping => mapping.Id), Is.EqualTo(new long[] { 7, 8 }));
            Assert.That(mappings[hash].Last().FlowActive, Is.False);
        }

        [Test]
        public void TryBuildNwGroup_CreatesInsertForTechnicalMembers()
        {
            NetworkObject first = CreateNetworkObject(1, "src-a", "10.0.0.1", "10.0.0.1");
            NetworkObject second = CreateNetworkObject(2, "src-b", "10.0.0.2", "10.0.0.2");
            FlowNwObject firstFlowObject = CreateFlowNwObject(101, first);
            FlowNwObject secondFlowObject = CreateFlowNwObject(102, second);
            NetworkObject group = CreateNetworkGroup(10, "source-group", first, second);
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [firstFlowObject, secondFlowObject]);
            Dictionary<string, FlowNwGroupInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildNwGroup", group, flowData, pendingInserts, mappings, true);

            string hash = FlowHashGenerator.GenerateGroupHash([firstFlowObject.Hash, secondFlowObject.Hash]);
            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Has.Count.EqualTo(1));
            Assert.That(pendingInserts[hash].Name, Is.EqualTo("source-group"));
            Assert.That(pendingInserts[hash].NwGroupMembers!.Data.Select(member => member.NwObjId), Is.EquivalentTo(new long[] { 101, 102 }));
            Assert.That(mappings[hash].Single().Id, Is.EqualTo(10));
            Assert.That(mappings[hash].Single().FlowActive, Is.True);
        }

        [Test]
        public void TryBuildNwGroup_ReturnsFalseForNonTechnicalMemberWithoutStoredHash()
        {
            NetworkObject nonTechnical = new()
            {
                Id = 1,
                Name = "fqdn-source",
                IP = "",
                Type = new NetworkObjectType { Name = "host" }
            };
            NetworkObject group = CreateNetworkGroup(10, "source-group", nonTechnical);
            FlowSyncFlowData flowData = CreateFlowData();
            Dictionary<string, FlowNwGroupInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildNwGroup", group, flowData, pendingInserts, mappings, true);

            Assert.That(result, Is.False);
            Assert.That(pendingInserts, Is.Empty);
            Assert.That(mappings, Is.Empty);
        }

        [Test]
        public void TryBuildSvcGroup_CreatesInsertForTechnicalMembers()
        {
            NetworkService first = CreateService(1, "https", 6, 443, 443);
            NetworkService second = CreateService(2, "http-alt", 6, 8080, 8080);
            FlowSvcObject firstFlowObject = CreateFlowSvcObject(201, first);
            FlowSvcObject secondFlowObject = CreateFlowSvcObject(202, second);
            NetworkService group = CreateServiceGroup(10, "web-services", first, second);
            FlowSyncFlowData flowData = CreateFlowData(svcObjects: [firstFlowObject, secondFlowObject]);
            Dictionary<string, FlowSvcGroupInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildSvcGroup", group, flowData, pendingInserts, mappings, true);

            string hash = FlowHashGenerator.GenerateGroupHash([firstFlowObject.Hash, secondFlowObject.Hash]);
            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Has.Count.EqualTo(1));
            Assert.That(pendingInserts[hash].Name, Is.EqualTo("web-services"));
            Assert.That(pendingInserts[hash].SvcGroupMembers!.Data.Select(member => member.SvcObjId), Is.EquivalentTo(new long[] { 201, 202 }));
            Assert.That(mappings[hash].Single().Id, Is.EqualTo(10));
            Assert.That(mappings[hash].Single().FlowActive, Is.True);
        }

        [Test]
        public void TryBuildSvcGroup_ReturnsFalseForEmptyGroup()
        {
            NetworkService group = CreateServiceGroup(10, "empty-services");
            FlowSyncFlowData flowData = CreateFlowData();
            Dictionary<string, FlowSvcGroupInsert> pendingInserts = [];
            Dictionary<string, List<FlowMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildSvcGroup", group, flowData, pendingInserts, mappings, true);

            Assert.That(result, Is.False);
            Assert.That(pendingInserts, Is.Empty);
            Assert.That(mappings, Is.Empty);
        }

        [Test]
        public void TryBuildRuleAccess_IncludesTimeObjectsInAccessInsert()
        {
            NetworkObject source = CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1");
            NetworkObject destination = CreateNetworkObject(2, "dst", "10.0.1.1", "10.0.1.1");
            NetworkService service = CreateService(3, "https", 6, 443, 443);
            DateTime endTime = new(2026, 7, 31, 23, 59, 0, DateTimeKind.Utc);
            TimeObject timeObj = new() { Id = 4, Name = "july", EndTime = endTime };
            FlowNwObject sourceFlowObject = CreateFlowNwObject(101, source);
            FlowNwObject destinationFlowObject = CreateFlowNwObject(102, destination);
            FlowSvcObject serviceFlowObject = CreateFlowSvcObject(201, service);
            FlowTimeObject timeFlowObject = CreateFlowTimeObject(301, timeObj);
            FlowSyncFlowData flowData = CreateFlowData(
                nwObjects: [sourceFlowObject, destinationFlowObject],
                svcObjects: [serviceFlowObject],
                timeObjects: [timeFlowObject]);
            Rule rule = new()
            {
                Id = 99,
                OwnerId = 5,
                Froms = [new NetworkLocation(new NetworkUser(), source)],
                Tos = [new NetworkLocation(new NetworkUser(), destination)],
                Services = [new ServiceWrapper { Content = service }],
                RuleTimes = [new RuleTime { Id = 8, TimeObjId = timeObj.Id, TimeObj = timeObj }]
            };
            Dictionary<string, FlowAccessInsert> pendingInserts = [];
            Dictionary<string, List<FlowRuleMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildRuleAccess", rule, flowData, pendingInserts, mappings);

            string accessHash = FlowHashGenerator.GenerateAccessHash([sourceFlowObject.Hash], [destinationFlowObject.Hash], [serviceFlowObject.Hash], [timeFlowObject.Hash], true);
            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Has.Count.EqualTo(1));
            Assert.That(pendingInserts[accessHash].OwnerId, Is.EqualTo(5));
            Assert.That(pendingInserts[accessHash].AllowsTraffic, Is.True);
            Assert.That(pendingInserts[accessHash].AccessSources!.Data.OfType<NwRef>().Single().NwObjId, Is.EqualTo(101));
            Assert.That(pendingInserts[accessHash].AccessDestinations!.Data.OfType<NwRef>().Single().NwObjId, Is.EqualTo(102));
            Assert.That(pendingInserts[accessHash].AccessServices!.Data.OfType<SvcRef>().Single().SvcObjId, Is.EqualTo(201));
            Assert.That(pendingInserts[accessHash].AccessTimeObjects!.Data.OfType<TimeRef>().Single().TimeObjId, Is.EqualTo(301));
            Assert.That(mappings[accessHash].Single().Id, Is.EqualTo(99));
        }

        [Test]
        public void TryBuildRuleAccess_ReusesExistingAccessWithoutInsert()
        {
            NetworkObject source = CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1");
            NetworkObject destination = CreateNetworkObject(2, "dst", "10.0.1.1", "10.0.1.1");
            NetworkService service = CreateService(3, "https", 6, 443, 443);
            FlowNwObject sourceFlowObject = CreateFlowNwObject(101, source);
            FlowNwObject destinationFlowObject = CreateFlowNwObject(102, destination);
            FlowSvcObject serviceFlowObject = CreateFlowSvcObject(201, service);
            string accessHash = FlowHashGenerator.GenerateAccessHash([sourceFlowObject.Hash], [destinationFlowObject.Hash], [serviceFlowObject.Hash], [], true);
            FlowAccess existingAccess = new() { Id = 401, Hash = accessHash };
            FlowSyncFlowData flowData = CreateFlowData(
                nwObjects: [sourceFlowObject, destinationFlowObject],
                svcObjects: [serviceFlowObject],
                accesses: [existingAccess]);
            Rule rule = CreateRule(99, source, destination, service);
            Dictionary<string, FlowAccessInsert> pendingInserts = [];
            Dictionary<string, List<FlowRuleMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildRuleAccess", rule, flowData, pendingInserts, mappings);

            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Is.Empty);
            Assert.That(mappings[accessHash].Single().FlowId, Is.EqualTo(401));
        }

        [Test]
        public void TryBuildRuleAccess_ExpandsNetworkAndServiceGroups()
        {
            NetworkObject source = CreateNetworkObject(1, "src", "10.0.0.1", "10.0.0.1");
            NetworkObject destination = CreateNetworkObject(2, "dst", "10.0.1.1", "10.0.1.1");
            NetworkService service = CreateService(3, "https", 6, 443, 443);
            NetworkObject sourceGroup = CreateNetworkGroup(10, "source-group", source);
            NetworkService serviceGroup = CreateServiceGroup(20, "service-group", service);
            FlowNwObject sourceFlowObject = CreateFlowNwObject(101, source);
            FlowNwObject destinationFlowObject = CreateFlowNwObject(102, destination);
            FlowSvcObject serviceFlowObject = CreateFlowSvcObject(201, service);
            FlowNwGroup sourceFlowGroup = CreateFlowNwGroup(301, sourceGroup, sourceFlowObject);
            FlowSvcGroup serviceFlowGroup = CreateFlowSvcGroup(401, serviceGroup, serviceFlowObject);
            FlowSyncFlowData flowData = CreateFlowData(
                nwObjects: [sourceFlowObject, destinationFlowObject],
                nwGroups: [sourceFlowGroup],
                svcObjects: [serviceFlowObject],
                svcGroups: [serviceFlowGroup]);
            Rule rule = CreateRule(99, sourceGroup, destination, serviceGroup);
            Dictionary<string, FlowAccessInsert> pendingInserts = [];
            Dictionary<string, List<FlowRuleMappingUpdate>> mappings = [];

            bool result = InvokePrivateStatic<bool>("TryBuildRuleAccess", rule, flowData, pendingInserts, mappings);

            string accessHash = FlowHashGenerator.GenerateAccessHash([sourceFlowObject.Hash], [destinationFlowObject.Hash], [serviceFlowObject.Hash], [], true);
            Assert.That(result, Is.True);
            Assert.That(pendingInserts, Has.Count.EqualTo(1));
            Assert.That(pendingInserts[accessHash].AccessSources!.Data.OfType<NwRef>().Single().NwObjId, Is.EqualTo(101));
            Assert.That(pendingInserts[accessHash].AccessSourceGroups!.Data.OfType<NwGroupRef>().Single().NwGroupId, Is.EqualTo(301));
            Assert.That(pendingInserts[accessHash].AccessServices!.Data.OfType<SvcRef>().Single().SvcObjId, Is.EqualTo(201));
            Assert.That(pendingInserts[accessHash].AccessServiceGroups!.Data.OfType<SvcGroupRef>().Single().SvcGroupId, Is.EqualTo(401));
            Assert.That(mappings[accessHash].Single().Id, Is.EqualTo(99));
        }

        [Test]
        public async Task Run_UsesSavedRankingToChooseNamingSourceManagement()
        {
            string sharedHash = FlowHashGenerator.GenerateNwObjectHash("10.0.0.1", "10.0.0.1");
            NetworkObject firstManagementObject = CreateNetworkObject(1, "first-name", "10.0.0.1", "10.0.0.1");
            NetworkObject secondManagementObject = CreateNetworkObject(2, "second-name", "10.0.0.1", "10.0.0.1");
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports =
                [
                    new ImportControl { ControlId = 10, MgmId = 1 },
                    new ImportControl { ControlId = 20, MgmId = 2 }
                ],
                ManagementDataById =
                {
                    [1] = new FlowSyncManagementData { Id = 1, NetworkObjects = [firstManagementObject] },
                    [2] = new FlowSyncManagementData { Id = 2, NetworkObjects = [secondManagementObject] }
                }
            };
            FlowSync flowSync = new(apiConn, new GlobalConfig { FlowNamingSourceManagementRanking = "[2,1]" });

            bool result = await flowSync.Run();

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkObjects, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedNetworkObjects[0].NwObjHash, Is.EqualTo(sharedHash));
            Assert.That(apiConn.InsertedNetworkObjects[0].Name, Is.EqualTo("second-name"));
        }

        [Test]
        public async Task Run_DoesNotNameImportedObjectsWhenNoRankingIsSaved()
        {
            FlowSyncTestApiConn apiConn = new()
            {
                PendingImports =
                [
                    new ImportControl { ControlId = 10, MgmId = 1 }
                ],
                ManagementDataById =
                {
                    [1] = new FlowSyncManagementData
                    {
                        Id = 1,
                        NetworkObjects = [CreateNetworkObject(1, "unnamed-source", "10.0.0.1", "10.0.0.1")]
                    }
                }
            };
            FlowSync flowSync = new(apiConn, new GlobalConfig { FlowNamingSourceManagementRanking = "[]" });

            bool result = await flowSync.Run();

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkObjects, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedNetworkObjects[0].Name, Is.Null);
        }

        private static T InvokePrivateStatic<T>(string methodName, params object[] parameters)
        {
            MethodInfo method = typeof(FlowSync).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(FlowSync).FullName, methodName);
            return (T)method.Invoke(null, parameters)!;
        }

        private static FlowSyncFlowData CreateFlowData(List<FlowNwObject>? nwObjects = null, List<FlowNwGroup>? nwGroups = null,
            List<FlowSvcObject>? svcObjects = null, List<FlowSvcGroup>? svcGroups = null, List<FlowTimeObject>? timeObjects = null,
            List<FlowAccess>? accesses = null, List<RuleAction>? ruleActions = null)
        {
            return new(new FlowSyncFlowDataInput
            {
                NwObjects = nwObjects ?? [],
                NwGroups = nwGroups ?? [],
                SvcObjects = svcObjects ?? [],
                SvcGroups = svcGroups ?? [],
                TimeObjects = timeObjects ?? [],
                Accesses = accesses ?? [],
                RuleActions = ruleActions
            });
        }

        private static NetworkObject CreateNetworkObject(long id, string name, string ip, string ipEnd)
        {
            return new()
            {
                Id = id,
                Name = name,
                IP = ip,
                IpEnd = ipEnd,
                Type = new NetworkObjectType { Name = "host" }
            };
        }

        private static NetworkObject CreateNetworkGroup(long id, string name, params NetworkObject[] members)
        {
            return new()
            {
                Id = id,
                Name = name,
                Type = new NetworkObjectType { Name = ObjectType.Group },
                ObjectGroupFlats = [.. members.Select(member => new GroupFlat<NetworkObject> { Id = member.Id, Object = member })]
            };
        }

        private static NetworkService CreateService(long id, string name, int protoId, int port, int portEnd)
        {
            return new()
            {
                Id = id,
                Name = name,
                ProtoId = protoId,
                DestinationPort = port,
                DestinationPortEnd = portEnd,
                Type = new NetworkServiceType { Name = "tcp" }
            };
        }

        private static NetworkService CreateServiceGroup(long id, string name, params NetworkService[] members)
        {
            return new()
            {
                Id = id,
                Name = name,
                Type = new NetworkServiceType { Name = ServiceType.Group },
                ServiceGroupFlats = [.. members.Select(member => new GroupFlat<NetworkService> { Id = member.Id, Object = member })]
            };
        }

        private static Rule CreateRule(long id, NetworkObject source, NetworkObject destination, NetworkService service)
        {
            return new()
            {
                Id = id,
                OwnerId = 5,
                Froms = [new NetworkLocation(new NetworkUser(), source)],
                Tos = [new NetworkLocation(new NetworkUser(), destination)],
                Services = [new ServiceWrapper { Content = service }]
            };
        }

        private static FlowNwObject CreateFlowNwObject(long id, NetworkObject nwObject)
        {
            string hash = FlowHashGenerator.GenerateNwObjectHash(nwObject.IP, nwObject.IpEnd);
            return new()
            {
                Id = id,
                Name = nwObject.Name,
                IpStart = nwObject.IP,
                IpEnd = nwObject.IpEnd,
                Hash = hash,
                Objects = [nwObject]
            };
        }

        private static FlowNwGroup CreateFlowNwGroup(long id, NetworkObject group, params FlowNwObject[] members)
        {
            string hash = FlowHashGenerator.GenerateGroupHash(members.Select(member => member.Hash));
            return new()
            {
                Id = id,
                Name = group.Name,
                Hash = hash,
                NwGroupMembers = [.. members.Select(member => new FlowNwGroupMember { NwGroupId = id, NwObjectId = member.Id })],
                Objects = [group]
            };
        }

        private static FlowSvcObject CreateFlowSvcObject(long id, NetworkService service)
        {
            int protoId = service.ProtoId ?? throw new ArgumentException("Service protocol is required.", nameof(service));
            int portStart = service.DestinationPort ?? throw new ArgumentException("Service destination port is required.", nameof(service));
            int portEnd = service.DestinationPortEnd ?? throw new ArgumentException("Service destination port end is required.", nameof(service));
            string hash = FlowHashGenerator.GenerateSvcObjectHash(protoId, portStart, portEnd);
            return new()
            {
                Id = id,
                Name = service.Name,
                ProtoId = protoId,
                PortStart = portStart,
                PortEnd = portEnd,
                Hash = hash,
                Services = [service]
            };
        }

        private static FlowSvcGroup CreateFlowSvcGroup(long id, NetworkService group, params FlowSvcObject[] members)
        {
            string hash = FlowHashGenerator.GenerateGroupHash(members.Select(member => member.Hash));
            return new()
            {
                Id = id,
                Name = group.Name,
                Hash = hash,
                SvcGroupMembers = [.. members.Select(member => new FlowSvcGroupMember { SvcGroupId = id, SvcObjectId = member.Id })],
                Services = [group]
            };
        }

        private static FlowTimeObject CreateFlowTimeObject(long id, TimeObject timeObj)
        {
            string hash = FlowHashGenerator.GenerateTimeObjectHash(timeObj.StartTime, timeObj.EndTime);
            return new()
            {
                Id = id,
                Name = timeObj.Name,
                StartTime = timeObj.StartTime,
                EndTime = timeObj.EndTime,
                Hash = hash,
                TimeObjects = [timeObj]
            };
        }

        private static TValue GetVariable<TValue>(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            return property != null ? (TValue)property.GetValue(variables)! : default!;
        }
    }
}
