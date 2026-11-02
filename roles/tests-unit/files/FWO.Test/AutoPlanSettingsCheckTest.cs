using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the consistency check of the auto plan settings shown on the device specific settings page.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class AutoPlanSettingsCheckTest
    {
        private const int kManagementId = 20;
        private const int kGatewayId = 1;

        private static readonly Dictionary<string, bool> kNoPlanningPhase = new()
        {
            [nameof(WfTaskType.access)] = false,
            [nameof(WfTaskType.group_create)] = false
        };
        private static readonly Dictionary<string, bool> kPlanningPhaseEverywhere = new()
        {
            [nameof(WfTaskType.access)] = true,
            [nameof(WfTaskType.group_create)] = true
        };
        private static readonly List<DeviceSpecificConfigNodeData> kNoStoredNodes = [];
        private static readonly List<string> kAccessOnly = [nameof(WfTaskType.access)];
        private static readonly List<string> kAccessAndGroup = [nameof(WfTaskType.access), nameof(WfTaskType.group_create)];

        [Test]
        public void Check_DefaultsPlanRulesManuallyWithoutPlanningPhase()
        {
            AutoPlanSettingsCheckResult result = AutoPlanSettingsCheck.Check(CreateTree(), kNoStoredNodes, GlobalConst.kPathAnalysisAlgorithmNone, kNoPlanningPhase);

            Assert.Multiple(() =>
            {
                Assert.That(result.ManualWithoutPlanningPhase, Is.EqualTo(kAccessOnly));
                Assert.That(result.RulesAutomaticWithoutAlgorithm, Is.False);
            });
        }

        [Test]
        public void Check_RulesAutomaticOnGatewayWithoutAlgorithm_Warns()
        {
            List<DeviceSpecificConfigNodeData> stored = [Stored("gateway", kGatewayId.ToString(), DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true)];

            AutoPlanSettingsCheckResult result = AutoPlanSettingsCheck.Check(CreateTree(), stored, GlobalConst.kPathAnalysisAlgorithmNone, kPlanningPhaseEverywhere);

            Assert.Multiple(() =>
            {
                Assert.That(result.RulesAutomaticWithoutAlgorithm, Is.True);
                Assert.That(result.ManualWithoutPlanningPhase, Is.Empty);
            });
        }

        [Test]
        public void Check_RulesAutomaticWithAlgorithm_DoesNotWarn()
        {
            List<DeviceSpecificConfigNodeData> stored = [Stored("global", DeviceSpecificScopePath.GlobalObjectKey, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true)];

            AutoPlanSettingsCheckResult result = AutoPlanSettingsCheck.Check(CreateTree(), stored, GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree, kNoPlanningPhase);

            Assert.Multiple(() =>
            {
                Assert.That(result.RulesAutomaticWithoutAlgorithm, Is.False);
                Assert.That(result.ManualWithoutPlanningPhase, Is.Empty);
            });
        }

        [Test]
        public void Check_ManagementWithoutAutoPlan_ReportsAllTaskTypesWithoutPlanningPhase()
        {
            List<DeviceSpecificConfigNodeData> stored =
            [
                Stored("global", DeviceSpecificScopePath.GlobalObjectKey, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true),
                Stored("management", kManagementId.ToString(), DeviceSpecificSettingKeys.AutoPlanImplementation, false)
            ];

            AutoPlanSettingsCheckResult result = AutoPlanSettingsCheck.Check(CreateTree(), stored, GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree, kNoPlanningPhase);

            Assert.That(result.ManualWithoutPlanningPhase, Is.EqualTo(kAccessAndGroup));
        }

        private static DeviceSpecificConfigNodeData Stored(string nodeType, string objectKey, DeviceSpecificSettingKey<bool> key, bool value)
        {
            DeviceSpecificConfigNodeData node = new() { NodeType = nodeType, ObjectKey = objectKey };
            node.Values.Add(new DeviceSpecificConfigValueData { ConfigKey = key.DatabaseKey, ConfigValue = new JValue(value) });
            return node;
        }

        private static DeviceSpecificNode CreateTree()
        {
            Device gateway = new() { Id = kGatewayId, Name = "gw-1" };
            Management management = new() { Id = kManagementId, Name = "mgm", DeviceType = new DeviceType { Id = 9 }, Devices = [gateway] };
            DeviceSpecificScopePath globalPath = DeviceSpecificScopePath.Global();
            DeviceSpecificNode root = new() { Scope = globalPath.ToScopes()[^1], Path = globalPath, Name = "Global" };
            DeviceSpecificScopePath managementPath = DeviceSpecificScopePath.ForManagement(management);
            DeviceSpecificNode managementNode = new() { Scope = managementPath.ToScopes()[^1], Path = managementPath, Name = "mgm", Parent = root };
            DeviceSpecificScopePath gatewayPath = DeviceSpecificScopePath.ForGateway(management, gateway);
            managementNode.Children.Add(new DeviceSpecificNode { Scope = gatewayPath.ToScopes()[^1], Path = gatewayPath, Name = "gw-1", Parent = managementNode });
            root.Children.Add(managementNode);
            return root;
        }
    }
}
