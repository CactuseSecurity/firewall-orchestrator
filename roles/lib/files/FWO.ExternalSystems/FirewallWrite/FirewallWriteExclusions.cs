using System.Collections.Frozen;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Leaves out the group tasks of the modelling object types the device specific setting
    /// <c>provisioningExcludedObjectTypes</c> excludes for the target, with all their steps, including the member objects
    /// created for the group. A group has the type its name tells (label key of a name "key: value"), else the type its request
    /// task names (<see cref="FirewallWriteContext.GroupObjectType"/>); a group of unknown type is never left out. Only group types can be excluded, so tasks of
    /// objects, services and rules are always written.
    /// </summary>
    public static class FirewallWriteExclusions
    {
        private static readonly FrozenSet<string> GroupTaskTypes =
            [nameof(WfTaskType.group_create), nameof(WfTaskType.group_modify), nameof(WfTaskType.group_delete)];

        /// <summary>Returns the planned steps the settings of the target exclude from provisioning.</summary>
        public static List<FirewallWriteStep> ExcludedSteps(List<FirewallWriteStep> steps, WfImplTask implTask, FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            ModellingTypes.ModObjectType? groupType = GroupType(implTask, context);
            return groupType != null && context.Settings.IsExcludedFromProvisioning(groupType.Value) ? [.. steps] : [];
        }

        /// <summary>The modelling object type of the group of a group task; null for other tasks and unknown groups.</summary>
        public static ModellingTypes.ModObjectType? GroupType(WfImplTask implTask, FirewallWriteContext context) =>
            GroupTaskTypes.Contains(implTask.TaskType)
                ? DynamicIpLabel.GroupObjectType(ImplTaskStepPlanner.GroupName(implTask), context.GroupObjectType)
                : null;
    }
}
