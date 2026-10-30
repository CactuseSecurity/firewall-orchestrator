using System.Globalization;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Services.Workflow;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// Result of the consistency check of the implementation mode and object creation device specific settings.
    /// </summary>
    public sealed class ImplementationModeSettingsCheckResult
    {
        /// <summary>
        /// Levels whose implementation mode (objects or rules) is external ticket system while no external ticket
        /// system is selected: nobody implements their tasks.
        /// </summary>
        public List<DeviceSpecificNode> ExternalModeWithoutSystem { get; } = [];

        /// <summary>
        /// Levels selecting an external ticket system that neither they nor any level below them use: the selection
        /// alone has no effect.
        /// </summary>
        public List<DeviceSpecificNode> SystemWithoutExternalMode { get; } = [];

        /// <summary>
        /// Gateways that differ from their management in using the external ticket system for rules. An external
        /// ticket system takes over whole managements: a gateway set to it below a management that is not is
        /// implemented by nobody, and automatic or manual on a gateway below a management set to it has no effect.
        /// Without a value of its own a gateway inherits the mode of its management, so only gateway overrides count.
        /// </summary>
        public List<DeviceSpecificNode> GatewayDiffersInExternalMode { get; } = [];

        /// <summary>
        /// Findings of the object creation settings of the hierarchies of super- and sub-managements.
        /// </summary>
        public List<ObjectCreationFinding> ObjectCreationFindings { get; } = [];

        /// <summary>
        /// Whether the check found anything to show.
        /// </summary>
        public bool HasFindings => ExternalModeWithoutSystem.Count > 0 || SystemWithoutExternalMode.Count > 0
            || GatewayDiffersInExternalMode.Count > 0 || ObjectCreationFindings.Count > 0;
    }

    /// <summary>
    /// Checks the effective implementation mode, external ticket system and object creation settings of every level
    /// of the device specific settings tree.
    /// </summary>
    public static class ImplementationModeSettingsCheck
    {
        /// <summary>
        /// Loads the effective settings of all levels of the tree with one settings query and checks them.
        /// </summary>
        /// <param name="apiConnection">Connection with the rights to read the device specific settings.</param>
        /// <param name="root">Root of the device specific settings tree as shown on the settings page.</param>
        /// <param name="managements">All managements with their super-management.</param>
        public static async Task<ImplementationModeSettingsCheckResult> LoadAndCheck(ApiConnection apiConnection, DeviceSpecificNode root,
            IReadOnlyCollection<Management> managements)
        {
            List<DeviceSpecificNode> nodes = [.. root.SelfAndDescendants()];
            List<DeviceSpecificScopePath> paths = [.. nodes.Select(node => node.Path)];
            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);
            Dictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settingsByNode = [];
            for (int index = 0; index < nodes.Count; index++)
            {
                settingsByNode[nodes[index]] = settings[index];
            }
            return Check(settingsByNode, managements);
        }

        /// <summary>
        /// Checks the effective settings of the levels of the tree.
        /// </summary>
        /// <param name="settingsByNode">Effective settings of every level of the tree.</param>
        /// <param name="managements">All managements with their super-management.</param>
        public static ImplementationModeSettingsCheckResult Check(IReadOnlyDictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settingsByNode,
            IReadOnlyCollection<Management> managements)
        {
            ImplementationModeSettingsCheckResult result = new();
            foreach ((DeviceSpecificNode node, GlobalDeviceSpecificSettings settings) in settingsByNode)
            {
                if (node.Level == DeviceSpecificScopeType.Gateway)
                {
                    CheckGateway(node, settings, settingsByNode, result);
                    continue;
                }
                if (UsesExternalTicketSystem(settings) && settings.RerouteToExternalTicketSystem == null)
                {
                    result.ExternalModeWithoutSystem.Add(node);
                }
                if (settings.RerouteToExternalTicketSystem != null && !SubtreeUsesExternalTicketSystem(node, settingsByNode))
                {
                    result.SystemWithoutExternalMode.Add(node);
                }
            }
            result.ObjectCreationFindings.AddRange(ObjectCreationConsistencyCheck.Check(managements, SettingsByManagement(settingsByNode)));
            return result;
        }

        private static void CheckGateway(DeviceSpecificNode gateway, GlobalDeviceSpecificSettings settings,
            IReadOnlyDictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settingsByNode, ImplementationModeSettingsCheckResult result)
        {
            bool managementUsesExternal = gateway.Parent != null && settingsByNode.TryGetValue(gateway.Parent, out GlobalDeviceSpecificSettings? managementSettings)
                && managementSettings.UsesExternalTicketSystem(true);
            if (settings.UsesExternalTicketSystem(true) != managementUsesExternal)
            {
                result.GatewayDiffersInExternalMode.Add(gateway);
            }
        }

        private static bool UsesExternalTicketSystem(GlobalDeviceSpecificSettings settings)
        {
            return settings.UsesExternalTicketSystem(false) || settings.UsesExternalTicketSystem(true);
        }

        private static bool SubtreeUsesExternalTicketSystem(DeviceSpecificNode node, IReadOnlyDictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settingsByNode)
        {
            return node.SelfAndDescendants().Any(member => settingsByNode.TryGetValue(member, out GlobalDeviceSpecificSettings? settings)
                && UsesExternalTicketSystem(settings));
        }

        private static Dictionary<int, GlobalDeviceSpecificSettings> SettingsByManagement(IReadOnlyDictionary<DeviceSpecificNode, GlobalDeviceSpecificSettings> settingsByNode)
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settingsByManagement = [];
            foreach ((DeviceSpecificNode node, GlobalDeviceSpecificSettings settings) in settingsByNode.Where(entry => entry.Key.Level == DeviceSpecificScopeType.Management))
            {
                if (int.TryParse(node.Scope.ObjectKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out int managementId))
                {
                    settingsByManagement[managementId] = settings;
                }
            }
            return settingsByManagement;
        }
    }
}
