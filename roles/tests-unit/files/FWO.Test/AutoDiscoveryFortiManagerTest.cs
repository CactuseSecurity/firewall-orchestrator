using FWO.Api.Client;
using FWO.Data;
using FWO.DeviceAutoDiscovery;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class AutoDiscoveryFortiManagerTest
    {
        [Test]
        public void ConvertAdomsToManagements_SetsDeviceUidToName_WhenUidMissing()
        {
            Management superManagement = new()
            {
                Name = "fmgr",
                DeviceType = new DeviceType { Id = 12 }
            };
            SimulatedApiConnection apiConnection = new();
            AutoDiscoveryFortiManager discovery = new(superManagement, apiConnection);

            Adom adom = new()
            {
                Name = "root",
                DeviceList =
                [
                    new FortiGate { Name = "gw-1", Uid = "" }
                ]
            };

            MethodInfo? convertMethod = typeof(AutoDiscoveryFortiManager)
                .GetMethod("ConvertAdomsToManagements", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(convertMethod, Is.Not.Null);

            var managements = (List<Management>)convertMethod!.Invoke(discovery, [new List<Adom> { adom }])!;

            Assert.That(managements, Has.Count.EqualTo(1));
            Assert.That(managements[0].Devices, Has.Length.EqualTo(1));
            Assert.That(managements[0].Devices[0].Uid, Is.EqualTo("gw-1"));
        }

        [Test]
        public void BuildAdomDeviceVdomStructure_PreCanceledToken_StopsBeforeQueryingDevices()
        {
            Management superManagement = new()
            {
                Name = "fmgr",
                Hostname = "fmgr.invalid",
                DeviceType = new DeviceType { Id = 12 }
            };
            AutoDiscoveryFortiManager discovery = new(superManagement, new SimulatedApiConnection());
            List<FortiGate> existingDevices = [new FortiGate { Name = "gw-1" }];
            Adom adom = new() { Name = "root", DeviceList = existingDevices };

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await discovery.BuildAdomDeviceVdomStructure("session", [adom], new FortiManagerClient(superManagement), new CancellationToken(canceled: true)));
            Assert.That(adom.DeviceList, Is.SameAs(existingDevices));
        }

        [Test]
        public void Run_PreCanceledToken_StopsBeforeAuthenticating()
        {
            Management superManagement = new()
            {
                Name = "fmgr",
                Hostname = "fmgr.invalid",
                DeviceType = new DeviceType { Id = 12, Name = "FortiManager" }
            };
            RecordingQueryApiConnection apiConnection = new();
            AutoDiscoveryBase discovery = new(superManagement, apiConnection);

            Assert.ThrowsAsync<OperationCanceledException>(async () => await discovery.Run(new CancellationToken(canceled: true)));
            Assert.That(apiConnection.QueryCount, Is.EqualTo(0), "no deltas may be calculated for a stopped discovery");
        }

        private sealed class RecordingQueryApiConnection : SimulatedApiConnection
        {
            public int QueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                return base.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
            }
        }

        [Test]
        public void CheckDeviceNotInMgmt_MatchesFortiManagerGateway_ByUid()
        {
            Device existing = new() { Name = "gw-1_vdomA", Uid = "gw-1" };
            Device discovered = new() { Name = "renamed-gateway", Uid = "gw-1" };
            Management management = new() { Devices = [existing] };

            MethodInfo? compareMethod = typeof(AutoDiscoveryBase)
                .GetMethod("CheckDeviceNotInMgmt", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(compareMethod, Is.Not.Null);

            bool notInManagement = (bool)compareMethod!.Invoke(null, [discovered, management, true])!;

            Assert.That(notInManagement, Is.False);
        }

        [Test]
        public void CheckDeviceNotInMgmt_DoesNotMatchFortiManagerGateway_WhenUidMissing()
        {
            Device existing = new() { Name = "old-gateway", Uid = "" };
            Device discovered = new() { Name = "new-gateway", Uid = "" };
            Management management = new() { Devices = [existing] };

            MethodInfo? compareMethod = typeof(AutoDiscoveryBase)
                .GetMethod("CheckDeviceNotInMgmt", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(compareMethod, Is.Not.Null);

            bool notInManagement = (bool)compareMethod!.Invoke(null, [discovered, management, true])!;

            Assert.That(notInManagement, Is.True);
        }

        [Test]
        public void DiscoverManagementDetails_MatchesFortiManagerAdom_ByUid()
        {
            Management existing = new()
            {
                Id = 7,
                Name = "fmgr_old_name",
                Uid = "adom-uid",
                ConfigPath = "old-name",
                Hostname = "fmgr.example.test",
                Port = 443,
                SuperManagerId = 1,
                ImportDisabled = false,
                Devices =
                [
                    new Device { Id = 10, Name = "fg-1_root", Uid = "fg-1_root", ImportDisabled = false }
                ]
            };
            Management discovered = new()
            {
                Name = "fmgr_new_name",
                Uid = "adom-uid",
                ConfigPath = "new-name",
                Hostname = "fmgr.example.test",
                Port = 443,
                SuperManagerId = 1,
                Devices =
                [
                    new Device { Name = "fg-1_root", Uid = "fg-1_root" }
                ]
            };
            List<Management> deltaManagements = [];
            List<Management> existingManagements = [existing];
            List<Management> discoveredManagements = [discovered];

            InvokeDiscoverManagementDetails(discovered, deltaManagements, existingManagements, discoveredManagements);

            Assert.That(deltaManagements, Is.Empty);
        }

        [Test]
        public void DiscoverManagementDetails_MatchesFortiManagerAdom_ByConfigPath_WhenUidChanged()
        {
            Management existing = CreateAdomManagement(7, "old-adom-uid", "adomA", 1);
            existing.Devices = [new Device { Id = 10, Name = "fg-1_root", Uid = "fg-1_root", ImportDisabled = false }];
            Management discovered = CreateAdomManagement(0, "new-adom-uid", "adomA", 1);
            discovered.Devices =
            [
                new Device { Name = "fg-1_root", Uid = "fg-1_root" },
                new Device { Name = "fg-1_vdomNew", Uid = "fg-1_vdomNew" }
            ];
            List<Management> deltaManagements = [];

            List<Management> existingManagements = [existing];
            List<Management> discoveredManagements = [discovered];

            InvokeDiscoverManagementDetails(discovered, deltaManagements, existingManagements, discoveredManagements);

            Assert.That(deltaManagements, Has.Count.EqualTo(1));
            Assert.That(deltaManagements[0].Id, Is.EqualTo(7));
            Assert.That(deltaManagements[0].Delete, Is.False);
            Assert.That(deltaManagements[0].Devices, Has.Length.EqualTo(1));
            Assert.That(deltaManagements[0].Devices[0].Uid, Is.EqualTo("fg-1_vdomNew"));
            Assert.That(deltaManagements[0].Devices[0].Delete, Is.False);
        }

        [Test]
        public void DiscoverManagementDetails_ReportsNoChange_WhenOnlyAdomUidChanged()
        {
            Management existing = CreateAdomManagement(7, "old-adom-uid", "adomA", 1);
            existing.Devices = [new Device { Id = 10, Name = "fg-1_root", Uid = "fg-1_root", ImportDisabled = false }];
            Management discovered = CreateAdomManagement(0, "new-adom-uid", "adomA", 1);
            discovered.Devices = [new Device { Name = "fg-1_root", Uid = "fg-1_root" }];
            List<Management> deltaManagements = [];

            List<Management> existingManagements = [existing];
            List<Management> discoveredManagements = [discovered];

            InvokeDiscoverManagementDetails(discovered, deltaManagements, existingManagements, discoveredManagements);

            Assert.That(deltaManagements, Is.Empty);
        }

        [Test]
        public void DiscoverManagementDetails_TreatsAdomAsNew_WhenConfigPathMatchesOtherSuperManager()
        {
            Management existing = CreateAdomManagement(7, "old-adom-uid", "adomA", 2);
            Management discovered = CreateAdomManagement(0, "new-adom-uid", "adomA", 1);
            List<Management> deltaManagements = [];

            List<Management> existingManagements = [existing];
            List<Management> discoveredManagements = [discovered];

            InvokeDiscoverManagementDetails(discovered, deltaManagements, existingManagements, discoveredManagements);

            Assert.That(deltaManagements, Has.Count.EqualTo(1));
            Assert.That(deltaManagements[0], Is.SameAs(discovered));
            Assert.That(deltaManagements[0].Id, Is.EqualTo(0));
        }

        [Test]
        public void FindManagementIfExist_FindsDiscoveredAdom_ByConfigPath_WhenUidChanged()
        {
            Management existing = CreateAdomManagement(7, "old-adom-uid", "adomA", 1);
            Management discovered = CreateAdomManagement(0, "new-adom-uid", "adomA", 1);

            List<Management> discoveredManagements = [discovered];
            List<Management> existingManagements = [existing];

            Management? found = InvokeFindManagementIfExist(existing, discoveredManagements, existingManagements);

            Assert.That(found, Is.SameAs(discovered));
        }

        [Test]
        public void FindManagementIfExist_PrefersUidMatch_OverConfigPathMatch()
        {
            Management discovered = CreateAdomManagement(0, "adom-uid", "adomB", 1);
            Management sameName = CreateAdomManagement(7, "other-uid", "adomB", 1);
            Management sameUid = CreateAdomManagement(8, "adom-uid", "adomA", 1);

            List<Management> existingManagements = [sameName, sameUid];
            List<Management> discoveredManagements = [discovered];

            Management? found = InvokeFindManagementIfExist(discovered, existingManagements, discoveredManagements);

            Assert.That(found, Is.SameAs(sameUid));
        }

        [Test]
        public void FindManagementIfExist_DoesNotMatch_WhenConfigPathEmpty()
        {
            Management discovered = CreateAdomManagement(0, "new-adom-uid", "", 1);
            Management existing = CreateAdomManagement(7, "old-adom-uid", "", 1);

            List<Management> existingManagements = [existing];
            List<Management> discoveredManagements = [discovered];

            Management? found = InvokeFindManagementIfExist(discovered, existingManagements, discoveredManagements);

            Assert.That(found, Is.Null);
        }

        [Test]
        public void DiscoverManagementDetails_TreatsAdomAsNew_WhenOldNameReusedAfterRename()
        {
            // ADOM "adomA" (uid 1) was renamed to "adomB", then a new ADOM "adomA" (uid 2) was created
            Management existing = CreateAdomManagement(7, "adom-uid-1", "adomA", 1);
            Management renamed = CreateAdomManagement(0, "adom-uid-1", "adomB", 1);
            Management reusedName = CreateAdomManagement(0, "adom-uid-2", "adomA", 1);
            List<Management> deltaManagements = [];
            List<Management> existingManagements = [existing];
            List<Management> discoveredManagements = [renamed, reusedName];

            InvokeDiscoverManagementDetails(reusedName, deltaManagements, existingManagements, discoveredManagements);

            Assert.That(deltaManagements, Has.Count.EqualTo(1));
            Assert.That(deltaManagements[0], Is.SameAs(reusedName));
            Assert.That(deltaManagements[0].Id, Is.EqualTo(0));
        }

        [Test]
        public void FindManagementIfExist_SkipsConfigPathMatch_WhenCandidateClaimedByUid()
        {
            // stale DB entry "adomA" must not be kept alive by an ADOM that belongs to another DB entry by UID
            Management claimedByUid = CreateAdomManagement(7, "adom-uid-1", "adomB", 1);
            Management stale = CreateAdomManagement(8, "adom-uid-9", "adomA", 1);
            Management discovered = CreateAdomManagement(0, "adom-uid-1", "adomA", 1);
            List<Management> existingManagements = [claimedByUid, stale];
            List<Management> discoveredManagements = [discovered];

            Management? found = InvokeFindManagementIfExist(stale, discoveredManagements, existingManagements);

            Assert.That(found, Is.Null);
        }

        private static Management CreateAdomManagement(int id, string uid, string configPath, int superManagerId)
        {
            return new Management
            {
                Id = id,
                Name = $"fmgr_{configPath}",
                Uid = uid,
                ConfigPath = configPath,
                Hostname = "fmgr.example.test",
                Port = 443,
                SuperManagerId = superManagerId,
                ImportDisabled = false
            };
        }

        private static void InvokeDiscoverManagementDetails(Management discovered, List<Management> deltaManagements,
            List<Management> existingManagements, List<Management> discoveredManagements)
        {
            MethodInfo? discoverMethod = typeof(AutoDiscoveryBase)
                .GetMethod("DiscoverManagementDetails", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(discoverMethod, Is.Not.Null);
            object[] arguments = [discovered, deltaManagements, existingManagements, discoveredManagements, true, true];
            discoverMethod!.Invoke(null, arguments);
        }

        private static Management? InvokeFindManagementIfExist(Management management, List<Management> managementList,
            List<Management> sourceList)
        {
            MethodInfo? findMethod = typeof(AutoDiscoveryBase)
                .GetMethod("FindManagementIfExist", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(findMethod, Is.Not.Null);
            object[] arguments = [management, managementList, sourceList, true];
            return (Management?)findMethod!.Invoke(null, arguments);
        }
    }
}
