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
    /// to Supermanager shares the single task on the super-management, one set to ignore gets none. A task ordered on
    /// a sub-management only regards that sub-management, a task ordered on the super-management all of them. The setting of a
    /// super-management with sub-managements is not read: the super-management is only a target, never a source. A
    /// management without sub-managements is its own target unless it is set to ignore itself. So the result is empty
    /// when every candidate is set to ignore, which the device specific settings allow for time objects only: no
    /// implementation task is to be created, and the request dialog rejects such a task beforehand. The implementation
    /// mode of the targets is not regarded here; the creation of the implementation tasks and the consistency check of
    /// the settings page filter it with <see cref="IsImplementedAutomatically"/>. Needs read access to all managements
    /// and the device specific settings, so it runs with the middleware-server role only; the ui resolves the targets
    /// through the middleware endpoint getImplTaskTargets.
    /// </remarks>
    public class ImplTaskManagementResolver(ApiConnection apiConnection)
    {
        /// <summary>
        /// Resolves the target managements of an object task.
        /// </summary>
        /// <param name="managementId">Management the request task belongs to.</param>
        /// <param name="kind">Kind of the created objects, which selects the device specific setting.</param>
        /// <returns>The target managements (id and name) without duplicates; empty when every candidate is set to
        /// ignore; the request management itself if it is unknown.</returns>
        public async Task<List<Management>> ResolveObjectTargets(int managementId, ObjectTaskKind kind)
        {
            List<Management> managements = await LoadManagements();
            Management? requestManagement = managements.FirstOrDefault(management => management.Id == managementId);
            if (requestManagement == null)
            {
                return [new() { Id = managementId }];
            }

            Management superManagement = FindSuperManagement(requestManagement, managements);
            if (superManagement.Id != requestManagement.Id)
            {
                List<Management> requestManagementOnly = [requestManagement];
                Dictionary<int, GlobalDeviceSpecificSettings> subSettings = await LoadSettings(requestManagementOnly);
                return ResolveSubManagementTarget(requestManagement, superManagement, subSettings, kind);
            }
            Dictionary<int, GlobalDeviceSpecificSettings> settings = await LoadSettings(HierarchyMembers(superManagement, managements));
            return ResolveTargets(superManagement, managements, settings, kind);
        }

        /// <summary>
        /// Resolves the target of an object task ordered on a sub-management: only that sub-management is regarded,
        /// not its sibling sub-managements.
        /// </summary>
        /// <param name="subManagement">Sub-management the request task belongs to.</param>
        /// <param name="superManagement">Super-management of the sub-management.</param>
        /// <param name="settingsByManagement">Effective settings by management id; a missing management gets the
        /// compiled defaults.</param>
        /// <param name="kind">Kind of the created objects, which selects the device specific setting.</param>
        /// <returns>The sub-management when set to Submanager, the super-management when set to Supermanager, nothing
        /// when set to ignore.</returns>
        public static List<Management> ResolveSubManagementTarget(Management subManagement, Management superManagement,
            IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement, ObjectTaskKind kind)
        {
            ProvisioningObjectCreationMode mode = GetCreationMode(SettingsOf(subManagement, settingsByManagement), kind);
            return mode switch
            {
                ProvisioningObjectCreationMode.Ignore => [],
                ProvisioningObjectCreationMode.Submanager => [ToTarget(subManagement)],
                _ => [ToTarget(superManagement)]
            };
        }

        /// <summary>
        /// Resolves the target managements of an object task without management, e.g. ordered through the REST
        /// interface: the targets of every hierarchy of managements shown in the ui.
        /// </summary>
        /// <param name="kind">Kind of the created objects, which selects the device specific setting.</param>
        /// <returns>The target managements (id and name) without duplicates, ordered by name.</returns>
        public async Task<List<Management>> ResolveObjectTargetsOfAllManagements(ObjectTaskKind kind)
        {
            List<Management> managements = await LoadManagements();
            List<Management> superManagements = FindHierarchyRoots(managements);
            List<Management> hierarchyMembers = [.. superManagements.SelectMany(superManagement => HierarchyMembers(superManagement, managements))];
            Dictionary<int, GlobalDeviceSpecificSettings> settings = await LoadSettings(hierarchyMembers);
            List<Management> targets = [];
            foreach (Management superManagement in superManagements)
            {
                foreach (Management target in ResolveTargets(superManagement, managements, settings, kind))
                {
                    AddMissingTarget(targets, target);
                }
            }
            return [.. targets.OrderBy(target => target.Name, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Resolves the target managements of one hierarchy from settings already loaded, without api access, so the
        /// creation of implementation tasks and the consistency check of the settings page decide alike.
        /// </summary>
        /// <param name="superManagement">Super-management of the hierarchy, or a management without sub-managements.</param>
        /// <param name="managements">All managements with their super-management.</param>
        /// <param name="settingsByManagement">Effective settings by management id; a missing management gets the
        /// compiled defaults.</param>
        /// <param name="kind">Kind of the created objects, which selects the device specific setting.</param>
        /// <returns>The target managements (id and name) without duplicates.</returns>
        public static List<Management> ResolveTargets(Management superManagement, IReadOnlyCollection<Management> managements,
            IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement, ObjectTaskKind kind)
        {
            List<Management> subManagements = SubManagements(superManagement, managements);
            if (subManagements.Count == 0)
            {
                ProvisioningObjectCreationMode ownMode = GetCreationMode(SettingsOf(superManagement, settingsByManagement), kind);
                return ownMode == ProvisioningObjectCreationMode.Ignore ? [] : [ToTarget(superManagement)];
            }
            List<Management> targets = [];
            foreach (Management subManagement in subManagements)
            {
                ProvisioningObjectCreationMode mode = GetCreationMode(SettingsOf(subManagement, settingsByManagement), kind);
                if (mode == ProvisioningObjectCreationMode.Ignore)
                {
                    continue;
                }
                Management target = mode == ProvisioningObjectCreationMode.Submanager ? subManagement : superManagement;
                AddMissingTarget(targets, ToTarget(target));
            }
            return targets;
        }

        /// <summary>
        /// Returns whether objects are written automatically on a target management: its implementation mode is
        /// Automatic. Manual and external ticket system get no implementation task.
        /// </summary>
        /// <param name="target">Target management.</param>
        /// <param name="settingsByManagement">Effective settings by management id; a missing management gets the
        /// compiled defaults.</param>
        public static bool IsImplementedAutomatically(Management target, IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement)
        {
            return SettingsOf(target, settingsByManagement).GetImplementationMode(false) == ProvisioningImplementationMode.Automatic;
        }

        /// <summary>
        /// Returns the super-managements of all managements shown in the ui, and the managements without
        /// super-management, each once and ordered by name.
        /// </summary>
        /// <param name="managements">All managements with their super-management.</param>
        public static List<Management> FindHierarchyRoots(IReadOnlyCollection<Management> managements)
        {
            return [.. managements
                .Where(management => !management.HideInUi)
                .Select(management => FindSuperManagement(management, managements))
                .DistinctBy(management => management.Id)
                .OrderBy(management => management.Name, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Returns the sub-managements of a super-management shown in the ui, ordered by name.
        /// </summary>
        /// <param name="superManagement">The super-management.</param>
        /// <param name="managements">All managements with their super-management.</param>
        public static List<Management> SubManagements(Management superManagement, IReadOnlyCollection<Management> managements)
        {
            return [.. managements
                .Where(management => management.SuperManagerId == superManagement.Id && !management.HideInUi)
                .OrderBy(management => management.Name, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Returns the setting of the creation level of a kind of objects.
        /// </summary>
        public static ProvisioningObjectCreationMode GetCreationMode(GlobalDeviceSpecificSettings settings, ObjectTaskKind kind)
        {
            return kind switch
            {
                ObjectTaskKind.Service => settings.ServiceObjectCreation,
                ObjectTaskKind.Time => settings.TimeObjectCreation,
                _ => settings.AddressObjectCreation
            };
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

        private async Task<List<Management>> LoadManagements()
        {
            return await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementHierarchy) ?? [];
        }

        /// <summary>
        /// Loads the effective settings of the given managements with one settings query.
        /// </summary>
        private async Task<Dictionary<int, GlobalDeviceSpecificSettings>> LoadSettings(List<Management> managements)
        {
            List<Management> distinctManagements = [.. managements.DistinctBy(management => management.Id)];
            List<DeviceSpecificScopePath> paths = [.. distinctManagements.Select(DeviceSpecificScopePath.ForManagement)];
            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);
            Dictionary<int, GlobalDeviceSpecificSettings> settingsByManagement = [];
            for (int index = 0; index < distinctManagements.Count; index++)
            {
                settingsByManagement[distinctManagements[index].Id] = settings[index];
            }
            return settingsByManagement;
        }

        private static List<Management> HierarchyMembers(Management superManagement, IReadOnlyCollection<Management> managements)
        {
            return [superManagement, .. SubManagements(superManagement, managements)];
        }

        private static GlobalDeviceSpecificSettings SettingsOf(Management management, IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement)
        {
            return settingsByManagement.TryGetValue(management.Id, out GlobalDeviceSpecificSettings? settings) ? settings : new();
        }

        private static void AddMissingTarget(List<Management> targets, Management candidate)
        {
            if (targets.All(existing => existing.Id != candidate.Id))
            {
                targets.Add(candidate);
            }
        }

        private static Management FindSuperManagement(Management management, IReadOnlyCollection<Management> managements)
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
