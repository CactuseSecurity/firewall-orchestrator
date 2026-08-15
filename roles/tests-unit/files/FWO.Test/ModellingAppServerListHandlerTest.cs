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
    internal class ModellingAppServerListHandlerTest
    {
        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public List<ModellingAppServer> ManualServers { get; set; } = new List<ModellingAppServer>();
            public List<ModellingAppServer> CsvServers { get; set; } = new List<ModellingAppServer>();
            public List<ModellingAppServer> ServersByIp { get; set; } = new List<ModellingAppServer>();
            public List<ModellingAppRole> AppRolesForServer { get; set; } = new List<ModellingAppRole>();
            public List<ModellingConnection> ConnectionsForServer { get; set; } = new List<ModellingConnection>();
            public int SetDeletedCalls { get; private set; }
            public int DeleteCalls { get; private set; }
            public int HistoryCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByIp)
                {
                    return Task.FromResult((QueryResponseType)(object)ServersByIp);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersBySource)
                {
                    string importSource = GetVariable<string>(variables, "importSource");
                    if (importSource == GlobalConst.kManual)
                    {
                        return Task.FromResult((QueryResponseType)(object)ManualServers);
                    }
                    if (importSource.StartsWith(GlobalConst.kCSV_))
                    {
                        return Task.FromResult((QueryResponseType)(object)CsvServers);
                    }
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppServer>());
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingAppRole>) && query == ModellingQueries.getAppRolesForAppServer)
                {
                    return Task.FromResult((QueryResponseType)(object)AppRolesForServer);
                }

                if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionIdsForAppServer)
                {
                    return Task.FromResult((QueryResponseType)(object)ConnectionsForServer);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.setAppServerDeletedState)
                {
                    SetDeletedCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.deleteAppServer)
                {
                    DeleteCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.addHistoryEntry)
                {
                    HistoryCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }

                throw new AssertionException($"Unexpected query: {query}");
            }

            private static T GetVariable<T>(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperties().First(p => p.Name == propertyName).GetValue(variables, null);
                return value is T typedValue ? typedValue : throw new AssertionException($"Variable {propertyName} missing");
            }
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            SimulatedUserConfig userConfig = new();
            SimulatedUserConfig.DummyTranslate["fetch_data"] = "fetch_data";
            SimulatedUserConfig.DummyTranslate["edit_app_server"] = "edit_app_server";
            SimulatedUserConfig.DummyTranslate["delete_app_server"] = "delete_app_server";
            SimulatedUserConfig.DummyTranslate["reactivate"] = "reactivate";
            SimulatedUserConfig.DummyTranslate["U9005"] = "Reactivate ";
            SimulatedUserConfig.DummyTranslate["U9007"] = "Cannot delete used ";
            SimulatedUserConfig.DummyTranslate["U9008"] = "Delete ";
            return userConfig;
        }

        private static ModellingAppServer CreateServer(long id, string name, string ip, string importSource, bool inUse = false, bool deleted = false)
        {
            return new ModellingAppServer
            {
                Id = id,
                AppId = 7,
                Name = name,
                Ip = ip,
                IpEnd = ip,
                ImportSource = importSource,
                CustomType = 1,
                InUse = inUse,
                IsDeleted = deleted
            };
        }

        [Test]
        public async Task Init_LoadsServersAndFlags()
        {
            RecordingApiConnection apiConn = new()
            {
                ManualServers = new List<ModellingAppServer> { CreateServer(1, "manual", "10.0.0.1", GlobalConst.kManual) },
                CsvServers = new List<ModellingAppServer> { CreateServer(2, "csv", "10.0.0.2", GlobalConst.kCSV_ + "import") },
                ServersByIp = new List<ModellingAppServer>
                {
                    CreateServer(3, "higher", "10.0.0.1", "workflow")
                }
            };
            ModellingAppServerListHandler handler = new(
                apiConn,
                CreateUserConfig(),
                (_, _, _, _) => { },
                false,
                true
            );

            await handler.Init(new FwoOwner { Id = 9 });

            Assert.That(handler.ManualAppServers, Has.Count.EqualTo(2));
            Assert.That(handler.ManualAppServers[0].InUse, Is.False);
            Assert.That(handler.ManualAppServers[0].HighestPrio, Is.False);
            Assert.That(handler.ManualAppServers[1].InUse, Is.False);
        }

        [Test]
        public void RequestDeleteAppServer_SetsMessageForUsedServer()
        {
            ModellingAppServerListHandler handler = new(
                new RecordingApiConnection(),
                CreateUserConfig(),
                (_, _, _, _) => { },
                false,
                true
            );
            ModellingAppServer appServer = CreateServer(1, "srv", "10.0.0.1", GlobalConst.kManual, inUse: true);

            handler.RequestDeleteAppServer(appServer);

            Assert.That(handler.DeleteAppServerMode, Is.True);
            Assert.That(handler.Message, Is.EqualTo("Cannot delete used srv?"));
        }

        [Test]
        public async Task DeleteAppServer_DeletesWhenNotInUse()
        {
            RecordingApiConnection apiConn = new();
            ModellingAppServer appServer = CreateServer(1, "srv", "10.0.0.1", GlobalConst.kManual);
            ModellingAppServerListHandler handler = new(
                apiConn,
                CreateUserConfig(),
                (_, _, _, _) => { },
                false,
                true
            );
            handler.ManualAppServers.Add(appServer);
            handler.RequestDeleteAppServer(appServer);

            await handler.DeleteAppServer();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.DeleteCalls, Is.EqualTo(1));
                Assert.That(apiConn.SetDeletedCalls, Is.EqualTo(0));
                Assert.That(apiConn.HistoryCalls, Is.EqualTo(1));
                Assert.That(handler.DeleteAppServerMode, Is.False);
            });
        }

        [Test]
        public async Task DeleteAppServer_MarksUsedServerDeleted()
        {
            RecordingApiConnection apiConn = new()
            {
                AppRolesForServer = new List<ModellingAppRole> { new ModellingAppRole { Id = 4, Name = "role" } }
            };
            ModellingAppServer appServer = CreateServer(1, "srv", "10.0.0.1", GlobalConst.kManual, inUse: true);
            ModellingAppServerListHandler handler = new(
                apiConn,
                CreateUserConfig(),
                (_, _, _, _) => { },
                false,
                true
            );
            handler.ManualAppServers.Add(appServer);
            handler.RequestDeleteAppServer(appServer);

            await handler.DeleteAppServer();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.SetDeletedCalls, Is.EqualTo(1));
                Assert.That(apiConn.DeleteCalls, Is.EqualTo(0));
                Assert.That(apiConn.HistoryCalls, Is.EqualTo(1));
                Assert.That(handler.DeleteAppServerMode, Is.False);
            });
        }

        [Test]
        public async Task ReactivateAppServer_ReactivatesDeletedServer()
        {
            RecordingApiConnection apiConn = new();
            ModellingAppServer appServer = CreateServer(1, "srv", "10.0.0.1", GlobalConst.kManual, deleted: true);
            ModellingAppServerListHandler handler = new(
                apiConn,
                CreateUserConfig(),
                (_, _, _, _) => { },
                false,
                true
            );
            handler.ManualAppServers.Add(appServer);
            handler.RequestReactivateAppServer(appServer);

            await handler.ReactivateAppServer();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.SetDeletedCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryCalls, Is.EqualTo(1));
                Assert.That(handler.ReactivateAppServerMode, Is.False);
            });
        }
    }
}
