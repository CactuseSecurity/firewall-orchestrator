using FWO.Data.Provisioning;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ProvisioningSettingsTest
    {
        [Test]
        public void NewSettings_ScopeTypeMatchesTheSettingsClass()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new GlobalProvisioningSettings().Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.Global));
                Assert.That(new DeviceTypeProvisioningSettings().Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.DeviceType));
                Assert.That(new ManagementProvisioningSettings().Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.Management));
                Assert.That(new GatewayProvisioningSettings().Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.Gateway));
            });
        }

        [Test]
        public void NewSettings_CarryTheDocumentedConstructorDefaults()
        {
            GatewayProvisioningSettings settings = new();

            Assert.Multiple(() =>
            {
                Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.FwoAuto));
                Assert.That(settings.InstallOn, Is.EqualTo("ANY"));
                Assert.That(settings.PathAnalysisAlgorithm, Is.EqualTo(ProvisioningPathAnalysisAlgorithm.StaticListsPerSubnet));
                Assert.That(settings.Logging, Is.EqualTo(ProvisioningLoggingMode.Log));
                Assert.That(settings.ServiceObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
                Assert.That(settings.AddressObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
                Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.AlwaysAccess));
                Assert.That(settings.Templates, Is.Empty);
                Assert.That(settings.PositioningAlgorithm, Is.EqualTo(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase));
                Assert.That(settings.RuleCategory, Is.EqualTo(ProvisioningRuleCategory.App));
                Assert.That(settings.SecurityProfiles, Is.Empty);
                Assert.That(settings.ZoneFrom, Is.EqualTo("ANY"));
                Assert.That(settings.ZoneTo, Is.EqualTo("ANY"));
            });
        }

        /// <summary>
        /// Every controlled value can be reset to Undefined, which is what the settings UI uses to mean
        /// "not set at this scope - inherit it from the parent scope".
        /// </summary>
        [Test]
        public void Settings_CanExpressUndefinedForEveryControlledValue()
        {
            GatewayProvisioningSettings settings = new()
            {
                ImplementationMode = ProvisioningImplementationMode.Undefined,
                PathAnalysisAlgorithm = ProvisioningPathAnalysisAlgorithm.Undefined,
                Logging = ProvisioningLoggingMode.Undefined,
                ServiceObjectCreation = ProvisioningObjectCreationMode.Undefined,
                AddressObjectCreation = ProvisioningObjectCreationMode.Undefined,
                RuleType = ProvisioningRuleType.Undefined,
                PositioningAlgorithm = ProvisioningPositioningAlgorithm.Undefined,
                RuleCategory = ProvisioningRuleCategory.Undefined,
                SecurityProfiles = []
            };

            Assert.Multiple(() =>
            {
                Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Undefined));
                Assert.That(settings.PathAnalysisAlgorithm, Is.EqualTo(ProvisioningPathAnalysisAlgorithm.Undefined));
                Assert.That(settings.Logging, Is.EqualTo(ProvisioningLoggingMode.Undefined));
                Assert.That(settings.ServiceObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Undefined));
                Assert.That(settings.AddressObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Undefined));
                Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.Undefined));
                Assert.That(settings.PositioningAlgorithm, Is.EqualTo(ProvisioningPositioningAlgorithm.Undefined));
                Assert.That(settings.RuleCategory, Is.EqualTo(ProvisioningRuleCategory.Undefined));
                Assert.That(settings.SecurityProfiles, Is.Empty);
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

            Assert.Multiple(() =>
            {
                Assert.That(settings.Scope.ScopeType, Is.EqualTo(ProvisioningScopeType.Gateway));
                Assert.That(settings.Scope.ObjectKey, Is.EqualTo("fortigate-1"));
                Assert.That(settings.Scope.ParentNodeId, Is.EqualTo(4));
                Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.FwoAuto));
                Assert.That(settings.RuleType, Is.EqualTo(ProvisioningRuleType.HandleAccessNatIps));
                Assert.That(settings.SecurityProfiles, Is.EqualTo(securityProfiles));
                Assert.That(settings.ZoneFrom, Is.EqualTo("ANY"));
            });
        }
    }
}
