using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Net;
using System.Text.Json;

namespace FWO.Test.AppServerIpChange
{
    /// <summary>
    /// Runs against an installed system: two app data imports deliver the same app, whose app server changes its ip
    /// while both ips resolve to the same dns name. The variance analysis has to detect the changed ip although the
    /// name is unchanged, and has to request the new member under the ip suffixed name. Nothing is requested or
    /// written to a firewall. Set up and cleaned up by roles/tests-integration/tasks/test-app-server-ip-change.yml.
    /// The reverse lookups of the test ips are answered in memory, see CreateTestReverseLookup.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class AppServerIpChangeTest
    {
        private const int kJwtLifetimeMinutes = 30;

        // the modelled app role and connection only exist in memory, their ids are never stored
        private const int kInMemoryId = 999999999;
        private const int kTcpProtocolId = 6;
        private const int kServicePort = 4711;

        // compares group members by ip and analyses app roles separately, as needed to see a changed member ip
        private const string kRuleRecognitionOption = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwRegardGroupName\":false," +
            "\"nwResolveGroup\":false,\"nwSeparateGroupAnalysis\":true,\"svcRegardPortAndProt\":true,\"svcRegardName\":false," +
            "\"svcRegardGroupName\":false,\"svcResolveGroup\":true,\"svcSplitPortRanges\":true}";

        /// <summary>
        /// Imports the app with the old ip, then with the new ip, and analyses the variance against the test firewall.
        /// </summary>
        [Test]
        public async Task ChangedIpWithUnchangedDnsNameIsDetectedByVarianceAnalysis()
        {
            AppServerIpChangeSettings settings = AppServerIpChangeSettings.FromEnvironment();

            using ApiConnection apiConnection = CreateMiddlewareConnection();
            using GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection);

            await ImportAppData(apiConnection, globalConfig, settings, settings.FirstImportSource);
            FwoOwner owner = await LoadTestOwner(apiConnection, settings);
            AssertAppServersAfterFirstImport(await LoadAppServers(apiConnection, owner, settings), settings);

            await ImportAppData(apiConnection, globalConfig, settings, settings.SecondImportSource);
            List<ModellingAppServer> appServers = await LoadAppServers(apiConnection, owner, settings);
            AssertAppServersAfterSecondImport(appServers, settings);

            ModellingAppServer activeAppServer = appServers.First(appServer => !appServer.IsDeleted);
            List<ModellingConnection> connections = BuildModelledConnections(owner, activeAppServer, settings.AppRoleName);
            using UserConfig userConfig = CreateVarianceUserConfig(globalConfig, apiConnection);
            await AssertVarianceDetectsChangedIp(apiConnection, userConfig, owner, connections, settings);
            await AssertRequestUsesIpSuffixedName(apiConnection, userConfig, owner, connections, settings);
        }

        /// <summary>
        /// Reverse lookup used by the app data import instead of the dns: both test ips resolve to the server name,
        /// every other ip is unresolvable. Avoids a test name server, which would have to run as root.
        /// </summary>
        private static Func<IPAddress, Task<string>> CreateTestReverseLookup(AppServerIpChangeSettings settings)
        {
            Dictionary<IPAddress, string> serverNamesByIp = new()
            {
                [IPAddress.Parse(settings.OldIp)] = settings.ServerName,
                [IPAddress.Parse(settings.NewIp)] = settings.ServerName
            };
            return address => Task.FromResult(serverNamesByIp.GetValueOrDefault(address, ""));
        }

        private static GraphQlApiConnection CreateMiddlewareConnection()
        {
            JwtWriter jwtWriter = new(ConfigFile.JwtPrivateKey);
            string jwt = jwtWriter.CreateJWTMiddlewareServer(TimeSpan.FromMinutes(kJwtLifetimeMinutes));
            return new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
        }

        /// <summary>
        /// Runs the app data import for one import file. The paths and the dns lookup are only set in memory,
        /// the stored global config stays as it is.
        /// The guard source is invalid on purpose: a failed source closes the owner import control as unsuccessful,
        /// so the rule owner mapping never takes up the changes of the test app (see getPendingRuleOwnerImports).
        /// </summary>
        private static async Task ImportAppData(ApiConnection apiConnection, GlobalConfig globalConfig, AppServerIpChangeSettings settings, string importSource)
        {
            List<string> importSources = [importSource, settings.GuardImportSource];
            List<string> expectedFailedImports = [settings.GuardImportSource + ".json"];
            globalConfig.ImportAppDataPath = JsonSerializer.Serialize(importSources);
            globalConfig.DnsLookup = true;

            using AppDataImport appDataImport = new(apiConnection, globalConfig, CreateTestReverseLookup(settings));
            List<string> failedImports = await appDataImport.Run();

            Assert.That(failedImports, Is.EqualTo(expectedFailedImports), $"app data import of {importSource} failed");
        }

