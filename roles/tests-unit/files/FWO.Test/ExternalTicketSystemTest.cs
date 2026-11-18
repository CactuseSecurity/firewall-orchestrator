using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests that the Tufin SecureChange system of a management is built from its device specific settings.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ExternalTicketSystemTest
    {
        private const string kUrl = "https://securechange.example/securechangeworkflow/api/securechange/";
        private const int kCredentialId = 7;

        [Test]
        public void FromSecureChangeSettings_TakesConnectionTimeoutAndRetries()
        {
            GlobalDeviceSpecificSettings settings = new()
            {
                ScUrl = kUrl,
                ScCredential = kCredentialId,
                ScLookupRequesterId = true,
                RequestResponseTimeout = 60,
                RequestMaxAttempts = 4,
                RequestCyclesBetweenAttempts = 9
            };

            ExternalTicketSystem system = ExternalTicketSystem.FromSecureChangeSettings(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(system.IsTufinSecureChange(), Is.True);
                Assert.That(system.Name, Is.EqualTo("Tufin SecureChange"));
                Assert.That(system.Url, Is.EqualTo(kUrl));
                Assert.That(system.CredentialId, Is.EqualTo(kCredentialId));
                Assert.That(system.LookupRequesterId, Is.True);
                Assert.That(system.ResponseTimeout, Is.EqualTo(60));
                Assert.That(system.MaxAttempts, Is.EqualTo(4));
                Assert.That(system.CyclesBetweenAttempts, Is.EqualTo(9));
                Assert.That(system.Authorization, Is.Null);
                Assert.That(system.Templates, Is.Empty);
            }
        }

        [Test]
        public void FromSecureChangeSettings_WithoutConnectionHasNoUrlAndNoCredential()
        {
            ExternalTicketSystem system = ExternalTicketSystem.FromSecureChangeSettings(new GlobalDeviceSpecificSettings());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(system.Url, Is.Empty);
                Assert.That(system.CredentialId, Is.Null);
                Assert.That(system.LookupRequesterId, Is.False);
                Assert.That(system.MaxAttempts, Is.EqualTo(GlobalDeviceSpecificSettings.kDefaultRequestMaxAttempts));
            }
        }
    }
}
