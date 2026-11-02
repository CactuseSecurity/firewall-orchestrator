using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// One finding of the object creation consistency check for one hierarchy: sub-managements whose object creation
    /// setting differs from the one of their super-management, covering every kind of objects with the same
    /// sub-managements.
    /// </summary>
    /// <param name="Kinds">Kinds of objects the finding refers to.</param>
    /// <param name="SuperManagement">Super-management of the hierarchy.</param>
    /// <param name="Managements">The sub-managements whose setting differs.</param>
    public sealed record ObjectCreationFinding(List<ObjectTaskKind> Kinds, Management SuperManagement, List<Management> Managements);

    /// <summary>
    /// Checks the object creation settings of every hierarchy of super- and sub-managements. A task on the
    /// super-management follows its setting, a task on a sub-management the setting of that sub-management (see
    /// <see cref="ImplTaskManagementResolver"/>). A sub-management whose setting differs from the one of its
    /// super-management therefore gets other targets depending on where a task is ordered, which is reported. A
    /// sub-management set to ignore is not reported: it wants no objects at all.
    /// </summary>
    public static class ObjectCreationConsistencyCheck
    {
        private static readonly List<ObjectTaskKind> kCheckedKinds = [ObjectTaskKind.Network, ObjectTaskKind.Service, ObjectTaskKind.Time];

        /// <summary>
        /// Returns the findings of all hierarchies with sub-managements, ordered by super-management name. Kinds of
        /// objects with the same differing sub-managements are combined into one finding.
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
                List<Management> subManagements = ImplTaskManagementResolver.SubManagements(superManagement, managements);
                foreach (ObjectTaskKind kind in kCheckedKinds)
                {
                    List<Management> differingSubManagements = DifferingSubManagements(superManagement, subManagements, settingsByManagement, kind);
                    if (differingSubManagements.Count > 0)
                    {
                        List<ObjectTaskKind> kinds = [kind];
                        AddOrMerge(findings, new(kinds, superManagement, differingSubManagements));
                    }
                }
            }
            return findings;
        }

        private static List<Management> DifferingSubManagements(Management superManagement, List<Management> subManagements,
            IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement, ObjectTaskKind kind)
        {
            ProvisioningObjectCreationMode superMode = Normalize(GetMode(superManagement, settingsByManagement, kind));
            return [.. subManagements.Where(subManagement =>
            {
                ProvisioningObjectCreationMode subMode = Normalize(GetMode(subManagement, settingsByManagement, kind));
                return subMode != ProvisioningObjectCreationMode.Ignore && subMode != superMode;
            })];
        }

        /// <summary>
        /// Adds the kinds of a finding to an existing finding of the same hierarchy and sub-managements, else adds it.
        /// </summary>
        private static void AddOrMerge(List<ObjectCreationFinding> findings, ObjectCreationFinding finding)
        {
            ObjectCreationFinding? existing = findings.FirstOrDefault(candidate => candidate.SuperManagement.Id == finding.SuperManagement.Id
                && candidate.Managements.Select(management => management.Id).SequenceEqual(finding.Managements.Select(management => management.Id)));
            if (existing == null)
            {
                findings.Add(finding);
                return;
            }
            existing.Kinds.AddRange(finding.Kinds);
        }

        /// <summary>
        /// An undefined setting creates on the super-management like in <see cref="ImplTaskManagementResolver"/>.
        /// </summary>
        private static ProvisioningObjectCreationMode Normalize(ProvisioningObjectCreationMode mode)
        {
            return mode == ProvisioningObjectCreationMode.Undefined ? ProvisioningObjectCreationMode.Supermanager : mode;
        }

        private static ProvisioningObjectCreationMode GetMode(Management management, IReadOnlyDictionary<int, GlobalDeviceSpecificSettings> settingsByManagement,
            ObjectTaskKind kind)
        {
            GlobalDeviceSpecificSettings settings = settingsByManagement.TryGetValue(management.Id, out GlobalDeviceSpecificSettings? found) ? found : new();
            return ImplTaskManagementResolver.GetCreationMode(settings, kind);
        }
    }
}
