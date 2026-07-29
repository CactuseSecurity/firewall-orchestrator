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
    internal class ModellingServiceHandlerTest
    {
        private static readonly ReturnIdWrapper NewServiceWrapper = new()
        {
            ReturnIds = new ReturnId[] { new ReturnId { NewId = 91 } }
        };

        private sealed class ThrowingApiConnection : SimulatedApiConnection
        {
            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                throw new AssertionException("SendQueryAsync should not be called for validation-only tests.");
            }
        }

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public int NewServiceCalls { get; private set; }
            public int UpdateServiceCalls { get; private set; }
            public int HistoryEntryCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newService)
                {
                    NewServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)NewServiceWrapper);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.updateService)
                {
                    UpdateServiceCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.addHistoryEntry)
                {
                    HistoryEntryCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private static UserConfig CreateUserConfig()
        {
            UserConfig userConfig = new();
            userConfig.Translate = new Dictionary<string, string>
            {
                ["edit_service"] = "edit_service",
                ["save_service"] = "save_service",
                ["E5102"] = "E5102",
                ["E5103"] = "E5103",
                ["E5118"] = "E5118"
            };
            return userConfig;
        }

        [Test]
        public async Task Save_ReturnsFalse_WhenProtocolMissing()
        {
            string? lastMessage = null;
            ModellingService service = new() { Name = "svc", Protocol = new NetworkProtocol { Id = 0 } };
            ModellingServiceHandler handler = new(
                new ThrowingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                service,
                [],
                [],
                false,
                (_, _, message, _) => lastMessage = message
            );

            bool result = await handler.Save();

            Assert.That(result, Is.False);
            Assert.That(lastMessage, Is.EqualTo("E5102"));
        }

        [Test]
        public async Task Save_ReturnsFalse_WhenPortOutOfRange()
        {
            string? lastMessage = null;
            ModellingService service = new()
            {
                Name = "svc",
                Protocol = new NetworkProtocol { Id = 6 },
                Port = 0
            };
            ModellingServiceHandler handler = new(
                new ThrowingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                service,
                [],
                [],
                false,
                (_, _, message, _) => lastMessage = message
            );

            bool result = await handler.Save();

            Assert.That(result, Is.False);
            Assert.That(lastMessage, Is.EqualTo("E5103"));
        }

        [Test]
        public async Task Save_ReturnsFalse_WhenPortEndLessThanPort()
        {
            string? lastMessage = null;
            ModellingService service = new()
            {
                Name = "svc",
                Protocol = new NetworkProtocol { Id = 6 },
                Port = 100,
                PortEnd = 50
            };
            ModellingServiceHandler handler = new(
                new ThrowingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                service,
                [],
                [],
                false,
                (_, _, message, _) => lastMessage = message
            );

            bool result = await handler.Save();

            Assert.That(result, Is.False);
            Assert.That(lastMessage, Is.EqualTo("E5118"));
        }

        [Test]
        public void Reset_RestoresOriginalValues_AndUpdatesList()
        {
            ModellingService service = new()
            {
                Id = 9,
                Name = "original",
                Protocol = new NetworkProtocol { Id = 6 },
                Port = 80,
                PortEnd = 80
            };
            List<ModellingService> available = [service];
            List<KeyValuePair<int, int>> availableSvcElems = [];
            ModellingServiceHandler handler = new(
                new ThrowingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                service,
                available,
                availableSvcElems,
                false,
                (_, _, _, _) => { }
            );

            handler.ActService.Name = "changed";
            handler.ActService.Port = 443;
            available[0] = handler.ActService;

            handler.Reset();

            Assert.That(handler.ActService.Name, Is.EqualTo("original"));
            Assert.That(handler.ActService.Port, Is.EqualTo(80));
            Assert.That(available[0].Name, Is.EqualTo("original"));
        }

        [Test]
        public async Task Save_AddsServiceWhenValid()
        {
            RecordingApiConnection apiConn = new();
            ModellingService service = new()
            {
                Name = "svc",
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                Port = 80,
                PortEnd = 80
            };
            List<ModellingService> available = new List<ModellingService>();
            List<KeyValuePair<int, int>> availableSvcElems = new List<KeyValuePair<int, int>>();
            ModellingServiceHandler handler = new(
                apiConn,
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                service,
                available,
                availableSvcElems,
                true,
                (_, _, _, _) => { }
            );

            bool result = await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Id, Is.EqualTo(91));
                Assert.That(available, Has.Count.EqualTo(1));
                Assert.That(availableSvcElems, Has.Count.EqualTo(1));
                Assert.That(apiConn.NewServiceCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Save_UpdatesServiceWhenValid()
        {
            RecordingApiConnection apiConn = new();
            ModellingService service = new()
            {
                Id = 12,
                Name = "svc",
                Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                Port = 80,
                PortEnd = 80
            };
            List<ModellingService> available = new List<ModellingService> { service };
            List<KeyValuePair<int, int>> availableSvcElems = new List<KeyValuePair<int, int>>();
            ModellingServiceHandler handler = new(
                apiConn,
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                service,
                available,
                availableSvcElems,
                false,
                (_, _, _, _) => { }
            );

            bool result = await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConn.UpdateServiceCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
                Assert.That(available[0].Name, Is.EqualTo("svc"));
            });
        }
    }
}
