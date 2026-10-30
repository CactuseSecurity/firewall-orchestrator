using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.Workflow;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// Result of the consistency check of the auto plan device specific settings.
    /// </summary>
    public sealed class AutoPlanSettingsCheckResult
    {
        /// <summary>
        /// True when at least one level creates the implementation tasks of rules automatically while no path
        /// analysis algorithm is selected: rule tasks without entered devices then get one task without gateway.
        /// </summary>
        public bool RulesAutomaticWithoutAlgorithm { get; set; }

        /// <summary>
        /// Task types planned manually on at least one level although their planning phase is not active, so their
        /// tickets would get no implementation task.
        /// </summary>
        public List<string> ManualWithoutPlanningPhase { get; set; } = [];
    }

    /// <summary>
    /// Checks the effective auto plan settings of every level of the device specific settings tree against the path
    /// analysis algorithm and the planning phases of the workflow.
    /// </summary>
    public static class AutoPlanSettingsCheck
    {
        private const string kLogTitle = "Auto Plan Settings Check";

        /// <summary>
        /// Depth limit of the tree walk; the hierarchy has four levels.
        /// </summary>
        private const int kMaxDepth = 100;

        /// <summary>
        /// Loads the stored settings of the levels of the tree and the planning phases and checks them.
        /// </summary>
        /// <param name="apiConnection">Connection with the rights to read the device specific settings.</param>
        /// <param name="root">Root of the device specific settings tree as shown on the settings page.</param>
        /// <param name="pathAnalysisAlgorithm">Configured path analysis algorithm.</param>
        public static async Task<AutoPlanSettingsCheckResult> LoadAndCheck(ApiConnection apiConnection, DeviceSpecificNode root, long pathAnalysisAlgorithm)
        {
            List<DeviceSpecificNode> treeNodes = [.. root.SelfAndDescendants()];
            List<DeviceSpecificConfigNodeData> storedNodes = await apiConnection.SendQueryAsync<List<DeviceSpecificConfigNodeData>>(
                DeviceSpecificSettingsQueries.getNodesByNaturalKeys, new
                {
                    globalKeys = ObjectKeys(treeNodes, DeviceSpecificScopeType.Global),
                    deviceTypeKeys = ObjectKeys(treeNodes, DeviceSpecificScopeType.DeviceType),
                    managementKeys = ObjectKeys(treeNodes, DeviceSpecificScopeType.Management),
                    gatewayKeys = ObjectKeys(treeNodes, DeviceSpecificScopeType.Gateway)
                }) ?? [];
            return Check(root, storedNodes, pathAnalysisAlgorithm, await LoadPlanningPhases(apiConnection));
        }

        private static List<string> ObjectKeys(List<DeviceSpecificNode> nodes, DeviceSpecificScopeType level)
        {
            return [.. nodes.Where(node => node.Level == level).Select(node => node.Scope.ObjectKey).Distinct()];
        }

        /// <summary>
        /// Returns whether the planning phase is active, by task type. If the matrices cannot be read, nothing is
        /// returned, so no task type is reported. New interface tasks are left out: their implementation task is
        /// created with the request task, independently of the auto plan settings.
        /// </summary>
        private static async Task<Dictionary<string, bool>> LoadPlanningPhases(ApiConnection apiConnection)
        {
            try
            {
                StateMatrixDict stateMatrices = new();
                await stateMatrices.Init(WorkflowPhases.request, apiConnection);
                return stateMatrices.Matrices
                    .Where(entry => !IsIndependentOfAutoPlan(entry.Key))
                    .ToDictionary(entry => entry.Key,
                        entry => entry.Value.PhaseActive.TryGetValue(WorkflowPhases.planning, out bool active) && active);
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kLogTitle, $"Planning phases could not be read: {exception.Message}");
                return new Dictionary<string, bool>();
            }
        }

        /// <summary>
        /// Walks the tree with the inherited values and collects the inconsistencies.
        /// </summary>
        /// <param name="root">Root of the device specific settings tree.</param>
        /// <param name="storedNodes">Stored nodes with their auto plan overrides.</param>
        /// <param name="pathAnalysisAlgorithm">Configured path analysis algorithm.</param>
        /// <param name="planningActive">Whether the planning phase is active, by task type.</param>
        public static AutoPlanSettingsCheckResult Check(DeviceSpecificNode root, List<DeviceSpecificConfigNodeData> storedNodes,
            long pathAnalysisAlgorithm, IReadOnlyDictionary<string, bool> planningActive)
        {
            Dictionary<(string NodeType, string ObjectKey), DeviceSpecificConfigNodeData> nodesByKey = [];
            foreach (DeviceSpecificConfigNodeData node in storedNodes)
            {
                nodesByKey[(node.NodeType, node.ObjectKey)] = node;
            }
            GlobalDeviceSpecificSettings defaults = new();
            LevelSummary summary = new();
            Walk(root, defaults.AutoPlanImplementation, defaults.AutoPlanImplementationForRules, nodesByKey, summary, 0);

            AutoPlanSettingsCheckResult result = new()
            {
                RulesAutomaticWithoutAlgorithm = summary.AnyRulesAutomatic && pathAnalysisAlgorithm == GlobalConst.kPathAnalysisAlgorithmNone
            };
            foreach ((string taskType, bool active) in planningActive.Where(entry => !IsIndependentOfAutoPlan(entry.Key)).OrderBy(entry => entry.Key))
            {
                bool manual = WfHandler.IsRuleTaskType(taskType) ? summary.AnyRulesManual : summary.AnyOthersManual;
                if (manual && !active)
                {
                    result.ManualWithoutPlanningPhase.Add(taskType);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns whether the implementation tasks of a task type do not depend on the auto plan settings: the master
        /// matrix has no tasks of its own, new interface tasks get their implementation task with the request task.
        /// </summary>
        private static bool IsIndependentOfAutoPlan(string taskType)
        {
            return taskType == WfTaskType.master.ToString() || taskType == WfTaskType.new_interface.ToString();
        }

        private sealed class LevelSummary
        {
            public bool AnyRulesAutomatic { get; set; }
            public bool AnyRulesManual { get; set; }
            public bool AnyOthersManual { get; set; }
        }

        private static void Walk(DeviceSpecificNode node, bool inheritedAuto, bool inheritedRules,
            Dictionary<(string NodeType, string ObjectKey), DeviceSpecificConfigNodeData> nodesByKey, LevelSummary summary, int depth)
        {
            if (depth > kMaxDepth)
            {
                Log.WriteWarning(kLogTitle, "Device specific settings tree deeper than expected, check stopped.");
                return;
            }
            bool auto = inheritedAuto;
            bool rules = inheritedRules;
            if (nodesByKey.TryGetValue((DeviceSpecificSettingsScopeFactory.ToNodeType(node.Level), node.Scope.ObjectKey), out DeviceSpecificConfigNodeData? stored))
            {
                auto = ReadOverride(stored, DeviceSpecificSettingKeys.AutoPlanImplementation, auto);
                rules = ReadOverride(stored, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, rules);
            }
            summary.AnyRulesAutomatic |= auto && rules;
            summary.AnyRulesManual |= !(auto && rules);
            summary.AnyOthersManual |= !auto;
            foreach (DeviceSpecificNode child in node.Children)
            {
                Walk(child, auto, rules, nodesByKey, summary, depth + 1);
            }
        }

        private static bool ReadOverride(DeviceSpecificConfigNodeData node, DeviceSpecificSettingKey<bool> key, bool inherited)
        {
            DeviceSpecificConfigValueData? value = node.Values.FirstOrDefault(value => value.ConfigKey == key.DatabaseKey);
            if (value == null)
            {
                return inherited;
            }
            try
            {
                return DeviceSpecificSettingValueSerializer.Deserialize(key, value.ConfigValue);
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kLogTitle, $"Invalid stored value of {key.DatabaseKey} on node {node.Id}: {exception.Message}");
                return inherited;
            }
        }
    }
}
