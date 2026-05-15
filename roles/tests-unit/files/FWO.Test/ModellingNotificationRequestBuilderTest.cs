using System.Text.Json;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Services.Modelling;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingNotificationRequestBuilderTest
    {
        [Test]
        public void BuildRequestTasks_CreatesAccessTasksForConnectionsNotRequestedOnFw()
        {
            SimulatedUserConfig userConfig = new();
            ModellingNotificationRequestBuilder builder = new(userConfig);
            FwoOwner owner = new() { Id = 7, Name = "App" };
            ModellingConnection requested = CreateConnection(11, "Implemented");
            requested.RequestedOnFw = true;

            List<WfReqTask> tasks = builder.BuildRequestTasks(
            [
                CreateConnection(10),
                requested
            ], owner, stateId: 23);

            Assert.That(tasks, Has.Count.EqualTo(2));
            WfReqTask task = tasks.Single(task => task.TaskType == WfTaskType.access.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(task.Title, Is.EqualTo("New Connection: Conn10 (FWOC10)"));
                Assert.That(task.TaskNumber, Is.EqualTo(2));
                Assert.That(task.TaskType, Is.EqualTo(WfTaskType.access.ToString()));
                Assert.That(task.StateId, Is.EqualTo(23));
                Assert.That(task.Owners[0].Owner.Id, Is.EqualTo(7));
                Assert.That(task.Elements, Has.Count.EqualTo(3));
                Assert.That(task.Elements.Any(element => element.Field == ElemFieldType.source.ToString() && element.GroupName == "AR1"), Is.True);
                Assert.That(task.Elements.Any(element => element.Field == ElemFieldType.destination.ToString() && element.Name == "Server1"), Is.True);
                Assert.That(task.Elements.Single(element => element.Field == ElemFieldType.destination.ToString()).IpString, Is.EqualTo("10.0.0.1"));
                Assert.That(task.Elements.Any(element => element.Field == ElemFieldType.service.ToString() && element.Name == "HTTP"), Is.True);
            });

            Dictionary<string, string>? addInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(task.AdditionalInfo ?? "{}");
            Assert.That(addInfo?[AdditionalInfoKeys.ConnId], Is.EqualTo("10"));
        }

        [Test]
        public void BuildRequestTasks_FiltersConnectionsAndGroupsByIncludedMarkerState()
        {
            SimulatedUserConfig userConfig = new()
            {
                ModIntegrationStateMarker = "ImplementationState",
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
                [
                    new() { Name = "Retry", IncludeIntoRequest = true },
                    new() { Name = "Done", IncludeIntoRequest = false }
                ])
            };
            ModellingNotificationRequestBuilder builder = new(userConfig);
            FwoOwner owner = new() { Id = 7, Name = "App" };
            ModellingConnection unmarkedConnection = CreateConnection(10);
            ModellingConnection includedConnection = CreateConnection(11);
            includedConnection.AddProperty("ImplementationState", "Retry");
            ModellingConnection excludedConnection = CreateConnection(12);
            excludedConnection.AddProperty("ImplementationState", "Done");
            includedConnection.SourceAppRoles =
            [
                new() { Content = new() { Id = 101, IdString = "ARIncluded", Comment = "ImplementationState: Retry" } },
                new() { Content = new() { Id = 102, IdString = "ARExcluded", Comment = "ImplementationState: Done" } },
                new() { Content = new() { Id = 103, IdString = "ARUnmarked" } }
            ];
            includedConnection.ServiceGroups =
            [
                new() { Content = new() { Id = 201, Name = "SGIncluded", Comment = "ImplementationState: Retry" } },
                new() { Content = new() { Id = 202, Name = "SGExcluded", Comment = "ImplementationState: Done" } },
                new() { Content = new() { Id = 203, Name = "SGUnmarked" } }
            ];

            List<WfReqTask> tasks = builder.BuildRequestTasks([unmarkedConnection, includedConnection, excludedConnection], owner, stateId: 23);

            Assert.That(tasks.Where(task => task.TaskType == WfTaskType.access.ToString()).Select(task => task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId)), Is.EquivalentTo(new int?[] { 10, 11 }));
            WfReqTask includedTask = tasks.Single(task => task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) == 11);
            Assert.That(tasks.Count(task => task.TaskType == WfTaskType.group_create.ToString()), Is.EqualTo(5));
            Assert.Multiple(() =>
            {
                Assert.That(includedTask.Elements.Any(element => element.GroupName == "ARIncluded"), Is.True);
                Assert.That(includedTask.Elements.Any(element => element.GroupName == "ARUnmarked"), Is.True);
                Assert.That(includedTask.Elements.Any(element => element.GroupName == "ARExcluded"), Is.False);
                Assert.That(includedTask.Elements.Any(element => element.GroupName == "SGIncluded"), Is.True);
                Assert.That(includedTask.Elements.Any(element => element.GroupName == "SGUnmarked"), Is.True);
                Assert.That(includedTask.Elements.Any(element => element.GroupName == "SGExcluded"), Is.False);
            });
        }

        [Test]
        public void BuildRequestTasks_RequestsIncludedMarkerStateConnectionsOnlyWhenChangedAfterMarker()
        {
            DateTime baseline = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
            SimulatedUserConfig userConfig = ConfigWithRetryIncluded();
            ModellingConnection unchangedConnection = CreateConnection(20);
            MarkState(unchangedConnection, "Retry", baseline);
            ModellingConnection changedConnection = CreateConnection(21);
            MarkState(changedConnection, "Retry", baseline);
            List<ModellingHistoryEntry> history =
            [
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.Connection,
                    ObjectId = 21,
                    ChangeTime = baseline.AddMinutes(1)
                }
            ];
            ModellingNotificationRequestBuilder builder = new(userConfig, history);

            List<WfReqTask> tasks = builder.BuildRequestTasks([unchangedConnection, changedConnection], new() { Id = 7, Name = "App" }, stateId: 23);

            Assert.That(tasks.Where(task => task.TaskType == WfTaskType.access.ToString()).Select(task => task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId)), Is.EquivalentTo(new int?[] { 21 }));
        }

        [Test]
        public void BuildRequestTasks_RequestsConnectionWhenReferencedSourceObjectChangedAfterMarker()
        {
            DateTime baseline = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
            SimulatedUserConfig userConfig = ConfigWithRetryIncluded();
            ModellingConnection connection = CreateConnection(24);
            MarkState(connection, "Retry", baseline);
            connection.SourceAppRoles =
            [
                new()
                {
                    Content = new()
                    {
                        Id = 101,
                        IdString = "ARChanged",
                        Comment = MarkedComment("Retry", baseline),
                        AppServers = [new() { Content = new() { Id = 301, Name = "NewSource", Ip = "10.0.2.1/32" } }]
                    }
                }
            ];
            List<ModellingHistoryEntry> history =
            [
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.AppServer,
                    ObjectId = 301,
                    ChangeTime = baseline.AddMinutes(1)
                }
            ];
            ModellingNotificationRequestBuilder builder = new(userConfig, history);

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, stateId: 23);

            WfReqTask accessTask = tasks.Single(task => task.TaskType == WfTaskType.access.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(tasks.Count(task => task.TaskType == WfTaskType.group_create.ToString()), Is.EqualTo(1));
                Assert.That(accessTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId), Is.EqualTo(24));
                Assert.That(accessTask.Elements.Any(element => element.Field == ElemFieldType.source.ToString() && element.GroupName == "ARChanged"), Is.True);
            });
        }

        [Test]
        public void BuildRequestTasks_RequestsAlreadyRequestedConnectionWhenConnectionChangedAfterMarker()
        {
            DateTime baseline = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
            SimulatedUserConfig userConfig = ConfigWithRetryIncluded();
            ModellingConnection connection = CreateConnection(25);
            connection.RequestedOnFw = true;
            MarkState(connection, "Retry", baseline);
            List<ModellingHistoryEntry> history =
            [
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.Connection,
                    ObjectId = 25,
                    ChangeTime = baseline.AddMinutes(1)
                }
            ];
            ModellingNotificationRequestBuilder builder = new(userConfig, history);

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, stateId: 23);

            WfReqTask accessTask = tasks.Single(task => task.TaskType == WfTaskType.access.ToString());
            Assert.That(accessTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId), Is.EqualTo(25));
        }

        [Test]
        public void BuildRequestTasks_DoesNotRequestAlreadyRequestedConnectionWithoutIncludedMarkerState()
        {
            ModellingConnection connection = CreateConnection(26);
            connection.RequestedOnFw = true;
            ModellingNotificationRequestBuilder builder = new(new SimulatedUserConfig());

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, stateId: 23);

            Assert.That(tasks, Is.Empty);
        }

        [Test]
        public void BuildRequestTasks_DoesNotRequestUnmarkedGroupsFromAlreadyRequestedConnection()
        {
            ModellingConnection connection = CreateConnection(27);
            connection.RequestedOnFw = true;
            connection.SourceAppRoles =
            [
                new() { Content = new() { Id = 127, IdString = "ARAlreadyRequested" } }
            ];
            connection.ServiceGroups =
            [
                new() { Content = new() { Id = 227, Name = "SGAlreadyRequested" } }
            ];
            ModellingNotificationRequestBuilder builder = new(new SimulatedUserConfig());

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, stateId: 23);

            Assert.That(tasks, Is.Empty);
        }

        [Test]
        public void BuildRequestTasks_UsesLastRequestStartAsChangeBaselineWhenAvailable()
        {
            DateTime stateSetAt = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
            DateTime lastRequestStartedAt = stateSetAt.AddMinutes(5);
            SimulatedUserConfig userConfig = ConfigWithRetryIncluded();
            ModellingConnection changedBeforeRequest = CreateConnection(22);
            MarkState(changedBeforeRequest, "Retry", stateSetAt);
            ModellingConnection changedAfterRequest = CreateConnection(23);
            MarkState(changedAfterRequest, "Retry", stateSetAt);
            List<ModellingHistoryEntry> history =
            [
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.Connection,
                    ObjectId = 22,
                    ChangeTime = stateSetAt.AddMinutes(1)
                },
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.Connection,
                    ObjectId = 23,
                    ChangeTime = lastRequestStartedAt.AddMinutes(1)
                }
            ];
            ModellingNotificationRequestBuilder builder = new(userConfig, history, lastRequestStartedAt);

            List<WfReqTask> tasks = builder.BuildRequestTasks([changedBeforeRequest, changedAfterRequest], new() { Id = 7, Name = "App" }, stateId: 23);

            Assert.That(tasks.Where(task => task.TaskType == WfTaskType.access.ToString()).Select(task => task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId)), Is.EquivalentTo(new int?[] { 23 }));
        }

        [Test]
        public void BuildRequestTasks_RequestsIncludedMarkerStateGroupsOnlyWhenChangedAfterMarker()
        {
            DateTime baseline = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
            SimulatedUserConfig userConfig = ConfigWithRetryIncluded();
            ModellingConnection connection = CreateConnection(30);
            connection.SourceAppRoles =
            [
                new() { Content = new() { Id = 111, IdString = "ARUnchanged", Comment = MarkedComment("Retry", baseline) } },
                new() { Content = new() { Id = 112, IdString = "ARChanged", Comment = MarkedComment("Retry", baseline) } }
            ];
            connection.ServiceGroups =
            [
                new() { Content = new() { Id = 211, Name = "SGUnchanged", Comment = MarkedComment("Retry", baseline) } },
                new() { Content = new() { Id = 212, Name = "SGChanged", Comment = MarkedComment("Retry", baseline) } }
            ];
            List<ModellingHistoryEntry> history =
            [
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.AppRole,
                    ObjectId = 112,
                    ChangeTime = baseline.AddMinutes(1)
                },
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.ServiceGroup,
                    ObjectId = 212,
                    ChangeTime = baseline.AddMinutes(1)
                }
            ];
            ModellingNotificationRequestBuilder builder = new(userConfig, history);

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, stateId: 23);
            WfReqTask accessTask = tasks.Single(task => task.TaskType == WfTaskType.access.ToString());

            Assert.Multiple(() =>
            {
                Assert.That(tasks.Count(task => task.TaskType == WfTaskType.group_create.ToString()), Is.EqualTo(2));
                Assert.That(accessTask.Elements.Any(element => element.GroupName == "ARChanged"), Is.True);
                Assert.That(accessTask.Elements.Any(element => element.GroupName == "ARUnchanged"), Is.True);
                Assert.That(accessTask.Elements.Any(element => element.GroupName == "SGChanged"), Is.True);
                Assert.That(accessTask.Elements.Any(element => element.GroupName == "SGUnchanged"), Is.True);
            });
        }

        [Test]
        public void BuildRequestTasks_RequestsGroupsFromConnectionsNotSelectedForAccess()
        {
            DateTime baseline = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
            SimulatedUserConfig userConfig = ConfigWithRetryIncluded();
            ModellingConnection connection = CreateConnection(31);
            MarkState(connection, "Done", baseline);
            connection.SourceAppRoles =
            [
                new() { Content = new() { Id = 121, IdString = "ARUnmarked" } },
                new() { Content = new() { Id = 122, IdString = "ARChanged", Comment = MarkedComment("Retry", baseline) } },
                new() { Content = new() { Id = 123, IdString = "ARUnchanged", Comment = MarkedComment("Retry", baseline) } }
            ];
            connection.ServiceGroups =
            [
                new() { Content = new() { Id = 221, Name = "SGUnmarked" } },
                new() { Content = new() { Id = 222, Name = "SGChanged", Comment = MarkedComment("Retry", baseline) } },
                new() { Content = new() { Id = 223, Name = "SGUnchanged", Comment = MarkedComment("Retry", baseline) } }
            ];
            List<ModellingHistoryEntry> history =
            [
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.AppRole,
                    ObjectId = 122,
                    ChangeTime = baseline.AddMinutes(1)
                },
                new()
                {
                    ObjectType = (int)ModellingTypes.ModObjectType.ServiceGroup,
                    ObjectId = 222,
                    ChangeTime = baseline.AddMinutes(1)
                }
            ];
            ModellingNotificationRequestBuilder builder = new(userConfig, history);

            List<WfReqTask> tasks = builder.BuildRequestTasks([connection], new() { Id = 7, Name = "App" }, stateId: 23);

            Assert.Multiple(() =>
            {
                Assert.That(tasks.Any(task => task.TaskType == WfTaskType.access.ToString()), Is.False);
                Assert.That(tasks.Count(task => task.TaskType == WfTaskType.group_create.ToString()), Is.EqualTo(4));
                Assert.That(tasks.Select(task => task.GetAddInfoValue(AdditionalInfoKeys.GrpName)), Does.Contain("ARUnmarked"));
                Assert.That(tasks.Select(task => task.GetAddInfoValue(AdditionalInfoKeys.GrpName)), Does.Contain("ARChanged"));
                Assert.That(tasks.Select(task => task.GetAddInfoValue(AdditionalInfoKeys.GrpName)), Does.Contain("SGUnmarked"));
                Assert.That(tasks.Select(task => task.GetAddInfoValue(AdditionalInfoKeys.GrpName)), Does.Contain("SGChanged"));
                Assert.That(tasks.Select(task => task.GetAddInfoValue(AdditionalInfoKeys.GrpName)), Does.Not.Contain("ARUnchanged"));
                Assert.That(tasks.Select(task => task.GetAddInfoValue(AdditionalInfoKeys.GrpName)), Does.Not.Contain("SGUnchanged"));
            });
        }

        private static ModellingConnection CreateConnection(int id, params string[] extraConfigTypes)
        {
            return new()
            {
                Id = id,
                Name = $"Conn{id}",
                Reason = "Need access",
                ExtraConfigs = [.. extraConfigTypes.Select(extraConfigType => new ModellingExtraConfig { ExtraConfigType = extraConfigType })],
                SourceAppRoles = [new() { Content = new() { Id = 1, IdString = "AR1" } }],
                DestinationAppServers = [new() { Content = new() { Name = "Server1", Ip = "10.0.0.1/32" } }],
                Services = [new() { Content = new() { Name = "HTTP", ProtoId = 6, Port = 80 } }]
            };
        }

        private static SimulatedUserConfig ConfigWithRetryIncluded()
        {
            return new()
            {
                ModIntegrationStateMarker = "ImplementationState",
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue([new() { Name = "Retry", IncludeIntoRequest = true }])
            };
        }

        private static void MarkState(ModellingConnection connection, string stateName, DateTime stateSetAt)
        {
            connection.AddProperty("ImplementationState", ModIntegrationStateConfig.BuildStateValue(stateName, stateSetAt.ToString("O")));
        }

        private static string MarkedComment(string stateName, DateTime stateSetAt)
        {
            return $"ImplementationState: {ModIntegrationStateConfig.BuildStateValue(stateName, stateSetAt.ToString("O"))}";
        }
    }
}
