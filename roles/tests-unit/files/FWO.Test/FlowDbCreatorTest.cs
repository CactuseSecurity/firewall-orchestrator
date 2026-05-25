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

            public List<long> UpdatedRequestTaskIds { get; } = [];
            public List<RequestElementFlowUpdate> UpdatedRequestElements { get; } = [];
            public FlowAccessInsert? InsertedAccess { get; private set; }
            public List<FlowNwGroup> InsertedNetworkGroups { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null)
            {
                if (query == FlowQueries.getFlowSyncNwObjects)
                {
                    return Task.FromResult((T)(object)new List<FlowNwObject>());
                }
                if (query == FlowQueries.getFlowSyncNwGroups)
                {
                    return Task.FromResult((T)(object)new List<FlowNwGroup>());
                }
                if (query == FlowQueries.getFlowSyncSvcObjects)
                {
                    return Task.FromResult((T)(object)new List<FlowSvcObject>());
                }
                if (query == FlowQueries.getFlowSyncSvcGroups)
                {
                    return Task.FromResult((T)(object)new List<FlowSvcGroup>());
                }
                if (query == FlowQueries.getFlowSyncAccesses)
                {
                    return Task.FromResult((T)(object)new List<FlowAccess>());
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
                    return Task.FromResult((T)(object)new FlowSvcObjectInsertResult { Returning = [inserted] });
                }
                if (query == FlowQueries.insertFlowAccesses)
                {
                    InsertedAccess = GetObjects<FlowAccessInsert>(variables).Single();
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
                    CreateAccessTask(2, "10.0.0.2", "10.0.1.2", 8443)
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
            Assert.That(apiConn.UpdatedRequestElements, Is.Empty);
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
            Assert.That(apiConn.UpdatedRequestElements, Is.Empty);
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
            Assert.That(apiConn.UpdatedRequestElements.Select(update => update.Id), Is.EquivalentTo(new long[] { 201 }));
            RequestElementFlowUpdate groupMemberUpdate = apiConn.UpdatedRequestElements.Single(update => update.Id == 201);
            Assert.That(groupMemberUpdate.FlowNetworkObjectId, Is.EqualTo(101));
            Assert.That(groupMemberUpdate.FlowNetworkGroupId, Is.EqualTo(401));
            Assert.That(apiConn.InsertedAccess!.AccessSources!.Data, Has.Count.EqualTo(1));
            Assert.That(apiConn.InsertedAccess.AccessSourceGroups!.Data, Is.Empty);
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
