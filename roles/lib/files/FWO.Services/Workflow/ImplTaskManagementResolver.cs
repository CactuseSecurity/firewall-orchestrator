using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Provisioning;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Determines the managements an object task (group or single object) is implemented on.
    /// </summary>
    /// <remarks>
    /// The level objects are written to is taken from the provisioning settings addressObjectCreation and
    /// serviceObjectCreation, read for every sub-management of the super-management with its inheritance from
    /// device type and global level. A sub-management set to Submanager gets its own task, one set to
    /// Supermanager shares the single task on the super-management. A management without sub-managements
    /// is its own target. Needs read access to all managements and the provisioning settings, so it runs with the
    /// middleware-server role only; the ui resolves the targets through the middleware endpoint getImplTaskTargets.
    /// </remarks>
    public class ImplTaskManagementResolver(ApiConnection apiConnection)
    {
        /// <summary>
        /// Resolves the target managements of an object task.
        /// </summary>
        /// <param name="managementId">Management the request task belongs to.</param>
        /// <param name="isServiceObject">True for services, false for network objects.</param>
        /// <returns>The target managements (id and name) without duplicates; the request management itself if it is unknown.</returns>
        public async Task<List<Management>> ResolveObjectTargets(int managementId, bool isServiceObject)
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

            ProvisioningSettingsManager settingsManager = new(apiConnection);
            ProvisioningSettingKey<ProvisioningObjectCreationMode> key = isServiceObject
                ? ProvisioningSettingKeys.ServiceObjectCreation
                : ProvisioningSettingKeys.AddressObjectCreation;
            List<Management> targets = [];
            foreach (Management subManagement in subManagements)
            {
                ResolvedProvisioningValue<ProvisioningObjectCreationMode> mode =
                    await settingsManager.LoadEffectiveValueAsync(ProvisioningScopePath.ForManagement(subManagement), key);
                Management target = mode.Value == ProvisioningObjectCreationMode.Submanager ? subManagement : superManagement;
                if (targets.All(existing => existing.Id != target.Id))
                {
                    targets.Add(ToTarget(target));
                }
            }
            return targets;
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
