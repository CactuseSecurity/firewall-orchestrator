using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Leaves out the steps of the modelling object types the device specific setting <c>provisioningExcludedObjectTypes</c>
    /// excludes for the target: hosts and address ranges are app servers, networks are networks, services are services,
    /// and groups and labels have the type their name tells (label key, else prefix of the naming convention). A group
    /// whose type cannot be told from its name is never left out.
    /// </summary>
    public static class FirewallWriteExclusions
    {
        private static readonly HashSet<string> GroupTaskTypes =
            [nameof(WfTaskType.group_create), nameof(WfTaskType.group_modify), nameof(WfTaskType.group_delete)];

        private static readonly HashSet<FirewallWriteOperation> AppServerOperations =
        [
            FirewallWriteOperation.HostCreate, FirewallWriteOperation.HostModify, FirewallWriteOperation.HostDelete,
            FirewallWriteOperation.AddressRangeCreate, FirewallWriteOperation.AddressRangeModify, FirewallWriteOperation.AddressRangeDelete,
            FirewallWriteOperation.Ipv6HostCreate, FirewallWriteOperation.Ipv6HostModify, FirewallWriteOperation.Ipv6HostDelete,
            FirewallWriteOperation.Ipv6AddressRangeCreate, FirewallWriteOperation.Ipv6AddressRangeModify, FirewallWriteOperation.Ipv6AddressRangeDelete
        ];

        private static readonly HashSet<FirewallWriteOperation> NetworkOperations =
        [
            FirewallWriteOperation.NetworkCreate, FirewallWriteOperation.NetworkModify, FirewallWriteOperation.NetworkDelete,
            FirewallWriteOperation.Ipv6NetworkCreate, FirewallWriteOperation.Ipv6NetworkModify, FirewallWriteOperation.Ipv6NetworkDelete
        ];

        private static readonly HashSet<FirewallWriteOperation> ServiceOperations =
        [
            FirewallWriteOperation.ServiceCreate, FirewallWriteOperation.ServiceModify, FirewallWriteOperation.ServiceDelete
        ];

        private static readonly HashSet<FirewallWriteOperation> GroupOperations =
        [
            FirewallWriteOperation.GroupCreate, FirewallWriteOperation.GroupAddMembers, FirewallWriteOperation.GroupRemoveMembers,
            FirewallWriteOperation.GroupDelete, FirewallWriteOperation.LabelWrite, FirewallWriteOperation.LabelDelete
        ];

        /// <summary>Returns the planned steps the settings of the target exclude from provisioning.</summary>
        public static List<FirewallWriteStep> ExcludedSteps(List<FirewallWriteStep> steps, WfImplTask implTask, FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            if (context.Settings.ProvisioningExcludedObjectTypes.Count == 0)
            {
                return [];
            }
            ModellingTypes.ModObjectType? groupType = GroupTaskTypes.Contains(implTask.TaskType)
                ? DynamicIpLabel.GroupObjectType(ImplTaskStepPlanner.GroupName(implTask), context.NamingConvention)
                : null;
            return [.. steps.Where(step => ObjectType(step.Operation, groupType) is ModellingTypes.ModObjectType objectType
                && context.Settings.IsExcludedFromProvisioning(objectType))];
        }

        /// <summary>
        /// Returns the modelling object type a step writes: app server, network or service by its operation, the type of
        /// the task's group for group and label operations; null for rules and session steps.
        /// </summary>
        public static ModellingTypes.ModObjectType? ObjectType(FirewallWriteOperation operation, ModellingTypes.ModObjectType? groupType)
        {
            if (AppServerOperations.Contains(operation))
            {
                return ModellingTypes.ModObjectType.AppServer;
            }
            if (NetworkOperations.Contains(operation))
            {
                return ModellingTypes.ModObjectType.Network;
            }
            if (ServiceOperations.Contains(operation))
            {
                return ModellingTypes.ModObjectType.Service;
            }
            return GroupOperations.Contains(operation) ? groupType : null;
        }
    }
}
