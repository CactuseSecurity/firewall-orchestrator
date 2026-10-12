using FWO.Config.Api.Data;
using FWO.Data;
using FWO.DeviceAutoDiscovery;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Certificate checking is one switch per connection type. These tests pin the defaults and
    /// that each switch reaches the connections of its type.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ExternalCertificateChecksTest
    {
        [Test]
        public void ConfigData_Defaults_CheckEveryConnectionType()
        {
            ConfigData config = new();

            Assert.Multiple(() =>
            {
                Assert.That(config.ImportCheckCertificates, Is.True);
                Assert.That(config.EmailCheckCertificates, Is.True);
                Assert.That(config.ExtTicketSystemsCheckCertificates, Is.True);
            });
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void GetExternalCertificateChecks_MapsFirewallAndTicketSystemSwitches(bool firewallConnections, bool ticketSystems)
        {
            ConfigData config = new()
            {
                ImportCheckCertificates = firewallConnections,
                ExtTicketSystemsCheckCertificates = ticketSystems
            };

            ExternalCertificateChecks checks = config.GetExternalCertificateChecks();

            Assert.That(checks, Is.EqualTo(new ExternalCertificateChecks(firewallConnections, ticketSystems)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AutoDiscovery_KeepsFirewallSwitch(bool checkCertificates)
        {
            Management superManagement = new()
            {
                Name = "fmgr",
                DeviceType = new DeviceType { Id = 12, Name = "FortiManager" }
            };

            AutoDiscoveryBase discovery = new(superManagement, new SimulatedApiConnection(), checkCertificates);

            Assert.That(discovery.CheckCertificates, Is.EqualTo(checkCertificates));
        }

        [Test]
        public void ResolveCheckCertificates_UsesSwitchOfAddressedConnectionType()
        {
            ExternalCertificateChecks checks = new(FirewallConnections: false, TicketSystems: true);

            Assert.Multiple(() =>
            {
                Assert.That(FWO.ExternalSystems.CheckPoint.CheckPointClient.ResolveCheckCertificates(new Management { Hostname = "cp.example" }, checks), Is.False);
                Assert.That(FWO.ExternalSystems.CheckPoint.CheckPointClient.ResolveCheckCertificates(new Management { Hostname = " " }, checks), Is.True);
            });
        }
    }
}
