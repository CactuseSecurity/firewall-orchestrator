using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the hints of the device specific settings page about implementation modes, external ticket systems and
    /// object creation.
    /// </summary>
    [TestFixture]
    internal class ImplementationModeSettingsCheckTest
    {
        private const int kManagementId = 20;
        private const int kOtherManagementId = 21;
        private const int kSuperManagementId = 30;
        private const int kSubManagementId = 31;
        private const int kGatewayId = 1;
        private const int kExternalTicketSystemId = 2;
        private const long kGlobalNodeId = 1;
        private const long kDeviceTypeNodeId = 2;
        private const long kManagementNodeId = 3;

        private sealed class Tree
        {
            public required DeviceSpecificNode Root { get; init; }
            public required DeviceSpecificNode Management { get; init; }
            public required DeviceSpecificNode OtherManagement { get; init; }
            public required DeviceSpecificNode Gateway { get; init; }
            public required List<Management> Managements { get; init; }
        }

        private static Tree CreateTree()
        {
            DeviceType deviceType = new() { Id = 9, Name = "Check Point", Version = "R8x" };
            Device gateway = new() { Id = kGatewayId, Name = "gw-1" };
            Management management = new() { Id = kManagementId, Name = "cp1", DeviceType = deviceType, Devices = [gateway] };
            Management otherManagement = new() { Id = kOtherManagementId, Name = "cp2", DeviceType = deviceType };
            DeviceSpecificScopePath globalPath = DeviceSpecificScopePath.Global();
            DeviceSpecificNode root = new() { Scope = globalPath.ToScopes()[^1], Path = globalPath, Name = "Global" };
            DeviceSpecificNode managementNode = CreateChild(root, DeviceSpecificScopePath.ForManagement(management), "cp1");
            DeviceSpecificNode otherManagementNode = CreateChild(root, DeviceSpecificScopePath.ForManagement(otherManagement), "cp2");
            DeviceSpecificNode gatewayNode = CreateChild(managementNode, DeviceSpecificScopePath.ForGateway(management, gateway), "gw-1");
            return new Tree
            {
                Root = root,
                Management = managementNode,
                OtherManagement = otherManagementNode,
                Gateway = gatewayNode,
                Managements = [management, otherManagement]
            };
        }

        private static DeviceSpecificNode CreateChild(DeviceSpecificNode parent, DeviceSpecificScopePath path, string name)
        {
            DeviceSpecificNode child = new() { Scope = path.ToScopes()[^1], Path = path, Name = name, Parent = parent };
            parent.Children.Add(child);
            return child;
        }

        private static Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> DefaultSettings(Tree tree)
        {
            return tree.Root.SelfAndDescendants().ToDictionary(node => node, _ => new GlobalDeviceSpecificSettings());
        }

        private static void UseExternalTicketSystem(GlobalDeviceSpecificSettings settings, int? system)
        {
            settings.ImplementationModeOverrideForRules = ProvisioningRuleImplementationMode.RerouteToExternalTicketSystem;
            settings.RerouteToExternalTicketSystem = system;
        }

        [Test]
        public void DefaultSettings_GiveNoFinding()
        {
            Tree tree = CreateTree();

            Assert.That(ImplementationModeSettingsCheck.Check(DefaultSettings(tree), tree.Managements).HasFindings, Is.False);
        }

        [Test]
        public void ExternalModeWithoutSystem_IsReported()
        {
            Tree tree = CreateTree();
            Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settings = DefaultSettings(tree);
            UseExternalTicketSystem(settings[tree.Management], null);
            UseExternalTicketSystem(settings[tree.Gateway], null);

            ImplementationModeSettingsCheckResult result = ImplementationModeSettingsCheck.Check(settings, tree.Managements);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExternalModeWithoutSystem.Single(), Is.SameAs(tree.Management));
                Assert.That(result.GatewayDiffersInExternalMode, Is.Empty, "the gateway follows its management");
            });
        }

        [Test]
        public void SystemWithoutExternalMode_IsReportedOnlyWhenNoLevelBelowUsesIt()
        {
            Tree tree = CreateTree();
            Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settings = DefaultSettings(tree);
            foreach (GlobalDeviceSpecificSettings levelSettings in settings.Values)
            {
                levelSettings.RerouteToExternalTicketSystem = kExternalTicketSystemId;
            }
            UseExternalTicketSystem(settings[tree.Management], kExternalTicketSystemId);
            UseExternalTicketSystem(settings[tree.Gateway], kExternalTicketSystemId);

            ImplementationModeSettingsCheckResult result = ImplementationModeSettingsCheck.Check(settings, tree.Managements);

            Assert.That(result.SystemWithoutExternalMode.Single(), Is.SameAs(tree.OtherManagement), "global is used below, cp1 itself");
        }

        [Test]
        public void ExternalModeOnGatewayBelowNonExternalManagement_IsReported()
        {
            Tree tree = CreateTree();
            Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settings = DefaultSettings(tree);
            UseExternalTicketSystem(settings[tree.Gateway], kExternalTicketSystemId);

            ImplementationModeSettingsCheckResult result = ImplementationModeSettingsCheck.Check(settings, tree.Managements);

            Assert.That(result.GatewayDiffersInExternalMode.Single(), Is.SameAs(tree.Gateway));
        }

        [Test]
        public void GatewayOverrideBelowExternalManagement_IsReported()
        {
            Tree tree = CreateTree();
            Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settings = DefaultSettings(tree);
            UseExternalTicketSystem(settings[tree.Management], kExternalTicketSystemId);
            settings[tree.Gateway].RerouteToExternalTicketSystem = kExternalTicketSystemId;
            settings[tree.Gateway].ImplementationModeOverrideForRules = ProvisioningRuleImplementationMode.Manual;

            ImplementationModeSettingsCheckResult result = ImplementationModeSettingsCheck.Check(settings, tree.Managements);

            Assert.That(result.GatewayDiffersInExternalMode.Single(), Is.SameAs(tree.Gateway), "the external ticket system takes over the whole management");
        }

        [Test]
        public void AutomaticGatewayBelowManualManagement_IsNotReported()
        {
            Tree tree = CreateTree();
            Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settings = DefaultSettings(tree);
            settings[tree.Management].ImplementationModeOverrideForRules = ProvisioningRuleImplementationMode.Manual;

            ImplementationModeSettingsCheckResult result = ImplementationModeSettingsCheck.Check(settings, tree.Managements);

            Assert.That(result.HasFindings, Is.False, "the deepest level wins between automatic and manual");
        }

        [Test]
        public void ObjectCreationFindings_UseTheSettingsOfTheManagementLevels()
        {
            DeviceType superType = new() { Id = 12, Name = "FortiManager", Version = "5ff" };
            DeviceType subType = new() { Id = 11, Name = "FortiADOM", Version = "5ff" };
            Management superManagement = new() { Id = kSuperManagementId, Name = "fortimgr100", DeviceType = superType, IsSupermanager = true };
            Management subManagement = new() { Id = kSubManagementId, Name = "adom1", DeviceType = subType, SuperManagerId = kSuperManagementId };
            DeviceSpecificScopePath globalPath = DeviceSpecificScopePath.Global();
            DeviceSpecificNode root = new() { Scope = globalPath.ToScopes()[^1], Path = globalPath, Name = "Global" };
            DeviceSpecificNode superNode = CreateChild(root, DeviceSpecificScopePath.ForManagement(superManagement), "fortimgr100");
            DeviceSpecificNode subNode = CreateChild(root, DeviceSpecificScopePath.ForManagement(subManagement), "adom1");
            Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settings = new()
            {
                { root, new GlobalDeviceSpecificSettings() },
                { superNode, new GlobalDeviceSpecificSettings() },
                { subNode, new GlobalDeviceSpecificSettings { AddressObjectCreation = ProvisioningObjectCreationMode.Submanager } }
            };
            List<Management> managements = [superManagement, subManagement];

            ImplementationModeSettingsCheckResult result = ImplementationModeSettingsCheck.Check(settings, managements);

            Assert.That(result.ObjectCreationFindings.Single().Type, Is.EqualTo(ObjectCreationFindingType.SuperSettingWithoutEffect));
        }

        [Test]
        public async Task LoadAndCheck_ReadsTheEffectiveSettingsOfTheTree()
        {
            Tree tree = CreateTree();
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            api.AddNode(kDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, "9", kGlobalNodeId);
            api.AddNode(kManagementNodeId, DeviceSpecificScopeType.Management, kManagementId.ToString(), kDeviceTypeNodeId);
            api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, ProvisioningRuleImplementationMode.RerouteToExternalTicketSystem);

            ImplementationModeSettingsCheckResult result = await ImplementationModeSettingsCheck.LoadAndCheck(api, tree.Root, tree.Managements);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExternalModeWithoutSystem.Single(), Is.SameAs(tree.Management));
                Assert.That(result.GatewayDiffersInExternalMode, Is.Empty, "the gateway inherits the mode of its management");
            });
        }
    }
}
