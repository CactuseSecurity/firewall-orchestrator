using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.Modelling;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ModellingServiceGroupHandlerTest
    {
        private static readonly ReturnIdWrapper NewServiceGroupWrapper = new()
        {
            ReturnIds = new ReturnId[] { new ReturnId { NewId = 55 } }
        };

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public int NewServiceGroupCalls { get; private set; }
            public int UpdateServiceGroupCalls { get; private set; }
            public int AddLinkCalls { get; private set; }
            public int RemoveLinkCalls { get; private set; }
            public int HistoryCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newServiceGroup)
                {
                    NewServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)NewServiceGroupWrapper);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.updateServiceGroup)
                {
                    UpdateServiceGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.addServiceToServiceGroup)
                {
                    AddLinkCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.removeServiceFromServiceGroup)
                {
                    RemoveLinkCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.addHistoryEntry)
                {
                    HistoryCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private static UserConfig CreateUserConfig()
        {
            UserConfig userConfig = new();
            userConfig.User.Name = "tester";
            userConfig.Translate = new Dictionary<string, string>
            {
                ["edit_service"] = "edit_service",
                ["save_service"] = "save_service",
                ["edit_service_group"] = "edit_service_group",
                ["save_service_group"] = "save_service_group",
                ["add_service_group"] = "add_service_group",
                ["delete_service"] = "delete_service",
                ["U0001"] = "U0001",
                ["E5102"] = "E5102"
            };
            return userConfig;
        }

        private static ModellingService CreateService(int id, string name)
        {
            return new ModellingService
            {
                Id = id,
                Name = name,
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                Port = 80,
                PortEnd = 80
            };
        }

        private static ModellingServiceWrapper Wrap(ModellingService service)
        {
            return new ModellingServiceWrapper { Content = service };
        }

        [Test]
        public void CreateService_SetsGlobalStateOnNewService()
        {
            ModellingServiceGroupHandler handler = new(
                new RecordingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 5 },
                new List<ModellingServiceGroup>(),
                new ModellingServiceGroup { IsGlobal = true },
                new List<ModellingService>(),
                new List<KeyValuePair<int, int>>(),
                true,
                (_, _, _, _) => { },
                () => Task.CompletedTask);

            handler.CreateService();

            Assert.That(handler.ServiceHandler, Is.Not.Null);
            Assert.That(handler.ServiceHandler!.ActService.IsGlobal, Is.True);
        }

        [Test]
        public async Task Save_AddsServiceGroupAndLinksServices()
        {
            RecordingApiConnection apiConn = new();
            ModellingService service = CreateService(10, "svc1");
            ModellingServiceGroup serviceGroup = new()
            {
                Name = "group",
                Comment = "comment",
                Services = new List<ModellingServiceWrapper> { Wrap(service) }
            };
            List<ModellingServiceGroup> serviceGroups = new List<ModellingServiceGroup>();
            List<ModellingService> availableServices = new List<ModellingService>();
            List<KeyValuePair<int, int>> availableSvcElems = new List<KeyValuePair<int, int>>();
            ModellingServiceGroupHandler handler = new(
                apiConn,
                CreateUserConfig(),
                new FwoOwner { Id = 5 },
                serviceGroups,
                serviceGroup,
                availableServices,
                availableSvcElems,
                true,
                (_, _, _, _) => { },
                () => Task.CompletedTask);

            bool result = await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(serviceGroup.Id, Is.EqualTo(55));
                Assert.That(serviceGroups, Has.Count.EqualTo(1));
                Assert.That(availableSvcElems, Has.Count.EqualTo(1));
                Assert.That(apiConn.NewServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.AddLinkCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryCalls, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task Save_UpdatesServiceGroupAndAppliesQueuedChanges()
        {
            RecordingApiConnection apiConn = new();
            ModellingService removeService = CreateService(11, "remove");
            ModellingService addService = CreateService(12, "add");
            ModellingServiceGroup serviceGroup = new()
            {
                Id = 77,
                Name = "group",
                Comment = "comment",
                Services = new List<ModellingServiceWrapper> { Wrap(removeService) }
            };
            List<ModellingServiceGroup> serviceGroups = new List<ModellingServiceGroup> { serviceGroup };
            List<ModellingService> availableServices = new List<ModellingService>();
            List<KeyValuePair<int, int>> availableSvcElems = new List<KeyValuePair<int, int>>();
            ModellingServiceGroupHandler handler = new(
                apiConn,
                CreateUserConfig(),
                new FwoOwner { Id = 5 },
                serviceGroups,
                serviceGroup,
                availableServices,
                availableSvcElems,
                false,
                (_, _, _, _) => { },
                () => Task.CompletedTask);
            handler.SvcToAdd.Add(addService);
            handler.SvcToDelete = new List<ModellingService> { removeService };

            bool result = await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConn.UpdateServiceGroupCalls, Is.EqualTo(1));
                Assert.That(apiConn.RemoveLinkCalls, Is.EqualTo(1));
                Assert.That(apiConn.AddLinkCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryCalls, Is.EqualTo(3));
                Assert.That(handler.SvcToAdd, Is.Empty);
                Assert.That(handler.SvcToDelete, Is.Empty);
            });
        }
    }
}
