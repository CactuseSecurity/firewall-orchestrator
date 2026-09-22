using FWO.Data.Provisioning;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ProvisioningSettingsTest
    {
        [Test]
        public void NewSettings_UseExpectedDefaults()
        {
            GlobalProvisioningSettings globalSettings = new();
            DeviceTypeProvisioningSettings deviceTypeSettings = new();

            Assert.That(globalSettings.Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.Global));
            Assert.That(globalSettings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.FwoAuto));
            Assert.That(globalSettings.InstallOn, Is.EqualTo("ANY"));
            Assert.That(globalSettings.PathAnalysisAlgorithm, Is.EqualTo(ProvisioningPathAnalysisAlgorithm.StaticListsPerSubnet));
            Assert.That(globalSettings.Logging, Is.EqualTo(ProvisioningLoggingMode.Log));
            Assert.That(globalSettings.ServiceObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(globalSettings.AddressObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(globalSettings.RuleType, Is.EqualTo(ProvisioningRuleType.AlwaysAccess));
            Assert.That(globalSettings.Templates, Is.Empty);

            Assert.That(deviceTypeSettings.Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.DeviceType));
            Assert.That(deviceTypeSettings.PositioningAlgorithm, Is.EqualTo(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase));
            Assert.That(deviceTypeSettings.RuleCategory, Is.EqualTo(ProvisioningRuleCategory.App));
            Assert.That(deviceTypeSettings.SecurityProfiles, Is.Empty);
            Assert.That(deviceTypeSettings.ZoneFrom, Is.EqualTo("ANY"));
            Assert.That(deviceTypeSettings.ZoneTo, Is.EqualTo("ANY"));
        }

        [Test]
        public void Settings_CanRepresentTicketProvisioningValues()
        {
            List<string> securityProfiles = new()
            {
                "Strict",
                "ScanAll"
            };

            GatewayProvisioningSettings settings = new()
            {
                Scope = new ProvisioningSettingsScope
                {
                    ScopeType = ProvisioningScopeType.Gateway,
                    ObjectKey = "fortigate-1",
                    DisplayName = "FortiGate 1",
                    NodeId = 5,
                    ParentNodeId = 4
                },
                ImplementationMode = ProvisioningImplementationMode.FwoAuto,
                InstallOn = "ANY",
                PathAnalysisAlgorithm = ProvisioningPathAnalysisAlgorithm.StaticListsPerSubnet,
                Logging = ProvisioningLoggingMode.LogTrack,
                ServiceObjectCreation = ProvisioningObjectCreationMode.Supermanager,
                AddressObjectCreation = ProvisioningObjectCreationMode.Submanager,
                RuleType = ProvisioningRuleType.HandleAccessNatIps,
                Templates = "{}",
                PositioningAlgorithm = ProvisioningPositioningAlgorithm.CheckPointEndOfAppSectionDistinguishCommonServices,
                RuleCategory = ProvisioningRuleCategory.CommonService,
                SecurityProfiles = securityProfiles,
                ZoneFrom = "ANY",
                ZoneTo = "ANY"
            };

            Assert.That(settings.Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.Gateway));
            Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.FwoAuto));
            Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.HandleAccessNatIps));
            Assert.That(settings.SecurityProfiles, Is.EqualTo(securityProfiles));
            Assert.That(settings.ZoneFrom, Is.EqualTo("ANY"));
        }
    }
}
