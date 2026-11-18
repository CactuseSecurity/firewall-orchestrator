using FWO.Basics;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Plans the steps of vendors that write groups as labels with IP criteria (Guardicore). A group task writes the label
    /// its group stands for (see <see cref="DynamicIpLabel.FromGroupName"/>): a new group gets exactly its members as IP
    /// criteria, a changed group gets its members to remove taken out and its other members added, and a deleted group
    /// deletes the label value. Network objects exist only as criteria of labels, so object tasks write nothing.
    /// </summary>
    public static class LabelStepPlanner
    {
        /// <summary>Plans the steps of a task.</summary>
        /// <exception cref="FirewallWriteException">Thrown for rule tasks, groups that stand for no label and members without addresses.</exception>
        public static List<FirewallWriteStep> Plan(WfImplTask implTask, FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            return implTask.TaskType switch
            {
                nameof(WfTaskType.object_create) or nameof(WfTaskType.object_modify) => [],
                nameof(WfTaskType.group_create) => [LabelWriteStep(implTask, context, true)],
                nameof(WfTaskType.group_modify) => [LabelWriteStep(implTask, context, false)],
                nameof(WfTaskType.group_delete) => [LabelDeleteStep(implTask, context)],
                _ => throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                    $"Task type '{implTask.TaskType}' cannot be written to a firewall that segments by labels; only groups are written as labels so far.")
            };
        }

        /// <summary>Returns the label a group task writes.</summary>
        /// <exception cref="FirewallWriteException">Thrown if the group name is neither "key: value" nor an id string of the naming convention.</exception>
        public static DynamicIpLabel Label(WfImplTask implTask, FirewallWriteContext context)
        {
            string groupName = ImplTaskStepPlanner.GroupName(implTask);
            return DynamicIpLabel.FromGroupName(groupName, context.NamingConvention)
                ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                    $"Group '{groupName}' of implementation task {implTask.Id} stands for no label: it is neither named \"key{DynamicIpLabel.kKeyValueSeparator}value\" " +
                    "nor an app role, app zone or network area id string of the naming convention.");
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
