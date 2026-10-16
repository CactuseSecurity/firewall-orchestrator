using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Determines the managements an object task (group or single object) is implemented on.
    /// </summary>
    /// <remarks>
    /// The level objects are written to is taken from the device specific settings addressObjectCreation,
    /// serviceObjectCreation or timeObjectCreation, read for every sub-management of the super-management with its
    /// inheritance from device type and global level. A sub-management set to Submanager gets its own task, one set
    /// to Supermanager shares the single task on the super-management, one set to ignore gets none. If every
    /// sub-management is set to ignore, the request management is the target, so that the task is not left without
    /// implementation task. A management without sub-managements is its own target. Needs read access to all managements and the device specific settings, so it runs with the
    /// middleware-server role only; the ui resolves the targets through the middleware endpoint getImplTaskTargets.
    /// </remarks>
    public class ImplTaskManagementResolver(ApiConnection apiConnection)
    {
        /// <summary>
        /// Resolves the target managements of an object task.
        /// </summary>
        /// <param name="managementId">Management the request task belongs to.</param>
        /// <param name="kind">Kind of the created objects, which selects the device specific setting.</param>
        /// <returns>The target managements (id and name) without duplicates; the request management itself if it is unknown.</returns>
        public async Task<List<Management>> ResolveObjectTargets(int managementId, ObjectTaskKind kind)
        {
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementHierarchy);
            Management? requestManagement = managements.FirstOrDefault(management => management.Id == managementId);
            if (requestManagement == null)
            {
                return [new() { Id = managementId }];
            }

            Management superManagement = FindSuperManagement(requestManagement, managements);
            List<Management> subManagements = [.. managements
                .Where(management => management.SuperManagerId == superManagement.Id && !management.HideInUi)
                .OrderBy(management => management.Name, StringComparer.OrdinalIgnoreCase)];
            if (subManagements.Count == 0)
            {
                return [ToTarget(superManagement)];
            }

            DeviceSpecificSettingsManager settingsManager = new(apiConnection);
            DeviceSpecificSettingKey<ProvisioningObjectCreationMode> key = GetCreationSettingKey(kind);
            List<Management> targets = [];
            foreach (Management subManagement in subManagements)
            {
                ResolvedDeviceSpecificValue<ProvisioningObjectCreationMode> mode =
                    await settingsManager.LoadEffectiveValueAsync(DeviceSpecificScopePath.ForManagement(subManagement), key);
                if (mode.Value == ProvisioningObjectCreationMode.Ignore)
                {
                    continue;
                }
                Management target = mode.Value == ProvisioningObjectCreationMode.Submanager ? subManagement : superManagement;
                if (targets.All(existing => existing.Id != target.Id))
                {
                    targets.Add(ToTarget(target));
                }
            }
            return targets.Count > 0 ? targets : [ToTarget(requestManagement)];
        }

        /// <summary>
        /// Returns the device specific setting that holds the creation level of the given kind of objects.
        /// </summary>
        public static DeviceSpecificSettingKey<ProvisioningObjectCreationMode> GetCreationSettingKey(ObjectTaskKind kind)
        {
            return kind switch
            {
                ObjectTaskKind.Service => DeviceSpecificSettingKeys.ServiceObjectCreation,
                ObjectTaskKind.Time => DeviceSpecificSettingKeys.TimeObjectCreation,
                _ => DeviceSpecificSettingKeys.AddressObjectCreation
            };
        }

        private static Management FindSuperManagement(Management management, List<Management> managements)
        {
            if (management.SuperManagerId == null)
            {
                return management;
            }
            return managements.FirstOrDefault(candidate => candidate.Id == management.SuperManagerId) ?? management;
        }

        private static Management ToTarget(Management management)
        {
            return new() { Id = management.Id, Name = management.Name };
        }
    }
}
