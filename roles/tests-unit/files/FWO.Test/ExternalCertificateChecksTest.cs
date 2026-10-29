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
        private static readonly List<string> kAllCertificateSwitches =
        [
            ConfigData.kImportCheckCertificatesKey,
            ConfigData.kExtTicketSystemsCheckCertificatesKey,
            ConfigData.kEmailCheckCertificatesKey
        ];

        [Test]
        public void ConfigData_Defaults_CheckEveryConnectionType()
        {
            ConfigData config = new();

            Assert.Multiple(() =>
            {
                Assert.That(config.ImportCheckCertificates, Is.True);
                Assert.That(config.EmailCheckCertificates, Is.True);
                Assert.That(config.ExtTicketSystemsCheckCertificates, Is.True);
                Assert.That(config.ImportSuppressCertificateWarnings, Is.False);
            });
        }

        [Test]
        public void GetUncheckedCertificateSwitches_IsEmptyWhileEveryCheckIsOn()
        {
            Assert.That(new ConfigData().GetUncheckedCertificateSwitches(), Is.Empty);
        }

        [Test]
        public void GetUncheckedCertificateSwitches_ListsEverySwitchThatIsOff()
        {
            ConfigData config = new()
            {
                ImportCheckCertificates = false,
                ExtTicketSystemsCheckCertificates = false,
                EmailCheckCertificates = false
            };

            Assert.That(config.GetUncheckedCertificateSwitches(), Is.EqualTo(kAllCertificateSwitches));
        }

        [TestCase(false, true, true, ConfigData.kImportCheckCertificatesKey)]
        [TestCase(true, false, true, ConfigData.kExtTicketSystemsCheckCertificatesKey)]
        [TestCase(true, true, false, ConfigData.kEmailCheckCertificatesKey)]
        public void GetUncheckedCertificateSwitches_ListsOnlyTheSwitchThatIsOff(bool firewalls, bool ticketSystems, bool email, string expectedKey)
        {
            ConfigData config = new()
            {
                ImportCheckCertificates = firewalls,
                ExtTicketSystemsCheckCertificates = ticketSystems,
                EmailCheckCertificates = email
            };

            Assert.That(config.GetUncheckedCertificateSwitches().Single(), Is.EqualTo(expectedKey));
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
