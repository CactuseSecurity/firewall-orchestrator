using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerSvcHandlingTest
    {
        private static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        private static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };
        private static readonly ReturnIdWrapper HistoryReturn = new() { ReturnIds = [new ReturnId()] };

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public List<ModellingServiceGroup> GlobalServiceGroups { get; set; } = [];
            public List<ModellingServiceGroup> AppServiceGroups { get; set; } = [];
            public List<ModellingService> GlobalServices { get; set; } = [];
            public List<ModellingService> AppServices { get; set; } = [];
            public List<ModellingServiceGroup> ServiceGroupsForService { get; set; } = [];
            public List<ModellingConnection> ConnectionIdsForService { get; set; } = [];
            public List<ModellingConnection> ConnectionIdsForServiceGroup { get; set; } = [];
            public int DeleteServiceGroupAffectedRows { get; set; } = 1;
            public int DeleteServiceAffectedRows { get; set; } = 1;

            public int GlobalServiceGroupCalls { get; private set; }
            public int AppServiceGroupCalls { get; private set; }
            public int GlobalServiceCalls { get; private set; }
            public int AppServiceCalls { get; private set; }
            public int ServiceGroupInUseCalls { get; private set; }
            public int ServiceInUseGroupCalls { get; private set; }
            public int ServiceInUseConnectionCalls { get; private set; }
            public int DeleteServiceGroupCalls { get; private set; }
            public int DeleteServiceCalls { get; private set; }
            public int AddServiceCalls { get; private set; }
            public int AddServiceGroupCalls { get; private set; }
            public int RemoveServiceCalls { get; private set; }
            public int RemoveServiceGroupCalls { get; private set; }
            public int HistoryEntryCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<ModellingServiceGroup>) && query == ModellingQueries.getGlobalServiceGroups)
                {
                    GlobalServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)GlobalServiceGroups);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingServiceGroup>) && query == ModellingQueries.getServiceGroupsForApp)
                {
                    AppServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)AppServiceGroups);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingService>) && query == ModellingQueries.getGlobalServices)
                {
                    GlobalServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)GlobalServices);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingService>) && query == ModellingQueries.getServicesForApp)
                {
                    AppServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)AppServices);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionIdsForServiceGroup)
                {
                    ServiceGroupInUseCalls++;
                    return Task.FromResult((QueryResponseType)(object)ConnectionIdsForServiceGroup);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingServiceGroup>) && query == ModellingQueries.getServiceGroupIdsForService)
                {
                    ServiceInUseGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)ServiceGroupsForService);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionIdsForService)
                {
                    ServiceInUseConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)ConnectionIdsForService);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.deleteServiceGroup)
                {
                    DeleteServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = DeleteServiceGroupAffectedRows });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.deleteService)
                {
                    DeleteServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = DeleteServiceAffectedRows });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.addServiceToConnection)
                {
                    AddServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.addServiceGroupToConnection)
                {
                    AddServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.removeServiceFromConnection)
                {
                    RemoveServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.removeServiceGroupFromConnection)
                {
                    RemoveServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.addHistoryEntry)
                {
                    HistoryEntryCalls++;
                    return Task.FromResult((QueryResponseType)(object)HistoryReturn);
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private static UserConfig CreateUserConfig(bool allowServiceInConn = true)
        {
            UserConfig userConfig = new();
            userConfig.User.Name = "tester";
            userConfig.AllowServiceInConn = allowServiceInConn;
            userConfig.Translate = new Dictionary<string, string>
            {
                ["edit_service"] = "edit_service",
                ["edit_service_group"] = "edit_service_group",
                ["delete_service"] = "delete_service",
                ["delete_service_group"] = "delete_service_group",
                ["fetch_data"] = "fetch_data",
                ["is_in_use"] = "is_in_use",
                ["U9003"] = "U9003",
                ["E9007"] = "E9007",
                ["U9004"] = "U9004",
                ["E9008"] = "E9008"
            };
            return userConfig;
        }

        private static ModellingConnectionHandler CreateHandler(RecordingApiConnection apiConnection, ModellingConnection connection, bool allowServiceInConn = true)
        {
            return new ModellingConnectionHandler(
                apiConnection,
                CreateUserConfig(allowServiceInConn),
                Application,
                [connection],
                connection,
                false,
                false,
                DisplayMessageInUi,
                () => Task.CompletedTask,
                true);
        }

        private static ModellingServiceWrapper WrapService(int id, string name)
        {
            return new ModellingServiceWrapper { Content = new ModellingService { Id = id, Name = name } };
        }

        private static ModellingServiceGroupWrapper WrapServiceGroup(int id, string name)
        {
            return new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = id, Name = name } };
        }

        private static async Task InvokePrivateAsync(ModellingConnectionHandler handler, string methodName, params object?[] parameters)
        {
            System.Reflection.MethodInfo method = typeof(ModellingConnectionHandler).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? throw new AssertionException($"Expected to find method '{methodName}'.");
            Task task = (Task)(method.Invoke(handler, parameters) ?? throw new AssertionException($"Expected method '{methodName}' to return a task."));
            await task;
        }

        [Test]
        public async Task InitAvailableSvcObjects_LoadsGroupsAndServicesAndPopulatesElements()
        {
            RecordingApiConnection apiConn = new()
            {
                GlobalServiceGroups =
                [
                    new() { Id = 10, Name = "GlobalVisible", AppId = 2 },
                    new() { Id = 11, Name = "FilteredOut", AppId = 1 }
                ],
                AppServiceGroups =
                [
                    new() { Id = 12, Name = "AppGroup", AppId = 1 }
                ],
                GlobalServices =
                [
                    new() { Id = 20, Name = "GlobalService" },
                    new() { Id = 21, Name = "FilteredOutService", AppId = 1 }
                ],
                AppServices =
                [
                    new() { Id = 22, Name = "AppService", AppId = 1 }
                ]
            };
            ModellingConnectionHandler handler = CreateHandler(apiConn, new ModellingConnection { Id = 1, Name = "Conn" }, true);

            await handler.InitAvailableSvcObjects();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.GlobalServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.AppServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.GlobalServiceCalls, Is.EqualTo(1));
                Assert.That(apiConn.AppServiceCalls, Is.EqualTo(1));
                Assert.That(handler.AvailableServiceGroups.Select(s => s.Id), Is.EquivalentTo(new[] { 10, 12 }));
                Assert.That(handler.AvailableServices.Select(s => s.Id), Is.EquivalentTo(new[] { 20, 22 }));
                Assert.That(handler.AvailableSvcElems, Does.Contain(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, 10)));
                Assert.That(handler.AvailableSvcElems, Does.Contain(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, 12)));
                Assert.That(handler.AvailableSvcElems, Does.Contain(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.Service, 20)));
                Assert.That(handler.AvailableSvcElems, Does.Contain(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.Service, 22)));
            });
        }

        [Test]
        public async Task InitAvailableSvcObjects_ExcludesServiceElements_WhenNotAllowed()
        {
            RecordingApiConnection apiConn = new()
            {
                GlobalServiceGroups =
                [
                    new() { Id = 10, Name = "GlobalVisible", AppId = 2 }
                ],
                AppServiceGroups =
                [
                    new() { Id = 12, Name = "AppGroup", AppId = 1 }
                ],
                GlobalServices =
                [
                    new() { Id = 20, Name = "GlobalService" }
                ],
                AppServices =
                [
                    new() { Id = 22, Name = "AppService", AppId = 1 }
                ]
            };
            ModellingConnectionHandler handler = CreateHandler(apiConn, new ModellingConnection { Id = 1, Name = "Conn" }, false);

            await handler.InitAvailableSvcObjects();

            Assert.Multiple(() =>
            {
                Assert.That(handler.AvailableServiceGroups.Select(s => s.Id), Is.EquivalentTo(new[] { 10, 12 }));
                Assert.That(handler.AvailableServices.Select(s => s.Id), Is.EquivalentTo(new[] { 20, 22 }));
                Assert.That(handler.AvailableSvcElems, Has.Count.EqualTo(2));
                Assert.That(handler.AvailableSvcElems, Does.Not.Contain(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.Service, 20)));
                Assert.That(handler.AvailableSvcElems, Does.Contain(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, 10)));
                Assert.That(handler.AvailableSvcElems, Does.Contain(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, 12)));
            });
        }

        [Test]
        public void SyncSvcChanges_AddsAndRemovesServicesAndGroups()
        {
            ModellingConnection connection = new()
            {
                Id = 1,
                Services = [WrapService(1, "SvcOld")],
                ServiceGroups = [WrapServiceGroup(2, "GrpOld")]
            };
            ModellingConnectionHandler handler = CreateHandler(new RecordingApiConnection(), connection);

            handler.SvcToDelete.Add(new ModellingService { Id = 1 });
            handler.SvcGrpToDelete.Add(new ModellingServiceGroup { Id = 2 });
            handler.SvcToAdd.Add(new ModellingService { Id = 3, Name = "SvcNew" });
            handler.SvcGrpToAdd.Add(new ModellingServiceGroup { Id = 4, Name = "GrpNew" });

            typeof(ModellingConnectionHandler)
                .GetMethod("SyncSvcChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(handler, null);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActConn.Services, Has.Count.EqualTo(1));
                Assert.That(handler.ActConn.Services[0].Content.Id, Is.EqualTo(3));
                Assert.That(handler.ActConn.ServiceGroups, Has.Count.EqualTo(1));
                Assert.That(handler.ActConn.ServiceGroups[0].Content.Id, Is.EqualTo(4));
            });
        }

        [Test]
        public async Task AddAndRemoveSvcObjects_InvokeExpectedMutations()
        {
            RecordingApiConnection apiConn = new();
            ModellingConnection connection = new() { Id = 7, Name = "Conn" };
            ModellingConnectionHandler handler = CreateHandler(apiConn, connection);

            handler.SvcToDelete.Add(new ModellingService { Id = 1, Name = "SvcOld" });
            handler.SvcGrpToDelete.Add(new ModellingServiceGroup { Id = 2, Name = "GrpOld" });
            await InvokePrivateAsync(handler, "RemoveSvcObjects");

            handler.SvcToAdd.Add(new ModellingService { Id = 3, Name = "SvcNew" });
            handler.SvcGrpToAdd.Add(new ModellingServiceGroup { Id = 4, Name = "GrpNew" });
            await InvokePrivateAsync(handler, "AddSvcObjects", handler.SvcToAdd, handler.SvcGrpToAdd);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.RemoveServiceCalls, Is.EqualTo(1));
                Assert.That(apiConn.RemoveServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.AddServiceCalls, Is.EqualTo(1));
                Assert.That(apiConn.AddServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(4));
            });
        }

        [Test]
        public void ServiceGroupActions_RespectNullAndNonNullInputs()
        {
            ModellingConnectionHandler handler = CreateHandler(new RecordingApiConnection(), new ModellingConnection { Id = 1, Name = "Conn" });

            handler.EditServiceGroup(null);
            handler.DisplayServiceGroup(null);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcGrpHandler, Is.Null);
                Assert.That(handler.EditSvcGrpMode, Is.False);
                Assert.That(handler.AddSvcGrpMode, Is.False);
                Assert.That(handler.DisplaySvcGrpMode, Is.False);
            });

            handler.CreateServiceGroup();

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcGrpHandler, Is.Not.Null);
                Assert.That(handler.AddSvcGrpMode, Is.True);
                Assert.That(handler.DisplaySvcGrpMode, Is.False);
                Assert.That(handler.EditSvcGrpMode, Is.True);
            });

            ModellingServiceGroup existingGroup = new() { Id = 33, Name = "ExistingGroup" };
            handler.EditServiceGroup(existingGroup);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcGrpHandler, Is.Not.Null);
                Assert.That(handler.SvcGrpHandler!.ActServiceGroup.Id, Is.EqualTo(33));
                Assert.That(handler.AddSvcGrpMode, Is.False);
                Assert.That(handler.DisplaySvcGrpMode, Is.False);
            });

            ModellingServiceGroup displayGroup = new() { Id = 44, Name = "DisplayGroup" };
            handler.DisplayServiceGroup(displayGroup);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcGrpHandler, Is.Not.Null);
                Assert.That(handler.SvcGrpHandler!.ActServiceGroup.Id, Is.EqualTo(44));
                Assert.That(handler.AddSvcGrpMode, Is.False);
                Assert.That(handler.DisplaySvcGrpMode, Is.True);
                Assert.That(handler.EditSvcGrpMode, Is.True);
            });
        }

        [Test]
        public async Task RequestDeleteServiceGrp_HandlesQueuedAndUsedGroups()
        {
            RecordingApiConnection queuedApiConn = new();
            ModellingConnectionHandler queuedHandler = CreateHandler(queuedApiConn, new ModellingConnection { Id = 1, Name = "Conn" });
            ModellingServiceGroup queuedGroup = new() { Id = 50, Name = "QueuedGroup" };
            queuedHandler.SvcGrpToAdd.Add(new ModellingServiceGroup { Id = 50, Name = "QueuedGroup" });

            await queuedHandler.RequestDeleteServiceGrp(queuedGroup);

            Assert.Multiple(() =>
            {
                Assert.That(queuedApiConn.ServiceGroupInUseCalls, Is.Zero);
                Assert.That(queuedHandler.DeleteAllowed, Is.False);
                Assert.That(queuedHandler.Message, Is.EqualTo("E9008QueuedGroup"));
                Assert.That(queuedHandler.DeleteSvcGrpMode, Is.True);
            });

            RecordingApiConnection apiConn = new()
            {
                ConnectionIdsForServiceGroup = [new ModellingConnection { Id = 2 }]
            };
            ModellingConnectionHandler blockedHandler = CreateHandler(apiConn, new ModellingConnection { Id = 1, Name = "Conn" });

            await blockedHandler.RequestDeleteServiceGrp(new ModellingServiceGroup { Id = 51, Name = "UsedGroup" });

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.ServiceGroupInUseCalls, Is.EqualTo(1));
                Assert.That(blockedHandler.DeleteAllowed, Is.False);
                Assert.That(blockedHandler.Message, Is.EqualTo("E9008UsedGroup"));
                Assert.That(blockedHandler.DeleteSvcGrpMode, Is.True);
            });

            RecordingApiConnection allowedApiConn = new();
            ModellingConnectionHandler allowedHandler = CreateHandler(allowedApiConn, new ModellingConnection { Id = 1, Name = "Conn" });

            await allowedHandler.RequestDeleteServiceGrp(new ModellingServiceGroup { Id = 52, Name = "FreeGroup" });

            Assert.Multiple(() =>
            {
                Assert.That(allowedApiConn.ServiceGroupInUseCalls, Is.EqualTo(1));
                Assert.That(allowedHandler.DeleteAllowed, Is.True);
                Assert.That(allowedHandler.Message, Is.EqualTo("U9004FreeGroup?"));
                Assert.That(allowedHandler.DeleteSvcGrpMode, Is.True);
            });
        }

        [Test]
        public async Task DeleteServiceGroup_RemovesAvailableObjectsAndLogsHistory()
        {
            RecordingApiConnection apiConn = new();
            ModellingServiceGroup serviceGroup = new() { Id = 60, Name = "DeleteMe" };
            ModellingConnectionHandler handler = CreateHandler(apiConn, new ModellingConnection { Id = 1, Name = "Conn" });
            handler.AvailableServiceGroups = [serviceGroup];
            handler.AvailableSvcElems = [new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, 60)];
            await handler.RequestDeleteServiceGrp(serviceGroup);

            await handler.DeleteServiceGroup();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.DeleteServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
                Assert.That(handler.AvailableServiceGroups, Is.Empty);
                Assert.That(handler.AvailableSvcElems, Is.Empty);
                Assert.That(handler.DeleteSvcGrpMode, Is.False);
            });
        }

        [Test]
        public void ServiceActions_RespectNullAndNonNullInputs()
        {
            ModellingConnectionHandler handler = CreateHandler(new RecordingApiConnection(), new ModellingConnection { Id = 1, Name = "Conn" });

            handler.EditService(null);

            Assert.That(handler.ServiceHandler, Is.Null);
            Assert.That(handler.EditServiceMode, Is.False);

            handler.CreateService();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ServiceHandler, Is.Not.Null);
                Assert.That(handler.AddServiceMode, Is.True);
                Assert.That(handler.EditServiceMode, Is.True);
            });

            ModellingService service = new() { Id = 70, Name = "EditMe" };
            handler.EditService(service);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ServiceHandler, Is.Not.Null);
                Assert.That(handler.ServiceHandler!.ActService.Id, Is.EqualTo(70));
                Assert.That(handler.AddServiceMode, Is.False);
                Assert.That(handler.EditServiceMode, Is.True);
            });
        }

        [Test]
        public async Task RequestDeleteService_HandlesQueuedAndUsedServices()
        {
            RecordingApiConnection queuedApiConn = new();
            ModellingConnectionHandler queuedHandler = CreateHandler(queuedApiConn, new ModellingConnection { Id = 1, Name = "Conn" });
            ModellingService queuedService = new() { Id = 80, Name = "QueuedService" };
            queuedHandler.SvcToAdd.Add(new ModellingService { Id = 80, Name = "QueuedService" });

            await queuedHandler.RequestDeleteService(queuedService);

            Assert.Multiple(() =>
            {
                Assert.That(queuedApiConn.ServiceInUseGroupCalls, Is.Zero);
                Assert.That(queuedApiConn.ServiceInUseConnectionCalls, Is.Zero);
                Assert.That(queuedHandler.DeleteAllowed, Is.False);
                Assert.That(queuedHandler.Message, Is.EqualTo("E9007QueuedService"));
                Assert.That(queuedHandler.DeleteServiceMode, Is.True);
            });

            RecordingApiConnection blockedApiConn = new()
            {
                ServiceGroupsForService = [],
                ConnectionIdsForService = [new ModellingConnection { Id = 2 }]
            };
            ModellingConnectionHandler blockedHandler = CreateHandler(blockedApiConn, new ModellingConnection { Id = 1, Name = "Conn" });

            await blockedHandler.RequestDeleteService(new ModellingService { Id = 81, Name = "UsedService" });

            Assert.Multiple(() =>
            {
                Assert.That(blockedApiConn.ServiceInUseGroupCalls, Is.EqualTo(1));
                Assert.That(blockedApiConn.ServiceInUseConnectionCalls, Is.EqualTo(1));
                Assert.That(blockedHandler.DeleteAllowed, Is.False);
                Assert.That(blockedHandler.Message, Is.EqualTo("E9007UsedService"));
                Assert.That(blockedHandler.DeleteServiceMode, Is.True);
            });

            RecordingApiConnection allowedApiConn = new();
            ModellingConnectionHandler allowedHandler = CreateHandler(allowedApiConn, new ModellingConnection { Id = 1, Name = "Conn" });

            await allowedHandler.RequestDeleteService(new ModellingService { Id = 82, Name = "FreeService" });

            Assert.Multiple(() =>
            {
                Assert.That(allowedApiConn.ServiceInUseGroupCalls, Is.EqualTo(1));
                Assert.That(allowedApiConn.ServiceInUseConnectionCalls, Is.EqualTo(1));
                Assert.That(allowedHandler.DeleteAllowed, Is.True);
                Assert.That(allowedHandler.Message, Is.EqualTo("U9003FreeService?"));
                Assert.That(allowedHandler.DeleteServiceMode, Is.True);
            });
        }

        [Test]
        public async Task DeleteService_RemovesAvailableObjectsAndLogsHistory()
        {
            RecordingApiConnection apiConn = new();
            ModellingService service = new() { Id = 90, Name = "DeleteMe" };
            ModellingConnectionHandler handler = CreateHandler(apiConn, new ModellingConnection { Id = 1, Name = "Conn" });
            handler.AvailableServices = [service];
            handler.AvailableSvcElems = [new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.Service, 90)];
            await handler.RequestDeleteService(service);

            await handler.DeleteService();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.DeleteServiceCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
                Assert.That(handler.AvailableServices, Is.Empty);
                Assert.That(handler.AvailableSvcElems, Is.Empty);
                Assert.That(handler.DeleteServiceMode, Is.False);
            });
        }

        [Test]
        public void ServiceCollections_AddOnlyMissingEntries()
        {
            ModellingConnection connection = new()
            {
                Id = 3,
                Services = [WrapService(1, "SvcExisting")],
                ServiceGroups = [WrapServiceGroup(2, "GrpExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(new RecordingApiConnection(), connection);
            handler.SvcToAdd.Add(new ModellingService { Id = 4, Name = "SvcAlreadyQueued" });
            handler.SvcGrpToAdd.Add(new ModellingServiceGroup { Id = 5, Name = "GrpAlreadyQueued" });

            handler.ServicesToConn(
            [
                new ModellingService { Id = 1, Name = "SvcExisting" },
                new ModellingService { Id = 4, Name = "SvcAlreadyQueued" },
                new ModellingService { Id = 6, Name = "SvcNew" }
            ]);
            handler.ServiceGrpsToConn(
            [
                new ModellingServiceGroup { Id = 2, Name = "GrpExisting" },
                new ModellingServiceGroup { Id = 5, Name = "GrpAlreadyQueued" },
                new ModellingServiceGroup { Id = 7, Name = "GrpNew" }
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(handler.SvcToAdd.Select(s => s.Id), Is.EquivalentTo(new[] { 4, 6 }));
                Assert.That(handler.SvcGrpToAdd.Select(s => s.Id), Is.EquivalentTo(new[] { 5, 7 }));
            });
        }
    }
}
