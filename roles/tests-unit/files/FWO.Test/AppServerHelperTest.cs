using FWO.Data.Modelling;
using FWO.Services.Modelling;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class AppServerHelperTest
    {
        private static ModellingNamingConvention CreateNamingConvention()
        {
            return new ModellingNamingConvention
            {
                AppServerPrefix = "srv-",
                NetworkPrefix = "net-",
                IpRangePrefix = "rng-"
            };
        }

        [Test]
        public void ConstructAppServerName_UsesPrefixAndIp_WhenNameMissing()
        {
            ModellingAppServer appServer = new()
            {
                Name = "",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("srv-10.0.0.1"));
        }

        [Test]
        public void ConstructAppServerName_ReturnsName_WhenStartsWithLetter()
        {
            ModellingAppServer appServer = new()
            {
                Name = "web-1",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("web-1"));
        }

        [Test]
        public void ConstructAppServerName_PrefixesName_WhenStartsWithDigit()
        {
            ModellingAppServer appServer = new()
            {
                Name = "1web",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("srv-1web"));
        }

        [Test]
        public void ConstructAppServerName_UsesNetworkPrefix_ForCidr()
        {
            ModellingAppServer appServer = new()
            {
                Name = "",
                Ip = "10.0.0.0/24",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("net-10.0.0.0/24"));
        }

        [Test]
        public void ConstructAppServerName_UsesIpRangePrefix_ForRange()
        {
            ModellingAppServer appServer = new()
            {
                Name = "",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.10"
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("rng-10.0.0.1-10.0.0.10"));
        }

        [Test]
        public void ConstructAppServerName_OverwritesExistingName_WhenRequested()
        {
            ModellingAppServer appServer = new()
            {
                Name = "web-1",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructAppServerName(appServer, CreateNamingConvention(), overwriteExistingNames: true);

            Assert.That(name, Is.EqualTo("srv-10.0.0.1"));
        }

        [Test]
        public void ConstructSanitizedAppServerName_ReplacesInvalidCharacters()
        {
            ModellingAppServer appServer = new()
            {
                Name = "web!1",
                Ip = "10.0.0.1",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructSanitizedAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("web_1"));
        }

        [Test]
        public void ConstructSanitizedAppServerName_SanitizesNetworkSlash()
        {
            ModellingAppServer appServer = new()
            {
                Name = "",
                Ip = "10.0.0.0/24",
                IpEnd = ""
            };

            string name = AppServerHelper.ConstructSanitizedAppServerName(appServer, CreateNamingConvention());

            Assert.That(name, Is.EqualTo("net-10.0.0.0_24"));
        }

        [Test]
        public async Task UpsertAppServer_UsesOverwriteAppServer_WhenSameSourceExists()
        {
            ModellingAppServer incoming = new()
            {
                Id = 0,
                AppId = 7,
                Name = "incoming",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = "import"
            };

            ModellingAppServer sameSource = new()
            {
                Id = 10,
                AppId = 7,
                Name = "existing-manual",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = "import"
            };

            ModellingAppServer otherSource = new()
            {
                Id = 11,
                AppId = 7,
                Name = "existing-import",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = GlobalConst.kManual,
                IsDeleted = false
            };

            AppServerHelperTestApiConn apiConn = new([sameSource, otherSource]);
            UserConfig userConfig = new SimulatedUserConfig();
            userConfig.User.Name = "tester";

            (long? appServerId, string? existingName) = await AppServerHelper.UpsertAppServer(apiConn, userConfig, incoming, nameCheck: false);

            Assert.That(appServerId, Is.EqualTo(sameSource.Id));
            Assert.That(existingName, Is.EqualTo(sameSource.Name));
            Assert.That(apiConn.UpdateAppServerCalls, Is.EqualTo(1));
            Assert.That(apiConn.SetDeletedCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task ReactivateOtherSource_ReplacesAppServerAndLogsHistory()
        {
            ModellingAppServer deleted = new()
            {
                Id = 5,
                AppId = 7,
                Name = "deleted",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = GlobalConst.kManual
            };

            ModellingAppServer reactivatable = new()
            {
                Id = 12,
                AppId = 7,
                Name = "reactivate",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = "import",
                IsDeleted = true
            };

            AppServerHelperTestApiConn apiConn = new([deleted, reactivatable]);
            UserConfig userConfig = new SimulatedUserConfig
            {
                AutoReplaceAppServer = true
            };
            userConfig.User.Name = "tester";

            await AppServerHelper.ReactivateOtherSource(apiConn, userConfig, deleted);

            Assert.That(apiConn.SetDeletedCalls, Is.EqualTo(1));
            Assert.That(apiConn.ReplaceInGroupCalls, Is.EqualTo(1));
            Assert.That(apiConn.ReplaceInConnectionCalls, Is.EqualTo(1));
            Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task NoHigherPrioActive_ReturnsFalse_WhenHigherPriorityAppServerExists()
        {
            ModellingAppServer incoming = new()
            {
                Id = 1,
                AppId = 7,
                Name = "incoming",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = GlobalConst.kManual
            };
            ModellingAppServer higherPrio = new()
            {
                Id = 2,
                AppId = 7,
                Name = "higher",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = "workflow",
                IsDeleted = false
            };
            AppServerHelperTestApiConn apiConn = new();
            apiConn.AppServersByIp = [higherPrio];

            bool result = await AppServerHelper.NoHigherPrioActive(apiConn, incoming);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task NoHigherPrioActive_ReturnsTrue_WhenLookupFails()
        {
            ModellingAppServer incoming = new()
            {
                Id = 1,
                AppId = 7,
                Name = "incoming",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = GlobalConst.kManual
            };
            AppServerHelperTestApiConn apiConn = new()
            {
                ThrowOnGetAppServersByIp = true
            };

            bool result = await AppServerHelper.NoHigherPrioActive(apiConn, incoming);

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task UpsertAppServer_AddsWhenNoExistingSameIp()
        {
            ModellingAppServer incoming = new()
            {
                Id = 0,
                AppId = 7,
                Name = "incoming",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = GlobalConst.kManual,
                CustomType = 1
            };
            AppServerHelperTestApiConn apiConn = new()
            {
                NewAppServerId = 99
            };
            UserConfig userConfig = new SimulatedUserConfig();
            userConfig.User.Name = "tester";

            (long? appServerId, string? existingName) = await AppServerHelper.UpsertAppServer(apiConn, userConfig, incoming, nameCheck: false);

            Assert.Multiple(() =>
            {
                Assert.That(appServerId, Is.EqualTo(99));
                Assert.That(existingName, Is.Null);
                Assert.That(apiConn.NewAppServerCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task UpsertAppServer_ReturnsExistingNameWhenHigherPriorityIsActive()
        {
            ModellingAppServer incoming = new()
            {
                Id = 1,
                AppId = 7,
                Name = "incoming",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.1",
                ImportSource = GlobalConst.kManual,
                CustomType = 1
            };
            AppServerHelperTestApiConn apiConn = new()
            {
                AppServersByIp =
                [
                    new()
                    {
                        Id = 2,
                        AppId = 7,
                        Name = "higher-prio",
                        Ip = "10.0.0.1",
                        IpEnd = "10.0.0.1",
                        ImportSource = "workflow",
                        IsDeleted = false
                    }
                ]
            };
            UserConfig userConfig = new SimulatedUserConfig();

            (long? appServerId, string? existingName) = await AppServerHelper.UpsertAppServer(apiConn, userConfig, incoming, nameCheck: false);

            Assert.Multiple(() =>
            {
                Assert.That(appServerId, Is.Null);
                Assert.That(existingName, Is.EqualTo("higher-prio"));
            });
        }

        [Test]
        public async Task AdjustAppServerNames_UpdatesNamesWhenConstructedNameDiffers()
        {
            ModellingAppServer appServer = new()
            {
                Id = 20,
                AppId = 7,
                Name = "old",
                Ip = "10.0.0.1",
                IpEnd = "10.0.0.2",
                ImportSource = "import"
            };
            AppServerHelperTestApiConn apiConn = new()
            {
                AllAppServers = [appServer]
            };
            SimulatedUserConfig userConfig = new()
            {
                ModNamingConvention = "{\"appServerPrefix\":\"srv-\",\"networkPrefix\":\"net-\",\"ipRangePrefix\":\"rng-\"}",
                OverwriteExistingNames = true
            };

            await AppServerHelper.AdjustAppServerNames(apiConn, userConfig);

            Assert.Multiple(() =>
            {
                Assert.That(appServer.Name, Is.EqualTo("rng-10.0.0.1-10.0.0.2"));
                Assert.That(appServer.ImportSource, Is.EqualTo(GlobalConst.kAdjustAppServerNames));
                Assert.That(apiConn.SetNameCalls, Is.EqualTo(1));
                Assert.That(apiConn.HistoryEntryCalls, Is.EqualTo(1));
            });
        }
    }

    internal sealed class AppServerHelperTestApiConn : SimulatedApiConnection
    {
        private static ReturnIdWrapper CreateEmptyReturnWrapper()
        {
            return new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId()]
            };
        }

        private readonly List<ModellingAppServer> appServersByIp;

        public bool ThrowOnGetAppServersByIp { get; set; }
        public List<ModellingAppServer> AllAppServers { get; set; } = [];
        public List<ModellingAppServer> AppServersByIp
        {
            get => appServersByIp;
            set
            {
                appServersByIp.Clear();
                appServersByIp.AddRange(value);
            }
        }
        public int NewAppServerCalls { get; private set; }
        public long NewAppServerId { get; set; } = 99;
        public int UpdateAppServerCalls { get; private set; }
        public int SetDeletedCalls { get; private set; }
        public int SetNameCalls { get; private set; }
        public int ReplaceInGroupCalls { get; private set; }
        public int ReplaceInConnectionCalls { get; private set; }
        public int HistoryEntryCalls { get; private set; }

        public AppServerHelperTestApiConn()
        {
            appServersByIp = [];
        }

        public AppServerHelperTestApiConn(List<ModellingAppServer> appServersByIp)
        {
            this.appServersByIp = appServersByIp;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByIp)
            {
                if (ThrowOnGetAppServersByIp)
                {
                    throw new InvalidOperationException("getAppServersByIp failed");
                }
                return Task.FromResult((QueryResponseType)(object)appServersByIp);
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByName)
            {
                List<ModellingAppServer> emptyAppServers = [];
                return Task.FromResult((QueryResponseType)(object)emptyAppServers);
            }

            if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAllAppServers)
            {
                return Task.FromResult((QueryResponseType)(object)AllAppServers);
            }

            if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.updateAppServer)
            {
                UpdateAppServerCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.setAppServerName)
            {
                SetNameCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ModellingQueries.newAppServer)
            {
                NewAppServerCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = new ReturnId[] { new ReturnId { NewIdLong = NewAppServerId } }
                });
            }

            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                if (query == ModellingQueries.setAppServerDeletedState)
                {
                    SetDeletedCalls++;
                    return Task.FromResult((QueryResponseType)(object)CreateEmptyReturnWrapper());
                }

                if (query == ModellingQueries.updateNwObjectInNwGroup)
                {
                    ReplaceInGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)CreateEmptyReturnWrapper());
                }

                if (query == ModellingQueries.updateNwObjectInConnection)
                {
                    ReplaceInConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)CreateEmptyReturnWrapper());
                }

                if (query == ModellingQueries.addHistoryEntry)
                {
                    HistoryEntryCalls++;
                    return Task.FromResult((QueryResponseType)(object)CreateEmptyReturnWrapper());
                }
            }

            throw new NotImplementedException();
        }
    }
}
