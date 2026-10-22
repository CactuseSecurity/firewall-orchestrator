using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Resolves the effective auto plan settings (autoPlanImplementation, autoPlanImplementationForRules) of gateways
    /// and managements along the device specific settings hierarchy.
    /// </summary>
    /// <remarks>
    /// Reads the device specific settings tables, which the workflow roles may not read. It runs in the state action
    /// "Create implementation tasks", which is executed in the middleware with the rights of the middleware server.
    /// </remarks>
    public class ImplTaskAutoPlanResolver(ApiConnection apiConnection)
    {
        /// <summary>
        /// Returns the effective settings of the global level and of every requested management and gateway that
        /// exists, by scope type and id (null for the global level). Unknown ids are left out.
        /// </summary>
        /// <param name="deviceIds">Gateways to resolve.</param>
        /// <param name="managementIds">Managements to resolve.</param>
        public async Task<Dictionary<(DeviceSpecificScopeType ScopeType, int? Id), GlobalDeviceSpecificSettings>> Resolve(
            IReadOnlyCollection<int> deviceIds, IReadOnlyCollection<int> managementIds)
        {
            DeviceSpecificSettingsManager settingsManager = new(apiConnection);
            DeviceSpecificSettingsLevel<GlobalDeviceSpecificSettings> globalLevel =
                await settingsManager.LoadLevelAsync<GlobalDeviceSpecificSettings>(DeviceSpecificScopePath.Global());
            Dictionary<(DeviceSpecificScopeType ScopeType, int? Id), GlobalDeviceSpecificSettings> settings = new()
            {
                [(DeviceSpecificScopeType.Global, null)] = globalLevel.Settings
            };
            if (deviceIds.Count == 0 && managementIds.Count == 0)
            {
                return settings;
            }

            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementsForAutoPlan,
                new { managementIds = managementIds.Distinct().ToList(), deviceIds = deviceIds.Distinct().ToList() }) ?? [];
            foreach (Management management in managements)
            {
                if (managementIds.Contains(management.Id))
                {
                    DeviceSpecificSettingsLevel<ManagementDeviceSpecificSettings> level =
                        await settingsManager.LoadLevelAsync<ManagementDeviceSpecificSettings>(DeviceSpecificScopePath.ForManagement(management));
                    settings[(DeviceSpecificScopeType.Management, management.Id)] = level.Settings;
                }
                foreach (Device device in management.Devices.Where(device => deviceIds.Contains(device.Id)))
                {
                    DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> level =
                        await settingsManager.LoadLevelAsync<GatewayDeviceSpecificSettings>(DeviceSpecificScopePath.ForGateway(management, device));
                    settings[(DeviceSpecificScopeType.Gateway, device.Id)] = level.Settings;
                }
            }
            return settings;
        }
    }
}
