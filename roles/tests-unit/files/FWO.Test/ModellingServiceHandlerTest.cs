using FWO.Api.Client;
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
        private sealed class ThrowingApiConnection : SimulatedApiConnection
        {
            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null)
            {
                throw new AssertionException("SendQueryAsync should not be called for validation-only tests.");
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
    }
}
