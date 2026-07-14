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
