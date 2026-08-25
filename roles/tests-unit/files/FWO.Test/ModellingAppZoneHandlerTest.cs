using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services.Modelling;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ModellingAppZoneHandlerTest
    {
        private static readonly ReturnIdWrapper NewAppZoneWrapper = new()
        {
            ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = 55 } }
        };

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public List<ModellingAppServer> ServersForOwner { get; set; } = new List<ModellingAppServer>();
            public int NewAppZoneCalls { get; private set; }
            public int AddLinkCalls { get; private set; }
            public int RemoveLinkCalls { get; private set; }
            public int HistoryCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)ServersForOwner);
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newAppZone)
                {
                    NewAppZoneCalls++;
                    return Task.FromResult((QueryResponseType)(object)NewAppZoneWrapper);
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.addNwObjectToNwGroup)
                {
                    AddLinkCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.removeNwObjectFromNwGroup)
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
            userConfig.ModNamingConvention = "{\"networkAreaRequired\":true,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\"}";
            userConfig.User.Name = "tester";
            return userConfig;
        }

        private static ModellingAppServerWrapper Wrap(ModellingAppServer appServer)
        {
            return new ModellingAppServerWrapper { Content = appServer };
        }

        [Test]
        public async Task PlanAppZoneDbUpdate_CreatesZoneFromActiveServers()
        {
            RecordingApiConnection apiConn = new()
            {
                ServersForOwner = new List<ModellingAppServer>
                {
                    new ModellingAppServer { Id = 1, Name = "active", Ip = "10.0.0.1", IpEnd = "", IsDeleted = false },
                    new ModellingAppServer { Id = 2, Name = "deleted", Ip = "10.0.0.2", IpEnd = "", IsDeleted = true }
                }
            };
            ModellingAppZoneHandler handler = new(apiConn, CreateUserConfig(), new FwoOwner { Id = 9 }, (_, _, _, _) => { });

            ModellingAppZone zone = await handler.PlanAppZoneDbUpdate(null);

            Assert.Multiple(() =>
            {
                Assert.That(zone.AlreadyExistsInDb, Is.False);
                Assert.That(zone.AppServers, Has.Count.EqualTo(1));
                Assert.That(zone.AppServersNew, Has.Count.EqualTo(1));
                Assert.That(zone.AppServers[0].Content.Name, Is.EqualTo("active"));
            });
        }

        [Test]
        public async Task PlanAppZoneDbUpdate_ForExistingZone_FillsDiffLists()
        {
            RecordingApiConnection apiConn = new()
            {
                ServersForOwner = new List<ModellingAppServer>
                {
                    new ModellingAppServer { Id = 2, Name = "keep", Ip = "10.0.0.2", IpEnd = "", IsDeleted = false },
                    new ModellingAppServer { Id = 3, Name = "new", Ip = "10.0.0.3", IpEnd = "", IsDeleted = false }
                }
            };
            ModellingAppZoneHandler handler = new(apiConn, CreateUserConfig(), new FwoOwner { Id = 9 }, (_, _, _, _) => { });
            ModellingAppZone oldZone = new(9)
            {
                AppServers = new List<ModellingAppServerWrapper>
                {
                    Wrap(new ModellingAppServer { Id = 1, Name = "old", Ip = "10.0.0.1", IpEnd = "" }),
                    Wrap(new ModellingAppServer { Id = 2, Name = "keep", Ip = "10.0.0.2", IpEnd = "" })
                }
            };

            ModellingAppZone zone = await handler.PlanAppZoneDbUpdate(oldZone);

            Assert.Multiple(() =>
            {
                Assert.That(zone.AlreadyExistsInDb, Is.True);
                Assert.That(zone.AppServersNew, Has.Count.EqualTo(1));
                Assert.That(zone.AppServersRemoved, Has.Count.EqualTo(1));
                Assert.That(zone.AppServersUnchanged, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task UpsertAppZone_AddsZoneAndLinksServers()
        {
            RecordingApiConnection apiConn = new()
            {
                ServersForOwner = new List<ModellingAppServer>()
            };
            ModellingAppZoneHandler handler = new(apiConn, CreateUserConfig(), new FwoOwner { Id = 9 }, (_, _, _, _) => { });
            ModellingAppZone zone = new(9)
            {
                Name = "zone",
                AppServers = new List<ModellingAppServerWrapper>
                {
                    Wrap(new ModellingAppServer { Id = 10, Name = "one", Ip = "10.0.0.1", IpEnd = "" }),
                    Wrap(new ModellingAppServer { Id = 11, Name = "two", Ip = "10.0.0.2", IpEnd = "" })
                }
            };

            ModellingAppZone? result = await handler.UpsertAppZone(zone);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.SameAs(zone));
                Assert.That(zone.Id, Is.EqualTo(55));
                Assert.That(apiConn.NewAppZoneCalls, Is.EqualTo(1));
                Assert.That(apiConn.AddLinkCalls, Is.EqualTo(2));
                Assert.That(apiConn.HistoryCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task UpsertAppZone_UpdatesExistingZone_AddsAndRemovesServers()
        {
            RecordingApiConnection apiConn = new();
            ModellingAppZoneHandler handler = new(apiConn, CreateUserConfig(), new FwoOwner { Id = 9 }, (_, _, _, _) => { });
            ModellingAppZone zone = new(9)
            {
                AlreadyExistsInDb = true,
                Id = 88,
                AppServersNew = new List<ModellingAppServerWrapper>
                {
                    Wrap(new ModellingAppServer { Id = 10, Name = "add", Ip = "10.0.0.10", IpEnd = "" })
                },
                AppServersRemoved = new List<ModellingAppServerWrapper>
                {
                    Wrap(new ModellingAppServer { Id = 11, Name = "remove", Ip = "10.0.0.11", IpEnd = "" })
                }
            };

            ModellingAppZone? result = await handler.UpsertAppZone(zone);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.SameAs(zone));
                Assert.That(apiConn.RemoveLinkCalls, Is.EqualTo(1));
                Assert.That(apiConn.AddLinkCalls, Is.EqualTo(1));
            });
        }
    }
}
