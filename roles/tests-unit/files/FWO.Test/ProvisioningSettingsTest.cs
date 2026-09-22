using FWO.Data.Provisioning;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ProvisioningSettingsTest
    {
        [Test]
        public void NewSettings_DefaultControlledValuesAreUndefined()
        {
            GlobalProvisioningSettings settings = new();

            Assert.That(settings.Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.Undefined));
            Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Undefined));
            Assert.That(settings.PathAnalysisAlgorithm, Is.EqualTo(ProvisioningPathAnalysisAlgorithm.Undefined));
            Assert.That(settings.Logging, Is.EqualTo(ProvisioningLoggingMode.Undefined));
            Assert.That(settings.ServiceObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Undefined));
            Assert.That(settings.AddressObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Undefined));
            Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.Undefined));
            Assert.That(settings.PositioningAlgorithm, Is.EqualTo(ProvisioningPositioningAlgorithm.Undefined));
            Assert.That(settings.RuleCategory, Is.EqualTo(ProvisioningRuleCategory.Undefined));
            Assert.That(settings.SecurityProfiles, Is.Empty);
        }

        [Test]
        public void Settings_CanRepresentTicketProvisioningValues()
        {
            List<string> securityProfiles = new()
            {
                "Strict",
                "ScanAll"
            };

            GlobalProvisioningSettings settings = new()
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
