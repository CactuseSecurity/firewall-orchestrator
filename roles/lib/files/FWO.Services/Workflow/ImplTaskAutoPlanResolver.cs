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
        /// Returns the effective settings of the global level and of every given gateway and requested management that
        /// exists, by scope type and id (null for the global level). All levels are resolved with one settings query;
        /// unknown management ids are left out.
        /// </summary>
        /// <param name="gateways">Gateways to resolve, with their management and its device type, as loaded with the
        /// device details.</param>
        /// <param name="managementIds">Managements to resolve.</param>
        public async Task<Dictionary<(DeviceSpecificScopeType ScopeType, int? Id), GlobalDeviceSpecificSettings>> Resolve(
            IReadOnlyCollection<Device> gateways, IReadOnlyCollection<int> managementIds)
        {
            List<(DeviceSpecificScopeType ScopeType, int? Id)> levels = [(DeviceSpecificScopeType.Global, null)];
            List<DeviceSpecificScopePath> paths = [DeviceSpecificScopePath.Global()];
            foreach (Management management in await LoadManagements(managementIds))
            {
                levels.Add((DeviceSpecificScopeType.Management, management.Id));
                paths.Add(DeviceSpecificScopePath.ForManagement(management));
            }
            // a gateway without known management cannot be placed in the hierarchy and inherits the global settings
            foreach (Device gateway in gateways.Where(gateway => gateway.Management.Id > 0).DistinctBy(gateway => gateway.Id))
            {
                levels.Add((DeviceSpecificScopeType.Gateway, gateway.Id));
                paths.Add(DeviceSpecificScopePath.ForGateway(gateway.Management, gateway));
            }

            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);
            Dictionary<(DeviceSpecificScopeType ScopeType, int? Id), GlobalDeviceSpecificSettings> settingsByLevel = [];
            for (int index = 0; index < levels.Count; index++)
            {
                settingsByLevel[levels[index]] = settings[index];
            }
            return settingsByLevel;
        }

        /// <summary>
        /// Loads the requested managements with their device type; no query without requested managements.
        /// </summary>
        private async Task<List<Management>> LoadManagements(IReadOnlyCollection<int> managementIds)
        {
            if (managementIds.Count == 0)
            {
                return [];
            }
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementHierarchy) ?? [];
            return [.. managements.Where(management => managementIds.Contains(management.Id))];
        }
    }
}
