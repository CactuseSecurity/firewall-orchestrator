using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Kinds of findings of the object creation consistency check.
    /// </summary>
    public enum ObjectCreationFindingType
    {
        /// <summary>
        /// The creation setting of the super-management differs from what its sub-managements do: it has no effect.
        /// </summary>
        SuperSettingWithoutEffect,

        /// <summary>
        /// Objects are created on the super-management and on sub-managements of the same hierarchy.
        /// </summary>
        MixedCreation
    }

    /// <summary>
    /// One finding of the object creation consistency check for one hierarchy, covering every kind of objects with the
    /// same finding and the same targets.
    /// </summary>
    /// <param name="Type">Kind of the finding.</param>
    /// <param name="Kinds">Kinds of objects the finding refers to.</param>
    /// <param name="SuperManagement">Super-management of the hierarchy.</param>
    /// <param name="Managements">The targets that get implementation tasks.</param>
    public sealed record ObjectCreationFinding(ObjectCreationFindingType Type, List<ObjectTaskKind> Kinds, Management SuperManagement,
        List<Management> Managements);

    /// <summary>
    /// Checks the object creation settings of every hierarchy of super- and sub-managements against the targets that
    /// actually get implementation tasks, i.e. the result of <see cref="ImplTaskManagementResolver.ResolveTargets"/>
    /// filtered to the implementation mode Automatic. So the hints of the settings page follow the same decision as
    /// the action creating the implementation tasks.
    /// </summary>
    public static class ObjectCreationConsistencyCheck
    {
        private static readonly List<ObjectTaskKind> kCheckedKinds = [ObjectTaskKind.Network, ObjectTaskKind.Service, ObjectTaskKind.Time];

        /// <summary>
        /// Returns the findings of all hierarchies with sub-managements, ordered by super-management name. Kinds of
        /// objects with the same finding and the same targets are combined into one finding.
        /// </summary>
        /// <param name="managements">All managements with their super-management.</param>
        /// <param name="settingsByManagement">Effective settings by management id; a missing management gets the
        /// compiled defaults.</param>
        public static List<ObjectCreationFinding> Check(IReadOnlyCollection<Management> managements,
            IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement)
        {
            List<ObjectCreationFinding> findings = [];
            foreach (Management superManagement in ImplTaskManagementResolver.FindHierarchyRoots(managements))
            {
                if (ImplTaskManagementResolver.SubManagements(superManagement, managements).Count == 0)
                {
                    continue;
                }
                foreach (ObjectTaskKind kind in kCheckedKinds)
                {
                    ObjectCreationFinding? finding = CheckHierarchy(superManagement, managements, settingsByManagement, kind);
                    if (finding != null)
                    {
                        AddOrMerge(findings, finding);
                    }
                }
            }
            return findings;
        }

        private static ObjectCreationFinding? CheckHierarchy(Management superManagement, IReadOnlyCollection<Management> managements,
            IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement, ObjectTaskKind kind)
        {
            List<Management> targets = ImplTaskManagementResolver.ResolveTargets(superManagement, managements, settingsByManagement, kind);
            List<Management> automaticTargets = [.. targets.Where(target => ImplTaskManagementResolver.IsImplementedAutomatically(target, settingsByManagement))];
            bool superIsTarget = automaticTargets.Any(target => target.Id == superManagement.Id);
            bool subIsTarget = automaticTargets.Any(target => target.Id != superManagement.Id);
            List<ObjectTaskKind> kinds = [kind];
            if (superIsTarget && subIsTarget)
            {
                return new(ObjectCreationFindingType.MixedCreation, kinds, superManagement, automaticTargets);
            }
            if (SuperSettingHasNoEffect(superManagement, settingsByManagement, kind, superIsTarget, subIsTarget))
            {
                return new(ObjectCreationFindingType.SuperSettingWithoutEffect, kinds, superManagement, automaticTargets);
            }
            return null;
        }

        /// <summary>
        /// Adds the kinds of a finding to an existing finding of the same hierarchy, type and targets, else adds it.
        /// </summary>
        private static void AddOrMerge(List<ObjectCreationFinding> findings, ObjectCreationFinding finding)
        {
            ObjectCreationFinding? existing = findings.FirstOrDefault(candidate => candidate.Type == finding.Type
                && candidate.SuperManagement.Id == finding.SuperManagement.Id
                && candidate.Managements.Select(management => management.Id).SequenceEqual(finding.Managements.Select(management => management.Id)));
            if (existing == null)
            {
                findings.Add(finding);
                return;
            }
            existing.Kinds.AddRange(finding.Kinds);
        }

        /// <summary>
        /// Returns whether the targets are all on one side, super-management or sub-managements, while the setting of
        /// the super-management names the other side.
        /// </summary>
        private static bool SuperSettingHasNoEffect(Management superManagement, IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement,
            ObjectTaskKind kind, bool superIsTarget, bool subIsTarget)
        {
            bool superSettingNamesSuper = CreatesOnSuperManagement(SettingsOf(superManagement, settingsByManagement), kind);
            return (subIsTarget && superSettingNamesSuper) || (superIsTarget && !superSettingNamesSuper);
        }

        /// <summary>
        /// Returns whether a creation setting names the super-management; an undefined setting does so like in
        /// <see cref="ImplTaskManagementResolver.ResolveTargets"/>.
        /// </summary>
        private static bool CreatesOnSuperManagement(GlobalDeviceSpecificSettings settings, ObjectTaskKind kind)
        {
            ProvisioningObjectCreationMode mode = ImplTaskManagementResolver.GetCreationMode(settings, kind);
            return mode != ProvisioningObjectCreationMode.Submanager && mode != ProvisioningObjectCreationMode.Ignore;
        }

        private static GlobalDeviceSpecificSettings SettingsOf(Management management, IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement)
        {
            return settingsByManagement.TryGetValue(management.Id, out GlobalDeviceSpecificSettings? settings) ? settings : new();
        }
    }
}
