using FWO.Data.DeviceSpecificSettings;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class DeviceSpecificSettingsTest
    {
        [Test]
        public void NewSettings_ScopeTypeMatchesTheSettingsClass()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new GlobalDeviceSpecificSettings().Scope.ScopeType, Is.EqualTo(DeviceSpecificScopeType.Global));
                Assert.That(new DeviceTypeDeviceSpecificSettings().Scope.ScopeType, Is.EqualTo(DeviceSpecificScopeType.DeviceType));
                Assert.That(new ManagementDeviceSpecificSettings().Scope.ScopeType, Is.EqualTo(DeviceSpecificScopeType.Management));
                Assert.That(new GatewayDeviceSpecificSettings().Scope.ScopeType, Is.EqualTo(DeviceSpecificScopeType.Gateway));
            });
        }

        [Test]
        public void NewSettings_CarryTheDocumentedConstructorDefaults()
        {
            GatewayDeviceSpecificSettings settings = new();

            Assert.Multiple(() =>
            {
                Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Automatic));
                Assert.That(settings.InstallOn, Is.EqualTo(ProvisioningInstallOn.Any));
                Assert.That(settings.RuleLoggingOverride, Is.EqualTo(ProvisioningLoggingMode.Log));
                Assert.That(settings.ServiceObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
                Assert.That(settings.AddressObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
                Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.AlwaysAccess));
                Assert.That(settings.Templates, Is.EqualTo("{}"));
                Assert.That(settings.PositioningAlgorithm, Is.EqualTo(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase));
                Assert.That(settings.CommonServiceSeparation, Is.False);
                Assert.That(settings.SecurityProfiles, Is.Empty);
                Assert.That(settings.RuleZoneFromOverride, Is.EqualTo("ANY"));
                Assert.That(settings.RuleZoneToOverride, Is.EqualTo("ANY"));
            });
        }

        [Test]
        public void Settings_CanRepresentTicketProvisioningValues()
        {
            List<string> securityProfiles =
            [
                "Strict",
                "ScanAll"
            ];

            GatewayDeviceSpecificSettings settings = new()
            {
                Scope = new DeviceSpecificSettingsScope
                {
                    ScopeType = DeviceSpecificScopeType.Gateway,
                    ObjectKey = "fortigate-1",
                    DisplayName = "FortiGate 1",
                    NodeId = 5,
                    ParentNodeId = 4
                },
                ImplementationMode = ProvisioningImplementationMode.Automatic,
                InstallOn = ProvisioningInstallOn.Any,
                RuleLoggingOverride = ProvisioningLoggingMode.LogTrack,
                ServiceObjectCreation = ProvisioningObjectCreationMode.Supermanager,
                AddressObjectCreation = ProvisioningObjectCreationMode.Submanager,
                RuleType = ProvisioningRuleType.HandleAccessNatIps,
                Templates = "{}",
                PositioningAlgorithm = ProvisioningPositioningAlgorithm.CheckPointEndOfAppSectionDistinguishCommonServices,
                CommonServiceSeparation = true,
                SecurityProfiles = securityProfiles,
                RuleZoneFromOverride = "ANY",
                RuleZoneToOverride = "ANY"
            };

            Assert.Multiple(() =>
            {
                Assert.That(settings.Scope.ScopeType, Is.EqualTo(DeviceSpecificScopeType.Gateway));
                Assert.That(settings.Scope.ObjectKey, Is.EqualTo("fortigate-1"));
                Assert.That(settings.Scope.ParentNodeId, Is.EqualTo(4));
                Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Automatic));
                Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.HandleAccessNatIps));
                Assert.That(settings.SecurityProfiles, Is.EqualTo(securityProfiles));
                Assert.That(settings.RuleZoneFromOverride, Is.EqualTo("ANY"));
            });
        }
    }
}
