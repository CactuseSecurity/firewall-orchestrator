using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowDbCreatorTest
    {
        private sealed class FlowDbCreatorTestApiConn : SimulatedApiConnection
        {
            private long nextNetworkObjectId = 100;
            private long nextNetworkGroupId = 400;
            private long nextServiceObjectId = 200;
            private const long InsertedAccessId = 300;

            public List<FlowNwObject> ExistingNetworkObjects { get; } = [];
            public List<FlowNwGroup> ExistingNetworkGroups { get; } = [];
            public List<FlowSvcObject> ExistingServiceObjects { get; } = [];
            public List<FlowSvcGroup> ExistingServiceGroups { get; } = [];
            public List<FlowAccess> ExistingAccesses { get; } = [];
            public Dictionary<int, List<FlowNwObject>> ExistingNetworkObjectsByManagement { get; } = [];
            public Dictionary<int, List<FlowNwGroup>> ExistingNetworkGroupsByManagement { get; } = [];
            public Dictionary<int, List<FlowSvcObject>> ExistingServiceObjectsByManagement { get; } = [];
            public Dictionary<int, List<FlowSvcGroup>> ExistingServiceGroupsByManagement { get; } = [];
            public Dictionary<int, List<FlowAccess>> ExistingAccessesByManagement { get; } = [];
            public List<int> QueriedManagementIds { get; } = [];
            public List<long> UpdatedRequestTaskIds { get; } = [];
            public List<RequestElementFlowUpdate> UpdatedRequestElements { get; } = [];
            public FlowAccessInsert? InsertedAccess { get; private set; }
            public List<FlowAccessInsert> InsertedAccesses { get; } = [];
            public int InsertedAccessCount { get; private set; }
            public List<FlowNwObject> InsertedNetworkObjects { get; } = [];
            public List<FlowNwGroup> InsertedNetworkGroups { get; } = [];
            public List<FlowSvcObject> InsertedServiceObjects { get; } = [];
            public List<FlowSvcGroup> InsertedServiceGroups { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null)
            {
                if (query == FlowQueries.getFlowSyncNwObjects)
                {
                    int mgmId = GetValue<int>(variables, "mgmId");
                    QueriedManagementIds.Add(mgmId);
                    return Task.FromResult((T)(object)GetManagementData(ExistingNetworkObjectsByManagement, ExistingNetworkObjects, mgmId));
                }
                if (query == FlowQueries.getFlowSyncNwGroups)
                {
                    int mgmId = GetValue<int>(variables, "mgmId");
                    QueriedManagementIds.Add(mgmId);
                    return Task.FromResult((T)(object)GetManagementData(ExistingNetworkGroupsByManagement, ExistingNetworkGroups, mgmId));
                }
                if (query == FlowQueries.getFlowSyncSvcObjects)
                {
                    int mgmId = GetValue<int>(variables, "mgmId");
                    QueriedManagementIds.Add(mgmId);
                    return Task.FromResult((T)(object)GetManagementData(ExistingServiceObjectsByManagement, ExistingServiceObjects, mgmId));
                }
                if (query == FlowQueries.getFlowSyncSvcGroups)
                {
                    int mgmId = GetValue<int>(variables, "mgmId");
                    QueriedManagementIds.Add(mgmId);
                    return Task.FromResult((T)(object)GetManagementData(ExistingServiceGroupsByManagement, ExistingServiceGroups, mgmId));
                }
                if (query == FlowQueries.getFlowSyncAccesses)
                {
                    int mgmId = GetValue<int>(variables, "mgmId");
                    QueriedManagementIds.Add(mgmId);
                    return Task.FromResult((T)(object)GetManagementData(ExistingAccessesByManagement, ExistingAccesses, mgmId));
                }
                if (query == FlowQueries.insertFlowNwObjects)
                {
                    FlowNwObjectInsert insert = GetObjects<FlowNwObjectInsert>(variables).Single();
                    FlowNwObject inserted = new()
                    {
                        Id = ++nextNetworkObjectId,
                        Name = insert.Name,
                        IpStart = insert.IpStart,
                        IpEnd = insert.IpEnd,
                        Hash = insert.NwObjHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule
                    };
                    InsertedNetworkObjects.Add(inserted);
                    return Task.FromResult((T)(object)new FlowNwObjectInsertResult { Returning = [inserted] });
                }
                if (query == FlowQueries.insertFlowNwGroups)
                {
                    FlowNwGroupInsert insert = GetObjects<FlowNwGroupInsert>(variables).Single();
                    long groupId = ++nextNetworkGroupId;
                    FlowNwGroup inserted = new()
                    {
                        Id = groupId,
                        Name = insert.Name ?? "",
                        Hash = insert.NwGrpHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule,
                        NwGroupMembers = insert.NwGroupMembers?.Data.Select(member => new FlowNwGroupMember
                        {
                            NwGroupId = groupId,
                            NwObjectId = member.NwObjId
                        }).ToList() ?? []
                    };
                    InsertedNetworkGroups.Add(inserted);
                    return Task.FromResult((T)(object)new FlowNwGroupInsertResult { Returning = [inserted] });
                }
                if (query == FlowQueries.insertFlowSvcObjects)
                {
                    FlowSvcObjectInsert insert = GetObjects<FlowSvcObjectInsert>(variables).Single();
                    FlowSvcObject inserted = new()
                    {
                        Id = ++nextServiceObjectId,
                        Name = insert.Name ?? "",
                        PortStart = insert.PortStart,
                        PortEnd = insert.PortEnd,
                        ProtoId = insert.IpProtoId,
                        Hash = insert.SvcObjHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule
                    };
                    InsertedServiceObjects.Add(inserted);
                    return Task.FromResult((T)(object)new FlowSvcObjectInsertResult { Returning = [inserted] });
                }
                if (query == FlowQueries.insertFlowSvcGroups)
                {
                    FlowSvcGroupInsert insert = GetObjects<FlowSvcGroupInsert>(variables).Single();
                    FlowSvcGroup inserted = new()
                    {
                        Id = ++nextServiceObjectId,
                        Name = insert.Name ?? "",
                        Hash = insert.SvcGrpHash ?? "",
                        State = insert.State ?? "",
                        RemovedDate = insert.RemovedDate,
                        ShowInRequestModule = insert.ShowInRequestModule,
                        SvcGroupMembers = insert.SvcGroupMembers?.Data.Select(member => new FlowSvcGroupMember
                        {
                            SvcGroupId = nextServiceObjectId,
                            SvcObjectId = member.SvcObjId
                        }).ToList() ?? []
                    };
                    InsertedServiceGroups.Add(inserted);
                    return Task.FromResult((T)(object)new FlowSvcGroupInsertResult { Returning = [inserted] });
                }
                if (query == FlowQueries.insertFlowAccesses)
                {
                    InsertedAccessCount++;
                    InsertedAccess = GetObjects<FlowAccessInsert>(variables).Single();
                    InsertedAccesses.Add(InsertedAccess);
                    FlowAccess inserted = new()
                    {
                        Id = InsertedAccessId,
                        Hash = InsertedAccess.AccessHash ?? "",
                        OwnerId = InsertedAccess.OwnerId,
                        State = InsertedAccess.State ?? ""
                    };
                    return Task.FromResult((T)(object)new FlowAccessInsertResult { Returning = [inserted] });
                }
                if (query == RequestQueries.updateRequestTaskFlowId)
                {
                    UpdatedRequestTaskIds.Add(GetValue<long>(variables, "id"));
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = GetValue<long>(variables, "id") });
                }
                if (query == RequestQueries.updateRequestElementFlowIds)
                {
                    UpdatedRequestElements.Add(new RequestElementFlowUpdate
                    {
                        Id = GetValue<long>(variables, "id"),
                        FlowNetworkObjectId = GetValue<long?>(variables, "flowNwObjId"),
                        FlowNetworkGroupId = GetValue<long?>(variables, "flowNwGrpId"),
                        FlowServiceObjectId = GetValue<long?>(variables, "flowSvcObjId"),
                        FlowServiceGroupId = GetValue<long?>(variables, "flowSvcGrpId")
                    });
                    return Task.FromResult((T)(object)new ReturnId { UpdatedIdLong = GetValue<long>(variables, "id") });
                }

                throw new AssertionException($"Unexpected query: {query}");
            }

            private static List<TObject> GetManagementData<TObject>(Dictionary<int, List<TObject>> dataByManagement, List<TObject> fallbackData, int managementId)
            {
                return dataByManagement.TryGetValue(managementId, out List<TObject>? managementData) ? managementData : fallbackData;
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

            private static TValue GetValue<TValue>(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value == null ? default! : (TValue)value;
            }
        }

        [Test]
        public void BuildFlowCreationPayloads_CreatesPayloadForEachTicketTask()
        {
            WfTicket ticket = new()
            {
                Id = 7,
                Tasks =
                [
                    CreateAccessTask(1, "10.0.0.1", "10.0.1.1", 443),
                    CreateAccessTask(2, "10.0.0.2", "10.0.1.2", 8443),
                    new WfReqTask { Id = 3, TicketId = 7, TaskType = WfTaskType.new_interface.ToString() }
                ]
            };

            List<FlowCreationPayload> payloads = FlowDbCreator.BuildFlowCreationPayloads(ticket, WfObjectScopes.Ticket, null, null);

            Assert.That(payloads, Has.Count.EqualTo(2));
            Assert.That(payloads.Select(payload => payload.TicketId), Is.All.EqualTo(7));
            Assert.That(payloads.SelectMany(payload => payload.OriginRequestTaskIds), Is.EquivalentTo(new long[] { 1, 2 }));
            Assert.That(payloads[0].Sources, Has.Count.EqualTo(1));
            Assert.That(payloads[0].Destinations, Has.Count.EqualTo(1));
            Assert.That(payloads[0].Services, Has.Count.EqualTo(1));
            Assert.That(payloads[0].OwnerId, Is.EqualTo(5));
        }

        [Test]
        public async Task CreateFlowInFlowDb_IgnoresNonFlowTicketTasks()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask accessTask = CreateAccessTask(21, "10.0.0.1", "10.0.1.1", 443);
            WfTicket ticket = new()
            {
                Id = 7,
                Tasks =
                [
                    new WfReqTask { Id = 20, TicketId = 7, TaskType = WfTaskType.new_interface.ToString() },
                    accessTask
                ]
            };

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.That(result, Is.True);
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.EqualTo(new List<long> { 21 }));
        }

        [Test]
        public void BuildFlowCreationPayloads_UsesRequestTaskOverrides()
        {
            WfReqTask task = CreateAccessTask(11, "10.0.0.1", "10.0.1.1", 443);

            List<FlowCreationPayload> payloads = FlowDbCreator.BuildFlowCreationPayloads(task, WfObjectScopes.RequestTask, new FwoOwner { Id = 99 }, 77);

            Assert.That(payloads, Has.Count.EqualTo(1));
            Assert.That(payloads[0].TicketId, Is.EqualTo(77));
            Assert.That(payloads[0].OwnerId, Is.EqualTo(99));
            Assert.That(payloads[0].OriginRequestTaskIds, Is.EqualTo(new List<long> { 11 }));
        }

        [Test]
        public async Task CreateFlowInFlowDb_ReturnsFalseForUnsupportedScope()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, new WfTicket(), WfObjectScopes.ImplementationTask, null, null);

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedAccess, Is.Null);
        }

        [Test]
        public async Task CreateFlowInFlowDb_PersistsAccessAndUpdatesRequestFlowIds()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask task = CreateAccessTask(11, "10.0.0.1", "10.0.1.1", 443);

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, task, WfObjectScopes.RequestTask, null, task.TicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedAccess, Is.Not.Null);
            FlowAccessInsert insertedAccess = apiConn.InsertedAccess!;
            Assert.That(insertedAccess.OwnerId, Is.EqualTo(5));
            Assert.That(insertedAccess.State, Is.EqualTo(FlowState.Requested));
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.EqualTo(new List<long> { 11 }));
            Assert.That(apiConn.UpdatedRequestElements.Select(update => update.Id), Is.EquivalentTo(new long[] { 111, 112, 113 }));
            Assert.That(apiConn.UpdatedRequestElements.Single(update => update.Id == 111).FlowNetworkObjectId, Is.EqualTo(101));
            Assert.That(apiConn.UpdatedRequestElements.Single(update => update.Id == 112).FlowNetworkObjectId, Is.EqualTo(102));
            Assert.That(apiConn.UpdatedRequestElements.Single(update => update.Id == 113).FlowServiceObjectId, Is.EqualTo(201));
        }

        [Test]
        public async Task CreateFlowInFlowDb_UpdatesAllBundledRequestTasks()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask first = CreateAccessTask(11, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateAccessTask(12, "10.0.0.1", "10.0.1.2", 443);
            first.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-11-12");
            second.SetAddInfo(AdditionalInfoKeys.FlowBundleId, "bundle-11-12");
            WfTicket ticket = new()
            {
                Id = 7,
                Tasks = [first, second]
            };

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.That(result, Is.True);
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.EquivalentTo(new long[] { 11, 12 }));
            Assert.That(apiConn.UpdatedRequestElements.Select(update => update.Id), Is.EquivalentTo(new long[] { 111, 112, 113, 121, 122, 123 }));
            FlowAccessInsert insertedAccess = apiConn.InsertedAccess!;
            Assert.That(insertedAccess.AccessSources!.Data, Has.Count.EqualTo(1));
            Assert.That(insertedAccess.AccessDestinations!.Data, Has.Count.EqualTo(2));
            Assert.That(insertedAccess.AccessServices!.Data, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task CreateFlowInFlowDb_DoesNotResolveConcreteNetworkElementAsGroupReference()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateNetworkGroupTask(20, "AR-Test", "10.0.0.1");
            WfReqTask accessTask = CreateAccessTask(21, "10.0.0.1", "10.0.1.1", 443);
            accessTask.Elements.Single(element => element.Field == ElemFieldType.source.ToString()).GroupName = "AR-Test";
            WfTicket ticket = new()
            {
                Id = 7,
                Tasks = [groupTask, accessTask]
            };

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkGroups, Has.Count.EqualTo(1));
            Assert.That(apiConn.UpdatedRequestElements.Select(update => update.Id), Is.EquivalentTo(new long[] { 201, 211, 212, 213 }));
            RequestElementFlowUpdate groupMemberUpdate = apiConn.UpdatedRequestElements.Single(update => update.Id == 201);
            Assert.That(groupMemberUpdate.FlowNetworkObjectId, Is.EqualTo(101));
            Assert.That(groupMemberUpdate.FlowNetworkGroupId, Is.EqualTo(401));
            Assert.That(apiConn.InsertedAccess!.AccessSources!.Data, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccess.AccessSourceGroups!.Data, Is.Empty);
        }

        [Test]
        public async Task CreateFlowInFlowDb_UsesCreatedGroupAsAccessReference()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateNetworkGroupTask(20, "AR-Test", "10.0.0.1");
            WfReqTask accessTask = CreateAccessTask(21, "10.0.0.2", "10.0.1.1", 443);
            WfReqElement source = accessTask.Elements.Single(element => element.Field == ElemFieldType.source.ToString());
            source.IpString = "";
            source.IpEnd = "";
            source.GroupName = "AR-Test";
            WfTicket ticket = new()
            {
                Id = 7,
                Tasks = [groupTask, accessTask]
            };

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkGroups, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccess!.AccessSources!.Data, Is.Empty);
            Assert.That(apiConn.InsertedAccess.AccessSourceGroups!.Data, Has.Count.EqualTo(1));
            Assert.That(((NwGroupRef)apiConn.InsertedAccess.AccessSourceGroups.Data[0]).NwGroupId, Is.EqualTo(apiConn.InsertedNetworkGroups[0].Id));
        }

        [Test]
        public async Task CreateFlowInFlowDb_ReturnsFalseWhenTicketPartiallyPersists()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask validAccessTask = CreateAccessTask(21, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask invalidGroupTask = CreateNetworkGroupTask(20, "AR-Test", "10.0.0.1");
            invalidGroupTask.AdditionalInfo = null;
            WfTicket ticket = new()
            {
                Id = 7,
                Tasks = [invalidGroupTask, validAccessTask]
            };

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedAccess, Is.Not.Null);
            Assert.That(apiConn.InsertedNetworkGroups, Is.Empty);
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.EqualTo(new List<long> { 21 }));
        }

        [Test]
        public async Task CreateFlowInFlowDb_SkipsGroupDeletePayload()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateNetworkGroupTask(20, "AR-Test", "10.0.0.1");
            groupTask.TaskType = WfTaskType.group_delete.ToString();

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, groupTask, WfObjectScopes.RequestTask, null, groupTask.TicketId);

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedNetworkGroups, Is.Empty);
            Assert.That(apiConn.UpdatedRequestElements, Is.Empty);
        }

        [Test]
        public async Task CreateFlowInFlowDb_DoesNotCreateGroupFromElementGroupName()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateNetworkGroupTask(20, "AR-Test", "10.0.0.1");
            groupTask.AdditionalInfo = null;

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, groupTask, WfObjectScopes.RequestTask, null, groupTask.TicketId);

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedNetworkGroups, Is.Empty);
        }

        [Test]
        public async Task CreateFlowInFlowDb_SkipsNetworkGroupWithPartiallyResolvedMembers()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateNetworkGroupTask(20, "AR-Test", "10.0.0.1");
            groupTask.Elements.Add(CreateNetworkElement(202, groupTask.Id, ElemFieldType.source, ""));
            groupTask.Elements[1].FlowNetworkObjectId = 999;
            groupTask.Elements[1].GroupName = "AR-Test";

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, groupTask, WfObjectScopes.RequestTask, null, groupTask.TicketId);

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedNetworkGroups, Is.Empty);
            Assert.That(apiConn.UpdatedRequestElements, Is.Empty);
        }

        [Test]
        public async Task CreateFlowInFlowDb_ReusesExistingNetworkGroup()
        {
            string memberHash = FlowHashGenerator.GenerateNwObjectHash("10.0.0.1", "10.0.0.1");
            string groupHash = FlowHashGenerator.GenerateGroupHash([memberHash]);
            FlowDbCreatorTestApiConn apiConn = new();
            apiConn.ExistingNetworkObjects.Add(new FlowNwObject { Id = 10, Hash = memberHash });
            apiConn.ExistingNetworkGroups.Add(new FlowNwGroup
            {
                Id = 55,
                Name = "AR-Test",
                Hash = groupHash,
                NwGroupMembers = [new FlowNwGroupMember { NwGroupId = 55, NwObjectId = 10 }]
            });
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateNetworkGroupTask(20, "AR-Test", "10.0.0.1");

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, groupTask, WfObjectScopes.RequestTask, null, groupTask.TicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkGroups, Is.Empty);
            RequestElementFlowUpdate update = apiConn.UpdatedRequestElements.Single();
            Assert.That(update.FlowNetworkObjectId, Is.EqualTo(10));
            Assert.That(update.FlowNetworkGroupId, Is.EqualTo(55));
        }

        [Test]
        public async Task CreateFlowInFlowDb_SkipsServiceGroupWithPartiallyResolvedMembers()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateServiceGroupTask(20, "SG-Test", 443);
            groupTask.Elements.Add(CreateServiceElement(202, groupTask.Id, 8443));
            groupTask.Elements[1].FlowServiceObjectId = 999;
            groupTask.Elements[1].GroupName = "SG-Test";

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, groupTask, WfObjectScopes.RequestTask, null, groupTask.TicketId);

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedServiceGroups, Is.Empty);
            Assert.That(apiConn.UpdatedRequestElements, Is.Empty);
        }

        [Test]
        public async Task CreateFlowInFlowDb_PersistsServiceGroupAndUpdatesElementFlowIds()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask groupTask = CreateServiceGroupTask(20, "SG-Test", 443);

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, groupTask, WfObjectScopes.RequestTask, null, groupTask.TicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedServiceGroups, Has.Count.EqualTo(1));
            RequestElementFlowUpdate update = apiConn.UpdatedRequestElements.Single();
            Assert.That(update.Id, Is.EqualTo(201));
            Assert.That(update.FlowServiceObjectId, Is.EqualTo(201));
            Assert.That(update.FlowServiceGroupId, Is.EqualTo(apiConn.InsertedServiceGroups[0].Id));
        }

        [Test]
        public async Task CreateFlowInFlowDb_SkipsAccessWithIncompleteDimensions()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask task = CreateAccessTask(11, "10.0.0.1", "10.0.1.1", 443);
            task.Elements.RemoveAll(element => element.Field == ElemFieldType.service.ToString());

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, task, WfObjectScopes.RequestTask, null, task.TicketId);

            Assert.That(result, Is.False);
            Assert.That(apiConn.InsertedAccess, Is.Null);
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.Empty);
        }

        [Test]
        public async Task CreateFlowInFlowDb_ReusesExistingAccessWithoutInsert()
        {
            const string sourceHash = "source-hash";
            const string destinationHash = "destination-hash";
            const string serviceHash = "service-hash";
            FlowDbCreatorTestApiConn apiConn = new();
            apiConn.ExistingNetworkObjects.Add(new FlowNwObject { Id = 10, Hash = sourceHash });
            apiConn.ExistingNetworkObjects.Add(new FlowNwObject { Id = 11, Hash = destinationHash });
            apiConn.ExistingServiceObjects.Add(new FlowSvcObject { Id = 20, Hash = serviceHash });
            apiConn.ExistingAccesses.Add(new FlowAccess
            {
                Id = 900,
                Hash = FlowHashGenerator.GenerateAccessHash([sourceHash], [destinationHash], [serviceHash])
            });
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask task = CreateAccessTask(11, "10.0.0.1", "10.0.1.1", 443);
            task.Elements.Single(element => element.Field == ElemFieldType.source.ToString()).FlowNetworkObjectId = 10;
            task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString()).FlowNetworkObjectId = 11;
            task.Elements.Single(element => element.Field == ElemFieldType.service.ToString()).FlowServiceObjectId = 20;

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, task, WfObjectScopes.RequestTask, null, task.TicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedAccessCount, Is.EqualTo(0));
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.EqualTo(new List<long> { 11 }));
            Assert.That(apiConn.UpdatedRequestElements.Select(update => update.Id), Is.EquivalentTo(new long[] { 111, 112, 113 }));
        }

        [Test]
        public async Task CreateFlowInFlowDb_ResolvesSelectedObjectsAndServicesThroughFlowSyncMappings()
        {
            const string sourceHash = "source-hash";
            const string destinationHash = "destination-hash";
            const string serviceHash = "service-hash";
            FlowDbCreatorTestApiConn apiConn = new();
            apiConn.ExistingNetworkObjects.Add(new FlowNwObject { Id = 10, Hash = sourceHash, Objects = [new NetworkObject { Id = 501 }] });
            apiConn.ExistingNetworkObjects.Add(new FlowNwObject { Id = 11, Hash = destinationHash, Objects = [new NetworkObject { Id = 502 }] });
            apiConn.ExistingServiceObjects.Add(new FlowSvcObject { Id = 20, Hash = serviceHash, Services = [new NetworkService { Id = 601 }] });
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask task = CreateAccessTask(11, "10.0.0.1", "10.0.1.1", 443);
            WfReqElement source = task.Elements.Single(element => element.Field == ElemFieldType.source.ToString());
            source.NetworkId = 501;
            source.IpString = "";
            source.IpEnd = "";
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.NetworkId = 502;
            destination.IpString = "";
            destination.IpEnd = "";
            WfReqElement service = task.Elements.Single(element => element.Field == ElemFieldType.service.ToString());
            service.ServiceId = 601;
            service.ProtoId = null;
            service.Port = null;
            service.PortEnd = null;

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, task, WfObjectScopes.RequestTask, null, task.TicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkObjects, Is.Empty);
            Assert.That(apiConn.InsertedServiceObjects, Is.Empty);
            Assert.That(((NwRef)apiConn.InsertedAccess!.AccessSources!.Data.Single()).NwObjId, Is.EqualTo(10));
            Assert.That(((NwRef)apiConn.InsertedAccess.AccessDestinations!.Data.Single()).NwObjId, Is.EqualTo(11));
            Assert.That(((SvcRef)apiConn.InsertedAccess.AccessServices!.Data.Single()).SvcObjId, Is.EqualTo(20));
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.EqualTo(new List<long> { 11 }));
            Assert.That(apiConn.UpdatedRequestElements.Single(update => update.Id == 111).FlowNetworkObjectId, Is.EqualTo(10));
            Assert.That(apiConn.UpdatedRequestElements.Single(update => update.Id == 112).FlowNetworkObjectId, Is.EqualTo(11));
            Assert.That(apiConn.UpdatedRequestElements.Single(update => update.Id == 113).FlowServiceObjectId, Is.EqualTo(20));
        }

        [Test]
        public async Task CreateFlowInFlowDb_LoadsFlowSyncDataPerManagement()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            apiConn.ExistingNetworkObjectsByManagement[2] =
            [
                new FlowNwObject { Id = 10, Hash = "mgm-2-source", Objects = [new NetworkObject { Id = 501 }] },
                new FlowNwObject { Id = 11, Hash = "mgm-2-destination", Objects = [new NetworkObject { Id = 502 }] }
            ];
            apiConn.ExistingServiceObjectsByManagement[2] =
            [
                new FlowSvcObject { Id = 20, Hash = "mgm-2-service", Services = [new NetworkService { Id = 601 }] }
            ];
            apiConn.ExistingNetworkObjectsByManagement[3] =
            [
                new FlowNwObject { Id = 30, Hash = "mgm-3-source", Objects = [new NetworkObject { Id = 701 }] },
                new FlowNwObject { Id = 31, Hash = "mgm-3-destination", Objects = [new NetworkObject { Id = 702 }] }
            ];
            apiConn.ExistingServiceObjectsByManagement[3] =
            [
                new FlowSvcObject { Id = 40, Hash = "mgm-3-service", Services = [new NetworkService { Id = 801 }] }
            ];
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask first = CreateAccessTask(11, "", "", 443);
            SetSelectedObjectAndServiceIds(first, 501, 502, 601);
            WfReqTask second = CreateAccessTask(12, "", "", 8443);
            second.ManagementId = 3;
            SetSelectedObjectAndServiceIds(second, 701, 702, 801);
            WfTicket ticket = new()
            {
                Id = 7,
                Tasks = [first, second]
            };

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, ticket, WfObjectScopes.Ticket, null, ticket.Id);

            Assert.That(result, Is.True);
            Assert.That(apiConn.QueriedManagementIds.Distinct(), Is.EquivalentTo(new[] { 2, 3 }));
            Assert.That(apiConn.InsertedAccesses, Has.Count.EqualTo(2));
            Assert.That(((NwRef)apiConn.InsertedAccesses[0].AccessSources!.Data.Single()).NwObjId, Is.EqualTo(10));
            Assert.That(((NwRef)apiConn.InsertedAccesses[1].AccessSources!.Data.Single()).NwObjId, Is.EqualTo(30));
            Assert.That(((SvcRef)apiConn.InsertedAccesses[1].AccessServices!.Data.Single()).SvcObjId, Is.EqualTo(40));
            Assert.That(apiConn.UpdatedRequestTaskIds, Is.EquivalentTo(new long[] { 11, 12 }));
            Assert.That(apiConn.InsertedNetworkObjects, Is.Empty);
            Assert.That(apiConn.InsertedServiceObjects, Is.Empty);
        }

        [Test]
        public async Task CreateFlowInFlowDb_UsesFallbackNamesForInsertedObjects()
        {
            FlowDbCreatorTestApiConn apiConn = new();
            FlowDbCreator flowDbCreator = new(apiConn);
            WfReqTask task = CreateAccessTask(11, "10.0.0.1", "10.0.1.1", 443);
            WfReqElement source = task.Elements.Single(element => element.Field == ElemFieldType.source.ToString());
            source.IpEnd = "10.0.0.9";
            WfReqElement service = task.Elements.Single(element => element.Field == ElemFieldType.service.ToString());
            service.PortEnd = 445;

            bool? result = await flowDbCreator.CreateFlowInFlowDb(new WfStateAction { Name = "Create flow" }, task, WfObjectScopes.RequestTask, null, task.TicketId);

            Assert.That(result, Is.True);
            Assert.That(apiConn.InsertedNetworkObjects.First().Name, Is.EqualTo("10.0.0.1-10.0.0.9"));
            Assert.That(apiConn.InsertedServiceObjects.Single().Name, Is.EqualTo("6/443-445"));
        }

        private static void SetSelectedObjectAndServiceIds(WfReqTask task, long sourceObjectId, long destinationObjectId, long serviceId)
        {
            WfReqElement source = task.Elements.Single(element => element.Field == ElemFieldType.source.ToString());
            source.NetworkId = sourceObjectId;
            source.IpString = "";
            source.IpEnd = "";
            WfReqElement destination = task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString());
            destination.NetworkId = destinationObjectId;
            destination.IpString = "";
            destination.IpEnd = "";
            WfReqElement service = task.Elements.Single(element => element.Field == ElemFieldType.service.ToString());
            service.ServiceId = serviceId;
            service.ProtoId = null;
            service.Port = null;
            service.PortEnd = null;
        }

        private static WfReqTask CreateAccessTask(long taskId, string sourceIp, string destinationIp, int port)
        {
            return new()
            {
                Id = taskId,
                TicketId = 7,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                RuleAction = 1,
                ManagementId = 2,
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 5 } }],
                Elements =
                [
                    CreateNetworkElement(taskId * 10 + 1, taskId, ElemFieldType.source, sourceIp),
                    CreateNetworkElement(taskId * 10 + 2, taskId, ElemFieldType.destination, destinationIp),
                    CreateServiceElement(taskId * 10 + 3, taskId, port)
                ]
            };
        }

        private static WfReqTask CreateNetworkGroupTask(long taskId, string groupName, string memberIp)
        {
            WfReqTask task = new()
            {
                Id = taskId,
                TicketId = 7,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = 2,
                Elements =
                [
                    CreateNetworkElement(taskId * 10 + 1, taskId, ElemFieldType.source, memberIp)
                ]
            };
            task.Elements[0].GroupName = groupName;
            task.SetAddInfo(AdditionalInfoKeys.GrpName, groupName);
            return task;
        }

        private static WfReqTask CreateServiceGroupTask(long taskId, string groupName, int port)
        {
            WfReqTask task = new()
            {
                Id = taskId,
                TicketId = 7,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = 2,
                Elements =
                [
                    CreateServiceElement(taskId * 10 + 1, taskId, port)
                ]
            };
            task.Elements[0].GroupName = groupName;
            task.SetAddInfo(AdditionalInfoKeys.GrpName, groupName);
            return task;
        }

        private static WfReqElement CreateNetworkElement(long id, long taskId, ElemFieldType field, string ip)
        {
            return new()
            {
                Id = id,
                TaskId = taskId,
                Field = field.ToString(),
                IpString = ip,
                IpEnd = ip,
                RequestAction = RequestAction.create.ToString()
            };
        }

        private static WfReqElement CreateServiceElement(long id, long taskId, int port)
        {
            return new()
            {
                Id = id,
                TaskId = taskId,
                Field = ElemFieldType.service.ToString(),
                ProtoId = 6,
                Port = port,
                PortEnd = port,
                RequestAction = RequestAction.create.ToString()
            };
        }

        internal sealed class RequestElementFlowUpdate
        {
            public long Id { get; set; }
            public long? FlowNetworkObjectId { get; set; }
            public long? FlowNetworkGroupId { get; set; }
            public long? FlowServiceObjectId { get; set; }
            public long? FlowServiceGroupId { get; set; }
        }
    }
}
