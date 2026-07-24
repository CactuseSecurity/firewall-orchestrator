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
    internal class ModellingAppServerHandlerTest
    {
        private static readonly ReturnIdWrapper NewAppServerWrapper = new()
        {
            ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = 77 } }
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
            public int NewAppServerCalls { get; private set; }
            public int HistoryEntryCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByIp)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByName)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newAppServer)
                {
                    NewAppServerCalls++;
                    return Task.FromResult((QueryResponseType)(object)NewAppServerWrapper);
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
                ["edit_app_server"] = "edit_app_server",
                ["save_app_server"] = "save_app_server",
                ["wrong_ip_address"] = "wrong_ip_address",
                ["E5102"] = "E5102",
                ["U0001"] = "U0001"
            };
            return userConfig;
        }

        [Test]
        public async Task Save_ReturnsFalse_WhenMissingIpOrCustomType()
        {
            string? lastMessage = null;
            ModellingAppServerHandler handler = new(
                new ThrowingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                new ModellingAppServer { Ip = "", CustomType = 1 },
                [],
                false,
                (_, _, message, _) => lastMessage = message,
                false,
                false
            );

            bool result = await handler.Save();

            Assert.That(result, Is.False);
            Assert.That(lastMessage, Is.EqualTo("E5102"));
        }

        [Test]
        public async Task Save_ReturnsFalse_WhenIpInvalid_AndSetsManualImport()
        {
            string? lastMessage = null;
            ModellingAppServer appServer = new() { Ip = "invalid-ip", CustomType = 1 };
            ModellingAppServerHandler handler = new(
                new ThrowingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 42 },
                appServer,
                [],
                false,
                (_, _, message, _) => lastMessage = message,
                false,
                false
            );

            bool result = await handler.Save();

            Assert.That(result, Is.False);
            Assert.That(lastMessage, Is.EqualTo("wrong_ip_address"));
            Assert.That(appServer.AppId, Is.EqualTo(42));
            Assert.That(appServer.ImportSource, Is.EqualTo(GlobalConst.kManual));
        }

        [Test]
        public void Reset_RestoresOriginalValues_AndUpdatesList()
        {
            ModellingAppServer appServer = new()
            {
                Id = 7,
                Name = "original",
                Ip = "10.0.0.1",
                CustomType = 1
            };
            List<ModellingAppServer> available = [appServer];
            ModellingAppServerHandler handler = new(
                new ThrowingApiConnection(),
                CreateUserConfig(),
                new FwoOwner { Id = 1 },
                appServer,
                available,
                false,
                (_, _, _, _) => { },
                false,
                false
            );

            handler.ActAppServer.Name = "changed";
            handler.ActAppServer.Ip = "10.0.0.2";
            available[0] = handler.ActAppServer;

            handler.Reset();

            Assert.That(handler.ActAppServer.Name, Is.EqualTo("original"));
            Assert.That(handler.ActAppServer.Ip, Is.EqualTo("10.0.0.1"));
            Assert.That(available[0].Name, Is.EqualTo("original"));
        }

        [Test]
        public async Task Save_AddsAppServerWhenValid()
        {
            RecordingApiConnection apiConn = new();
            ModellingAppServer appServer = new()
            {
                Name = "srv1",
                Ip = "10.0.0.1",
                CustomType = 1
            };
            List<ModellingAppServer> available = new List<ModellingAppServer>();
            ModellingAppServerHandler handler = new(
                apiConn,
                CreateUserConfig(),
                new FwoOwner { Id = 42 },
                appServer,
                available,
                true,
                (_, _, _, _) => { },
                false,
                false
            );

            bool result = await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(appServer.Id, Is.EqualTo(77));
                Assert.That(appServer.AppId, Is.EqualTo(42));
                Assert.That(appServer.ImportSource, Is.EqualTo(GlobalConst.kManual));
                Assert.That(available, Has.Count.EqualTo(1));
                Assert.That(apiConn.NewAppServerCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
            });
        }
    }
}
