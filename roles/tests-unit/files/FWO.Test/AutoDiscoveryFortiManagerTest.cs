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
        public void CheckDeviceNotInMgmt_IgnoresName_WhenComparingByUidOnly()
        {
            Device existing = new() { Name = "gw-1_vdomA", Uid = "gw-1" };
            Device discovered = new() { Name = "gw-1", Uid = "gw-1" };
            Management management = new() { Devices = [existing] };

            MethodInfo? compareMethod = typeof(AutoDiscoveryBase)
                .GetMethod("CheckDeviceNotInMgmt", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(compareMethod, Is.Not.Null);

            bool notInManagement = (bool)compareMethod!.Invoke(null, [discovered, management, true])!;

            Assert.That(notInManagement, Is.False);
        }
    }
}
