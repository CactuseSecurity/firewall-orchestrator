using System.Collections.Frozen;
using FWO.Basics;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Plans the steps of vendors that write groups as labels with IP criteria (Guardicore). A group task writes the label
    /// its group stands for (see <see cref="DynamicIpLabel.FromGroupName"/>): a new group gets exactly its members as IP
    /// criteria, a changed group gets its members to remove taken out and its other members added, and a deleted group
    /// deletes the label value. Network objects exist only as criteria of labels, so object tasks write nothing. These vendors
    /// know no service groups either: the services are part of the rules, so service group tasks write nothing.
    /// </summary>
    public static class LabelStepPlanner
    {
        private static readonly FrozenSet<string> GroupTaskTypes =
            [nameof(WfTaskType.group_create), nameof(WfTaskType.group_modify), nameof(WfTaskType.group_delete)];

        /// <summary>
        /// Whether a task changes a service group: a group task whose request names the type service group, or whose
        /// elements are services. Such tasks are left out of provisioning with a note.
        /// </summary>
        public static bool IsServiceGroupTask(WfImplTask implTask, FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            return GroupTaskTypes.Contains(implTask.TaskType)
                && (context.GroupObjectType == ModellingTypes.ModObjectType.ServiceGroup
                    || implTask.ImplElements.Any(FirewallElementValues.IsServiceElement));
        }

        /// <summary>Plans the steps of a task.</summary>
        /// <exception cref="FirewallWriteException">Thrown for unknown task types, groups that stand for no label, members without addresses
        /// and rules that cannot be written, see <see cref="LabelRuleStepPlanner"/>.</exception>
        public static List<FirewallWriteStep> Plan(WfImplTask implTask, FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            if (IsServiceGroupTask(implTask, context))
            {
                return [];
            }
            return implTask.TaskType switch
            {
                nameof(WfTaskType.object_create) or nameof(WfTaskType.object_modify) => [],
                nameof(WfTaskType.group_create) => [LabelWriteStep(implTask, context, true)],
                nameof(WfTaskType.group_modify) => [LabelWriteStep(implTask, context, false)],
                nameof(WfTaskType.group_delete) => [LabelDeleteStep(implTask, context)],
                nameof(WfTaskType.access) or nameof(WfTaskType.rule_modify) or nameof(WfTaskType.rule_delete) =>
                    LabelRuleStepPlanner.Plan(implTask, context, FirewallVendorProfile.For(context.Vendor)),
                _ => throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                    $"Task type '{implTask.TaskType}' cannot be written to a firewall that segments by labels.")
            };
        }

        /// <summary>Returns the label a group task writes.</summary>
        /// <exception cref="FirewallWriteException">Thrown if the group is neither named "key: value" nor requested as app role, app zone or network area.</exception>
        public static DynamicIpLabel Label(WfImplTask implTask, FirewallWriteContext context)
        {
            string groupName = ImplTaskStepPlanner.GroupName(implTask);
            return DynamicIpLabel.FromGroupName(groupName, context.GroupObjectType)
                ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                    $"Group '{groupName}' of implementation task {implTask.Id} stands for no label: it is neither named \"key{DynamicIpLabel.kKeyValueSeparator}value\" " +
                    "nor requested as app role, app zone or network area.");
        }

        private static FirewallWriteStep LabelWriteStep(WfImplTask implTask, FirewallWriteContext context, bool replace)
        {
            DynamicIpLabel label = Label(implTask, context);
            List<WfImplElement> members = ImplTaskStepPlanner.GroupMembers(implTask);
            List<IpInterval> keep = MemberIntervals(members.Where(member => member.ImplAction != nameof(RequestAction.delete)));
            List<IpInterval> remove = replace ? [] : MemberIntervals(members.Where(member => member.ImplAction == nameof(RequestAction.delete)));

            FirewallWriteStep step = new(FirewallWriteOperation.LabelWrite) { TargetName = label.Name, ReplacesMembers = replace };
            step.MembersToAdd.AddRange(keep.Select(IpAddressSet.Format));
            step.MembersToRemove.AddRange(remove.Select(IpAddressSet.Format));
            return SetLabel(step, label, implTask)
                .SetJson(FirewallWritePlaceholders.LabelCriteria, GuardicoreLabelCriteria.CreationJson(keep));
        }

        private static FirewallWriteStep LabelDeleteStep(WfImplTask implTask, FirewallWriteContext context)
        {
            DynamicIpLabel label = Label(implTask, context);
            return SetLabel(new FirewallWriteStep(FirewallWriteOperation.LabelDelete) { TargetName = label.Name }, label, implTask);
        }

        private static FirewallWriteStep SetLabel(FirewallWriteStep step, DynamicIpLabel label, WfImplTask implTask)
        {
            return step.SetText(FirewallWritePlaceholders.LabelKey, label.Key)
                .SetText(FirewallWritePlaceholders.LabelValue, label.Value)
                .SetText(FirewallWritePlaceholders.GroupName, label.Name)
                .SetText(FirewallWritePlaceholders.Comment, ImplTaskStepPlanner.Comment(implTask));
        }

        /// <summary>The addresses of group members; a label holds addresses only, so every member needs one.</summary>
        private static List<IpInterval> MemberIntervals(IEnumerable<WfImplElement> members)
        {
            List<IpInterval> intervals = [];
            foreach (WfImplElement member in members)
            {
                if (!IpAddressSet.TryParseInterval(member.IpString, member.IpEnd, out IpInterval interval))
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                        $"Group member '{member.Name}' has no valid IP address; labels hold the addresses of their members only.");
                }
                intervals.Add(interval);
            }
            return intervals;
        }
    }
}
