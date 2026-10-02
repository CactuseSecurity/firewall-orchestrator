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

            MethodInfo? discoverMethod = typeof(AutoDiscoveryBase)
                .GetMethod("DiscoverManagementDetails", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(discoverMethod, Is.Not.Null);

            discoverMethod!.Invoke(null, [discovered, deltaManagements, new List<Management> { existing }, true, true]);

            Assert.That(deltaManagements, Is.Empty);
        }
    }
}