        private static async Task<FwoOwner> LoadTestOwner(ApiConnection apiConnection, AppServerIpChangeSettings settings)
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
            FwoOwner? owner = owners.FirstOrDefault(existingOwner => existingOwner.ExtAppId == settings.ExtAppId);
            Assert.That(owner, Is.Not.Null, $"app {settings.ExtAppId} was not imported");
            return owner!;
        }

        private static async Task<List<ModellingAppServer>> LoadAppServers(ApiConnection apiConnection, FwoOwner owner, AppServerIpChangeSettings settings)
        {
            var variables = new
            {
                importSource = settings.AppImportSource,
                appId = owner.Id
            };
            return await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersBySource, variables);
        }

        /// <summary>
        /// The import file names the app server by its ip, so the dns name shows that the import resolved it
        /// already for the new app.
        /// </summary>
        private static void AssertAppServersAfterFirstImport(List<ModellingAppServer> appServers, AppServerIpChangeSettings settings)
        {
            List<ModellingAppServer> activeAppServers = [.. appServers.Where(appServer => !appServer.IsDeleted)];
            Assert.That(activeAppServers, Has.Count.EqualTo(1), "first import: one active app server expected");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(activeAppServers[0].Name, Is.EqualTo(settings.ServerName), "first import: dns name expected");
                Assert.That(activeAppServers[0].Ip.StripOffNetmask(), Is.EqualTo(settings.OldIp));
            }
        }

        private static void AssertAppServersAfterSecondImport(List<ModellingAppServer> appServers, AppServerIpChangeSettings settings)
        {
            List<ModellingAppServer> activeAppServers = [.. appServers.Where(appServer => !appServer.IsDeleted)];
            List<ModellingAppServer> deletedAppServers = [.. appServers.Where(appServer => appServer.IsDeleted)];
            Assert.That(activeAppServers, Has.Count.EqualTo(1), "second import: one active app server expected");
            Assert.That(deletedAppServers, Has.Count.EqualTo(1), "second import: the app server with the old ip has to be marked deleted");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(activeAppServers[0].Name, Is.EqualTo(settings.ServerName), "second import: dns name expected");
                Assert.That(activeAppServers[0].Ip.StripOffNetmask(), Is.EqualTo(settings.NewIp));
                Assert.That(deletedAppServers[0].Name, Is.EqualTo(settings.ServerName), "the name must not change with the ip");
                Assert.That(deletedAppServers[0].Ip.StripOffNetmask(), Is.EqualTo(settings.OldIp));
            }
        }

        /// <summary>
        /// Models the app role as a modeller would after the second import: it contains the app server with the new ip.
        /// The app role has the name of the group on the test firewall, which still contains the old ip.
        /// </summary>
        private static List<ModellingConnection> BuildModelledConnections(FwoOwner owner, ModellingAppServer appServer, string appRoleName)
        {
            ModellingAppRole appRole = new()
            {
                Id = kInMemoryId,
                Name = appRoleName,
                IdString = appRoleName,
                AppId = owner.Id,
                GroupType = (int)ModellingTypes.ModObjectType.AppRole,
                AppServers = [new() { Content = appServer }]
            };
            ModellingService service = new()
            {
                Id = kInMemoryId,
                Name = appRoleName,
                Port = kServicePort,
                ProtoId = kTcpProtocolId
            };
            ModellingConnection connection = new()
            {
                Id = kInMemoryId,
                AppId = owner.Id,
                Name = appRoleName,
                SourceAppRoles = [new() { Content = appRole }],
                DestinationAppRoles = [new() { Content = appRole }],
                Services = [new() { Content = service }]
            };
            return [connection];
        }

        private static UserConfig CreateVarianceUserConfig(GlobalConfig globalConfig, ApiConnection apiConnection)
        {
            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);
            userConfig.RuleRecognitionOption = kRuleRecognitionOption;
            userConfig.CreateAppZones = false;
            return userConfig;
        }

        private static ModellingVarianceAnalysis CreateVarianceAnalysis(ApiConnection apiConnection, UserConfig userConfig, FwoOwner owner, VarianceMessages messages)
        {
            return new ModellingVarianceAnalysis(apiConnection, new ExtStateHandler(apiConnection), userConfig, owner, messages.Collect)
            {
                // a fall back would write a log entry into the database
                LogPrefilterFallbackToDb = false
            };
        }

        private static async Task AssertVarianceDetectsChangedIp(ApiConnection apiConnection, UserConfig userConfig, FwoOwner owner,
            List<ModellingConnection> connections, AppServerIpChangeSettings settings)
        {
            VarianceMessages messages = new();
            ModellingVarianceAnalysis varianceAnalysis = CreateVarianceAnalysis(apiConnection, userConfig, owner, messages);

            ModellingVarianceResult result = await varianceAnalysis.AnalyseRulesVsModelledConnections(connections, new ModellingFilter());

            Assert.That(messages.Errors, Is.Empty, "variance analysis reported errors");
            Assert.That(result.DifferingAppRoles.TryGetValue(settings.ManagementId, out List<ModellingAppRole>? differingAppRoles), Is.True,
                "the test firewall is not analysed, check its ext_mgm_data");
            ModellingAppRole? differingAppRole = differingAppRoles!.FirstOrDefault(appRole => appRole.IdString == settings.AppRoleName);
            Assert.That(differingAppRole, Is.Not.Null, "the changed ip of the app server is not detected");
            Assert.That(differingAppRole!.SurplusAppServers, Has.Count.EqualTo(1), "the member with the old ip has to be surplus");
            ModellingAppServer surplusAppServer = differingAppRole.SurplusAppServers[0].Content;
            ModellingAppServer modelledAppServer = differingAppRole.AppServers.Single().Content;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(surplusAppServer.Name, Is.EqualTo(settings.ServerName));
                Assert.That(surplusAppServer.Ip.StripOffNetmask(), Is.EqualTo(settings.OldIp));
                Assert.That(modelledAppServer.Name, Is.EqualTo(settings.ServerName));
                Assert.That(modelledAppServer.Ip.StripOffNetmask(), Is.EqualTo(settings.NewIp));
                Assert.That(modelledAppServer.NotImplemented, Is.True, "the member with the new ip has to be missing on the firewall");
            }
        }

        /// <summary>
        /// The firewall object with the old ip keeps its name, as it may be used elsewhere: the new member is requested
        /// as a new object with the ip appended to the name. The request tasks are only built, not stored.
        /// </summary>
        private static async Task AssertRequestUsesIpSuffixedName(ApiConnection apiConnection, UserConfig userConfig, FwoOwner owner,
            List<ModellingConnection> connections, AppServerIpChangeSettings settings)
        {
            VarianceMessages messages = new();
            ModellingVarianceAnalysis varianceAnalysis = CreateVarianceAnalysis(apiConnection, userConfig, owner, messages);

            List<WfReqTask> tasks = await varianceAnalysis.AnalyseModelledConnectionsForRequest(connections);

            Assert.That(messages.Errors, Is.Empty, "variance analysis for request reported errors");
            List<WfReqTask> testFirewallTasks = [.. tasks.Where(task => task.ManagementId == settings.ManagementId)];
            string updateTitle = userConfig.GetText("update_app_role") + settings.AppRoleName;
            WfReqTask? addTask = testFirewallTasks.FirstOrDefault(task => task.Title == updateTitle + userConfig.GetText("add_members"));
            WfReqTask? removeTask = testFirewallTasks.FirstOrDefault(task => task.Title == updateTitle + userConfig.GetText("remove_members"));
            Assert.That(addTask, Is.Not.Null, "no task adds the member with the new ip");
            Assert.That(removeTask, Is.Not.Null, "no task removes the member with the old ip");

            WfReqElement? newMember = addTask!.Elements.FirstOrDefault(element => element.RequestAction == RequestAction.create.ToString());
            WfReqElement? oldMember = removeTask!.Elements.FirstOrDefault(element => element.RequestAction == RequestAction.delete.ToString());
            Assert.That(newMember, Is.Not.Null, "the member with the new ip is not requested");
            Assert.That(oldMember, Is.Not.Null, "the member with the old ip is not removed");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(newMember!.Name, Is.EqualTo($"{settings.ServerName}_{settings.NewIp}"));
                Assert.That(newMember.IpString?.StripOffNetmask(), Is.EqualTo(settings.NewIp));
                Assert.That(oldMember!.Name, Is.EqualTo(settings.ServerName));
                Assert.That(oldMember.IpString?.StripOffNetmask(), Is.EqualTo(settings.OldIp));
            }
        }

        /// <summary>
        /// Collects the errors the variance analysis would show in the UI.
        /// </summary>
        private sealed class VarianceMessages
        {
            public List<string> Errors { get; } = [];

            public void Collect(Exception? exception, string title, string message, bool isError)
            {
                if (isError)
                {
                    Errors.Add($"{title}: {message} {exception?.Message}");
                }
            }
        }
    }
}
