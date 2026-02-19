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
    internal class ModellingAppServerHandlerTest
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
                ["edit_app_server"] = "edit_app_server",
                ["save_app_server"] = "save_app_server",
                ["wrong_ip_address"] = "wrong_ip_address",
                ["E5102"] = "E5102"
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
    }
}
