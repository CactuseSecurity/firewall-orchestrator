using FWO.Data.Modelling;
using FWO.Services.Modelling;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using NUnit.Framework;

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
                IpEnd = "",
                ImportSource = "import"
            };

            ModellingAppServer otherSource = new()
            {
                Id = 11,
                AppId = 7,
                Name = "existing-import",
                Ip = "10.0.0.1",
                IpEnd = "",
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
                IpEnd = "",
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
    }

    internal sealed class AppServerHelperTestApiConn : SimulatedApiConnection
    {
        private readonly List<ModellingAppServer> appServersByIp;

        public int UpdateAppServerCalls { get; private set; }
        public int SetDeletedCalls { get; private set; }
        public int ReplaceInGroupCalls { get; private set; }
        public int ReplaceInConnectionCalls { get; private set; }
        public int HistoryEntryCalls { get; private set; }

        public AppServerHelperTestApiConn(List<ModellingAppServer> appServersByIp)
        {
            this.appServersByIp = appServersByIp;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (typeof(QueryResponseType) == typeof(List<ModellingAppServer>) && query == ModellingQueries.getAppServersByIp)
            {
                return Task.FromResult((QueryResponseType)(object)appServersByIp);
            }

            if (typeof(QueryResponseType) == typeof(ReturnId) && query == ModellingQueries.updateAppServer)
            {
                UpdateAppServerCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                if (query == ModellingQueries.setAppServerDeletedState)
                {
                    SetDeletedCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId()] });
                }

                if (query == ModellingQueries.updateNwObjectInNwGroup)
                {
                    ReplaceInGroupCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId()] });
                }

                if (query == ModellingQueries.updateNwObjectInConnection)
                {
                    ReplaceInConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId()] });
                }

                if (query == ModellingQueries.addHistoryEntry)
                {
                    HistoryEntryCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId()] });
                }
            }

            throw new NotImplementedException();
        }
    }
}
