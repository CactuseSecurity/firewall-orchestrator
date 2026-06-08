using FWO.Api.Client;
using FWO.Api.Client.Queries;
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
            private long nextFlowServiceObjectId = 200;
            private long nextFlowAccessId = 300;

            public List<FlowNwObject> FlowNetworkObjects { get; } = [];
            public List<FlowSvcObject> FlowServiceObjects { get; } = [];
            public List<FlowAccess> FlowAccesses { get; } = [];
            public List<FlowNwObjectInsert> InsertedNetworkObjects { get; } = [];
            public List<FlowSvcObjectInsert> InsertedServiceObjects { get; } = [];
            public List<FlowAccessInsert> InsertedAccesses { get; } = [];
            public List<object> NetworkObjectMappingUpdates { get; } = [];
            public List<object> ServiceObjectMappingUpdates { get; } = [];
            public List<object> RuleMappingUpdates { get; } = [];
            public bool RemovedMappingsCleared { get; private set; }
            public long? CompletedImportControlId { get; private set; }

            public FlowSyncManagementData ManagementData { get; set; } = new();
            public Dictionary<int, FlowSyncManagementData> ManagementDataById { get; set; } = [];
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
                    return Task.FromResult((T)(object)new List<FlowNwGroup>());
                }
                if (query == FlowQueries.getFlowSyncSvcObjects)
                {
                    return Task.FromResult((T)(object)FlowServiceObjects);
                }
                if (query == FlowQueries.getFlowSyncSvcGroups)
                {
                    return Task.FromResult((T)(object)new List<FlowSvcGroup>());
                }
                if (query == FlowQueries.getFlowSyncTimeObjects)
                {
                    return Task.FromResult((T)(object)new List<FlowTimeObject>());
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
                        RemovedDate = insert.RemovedDate
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
            Assert.That(apiConn.NetworkObjectMappingUpdates, Has.Count.EqualTo(2));
            Assert.That(apiConn.ServiceObjectMappingUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConn.RuleMappingUpdates, Has.Count.EqualTo(1));
            Assert.That(apiConn.RemovedMappingsCleared, Is.True);
            Assert.That(apiConn.CompletedImportControlId, Is.EqualTo(9));
        }

        [Test]
        public void FlowSyncFlowData_IndexesExistingFlowObjectsAndReverseMappings()
        {
            string nwHash = FlowHashGenerator.GenerateNwObjectHash("10.0.0.1", "10.0.0.1");
            string svcHash = FlowHashGenerator.GenerateSvcObjectHash(6, 443, 443);
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

            FlowSyncFlowData flowData = new([nwObject], [], [svcObject], [], [], []);

            Assert.That(flowData.NwObjects[nwHash].Id, Is.EqualTo(10));
            Assert.That(flowData.NwObjectsById[10].Hash, Is.EqualTo(nwHash));
            Assert.That(flowData.NwObjectHashes[1], Is.EqualTo(nwHash));
            Assert.That(flowData.SvcObjects[svcHash].Id, Is.EqualTo(20));
            Assert.That(flowData.SvcObjectsById[20].Hash, Is.EqualTo(svcHash));
            Assert.That(flowData.SvcObjectHashes[2], Is.EqualTo(svcHash));
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
            FlowSyncFlowData flowData = new([existingFlowObject], [], [], [], [], []);
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
            FlowSyncFlowData flowData = new([], [], [], [], [], []);
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

        private static TValue GetVariable<TValue>(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            return property != null ? (TValue)property.GetValue(variables)! : default!;
        }
    }
}
