using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using System.Globalization;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Derives the firewall write steps of one implementation task: object, service, group and rule operations, each with
    /// the placeholder values of the task, its elements and the target management and gateway. Session steps such as
    /// publish and install are added by <see cref="FirewallWriter"/>.
    /// </summary>
    public static class ImplTaskStepPlanner
    {
        private const int kActionAccept = 1;
        private const int kActionAccess = 4;
        private const int kActionPermit = 11;
        private const int kTrackNone = 2;

        private static readonly List<string> MemberAddActions =
            [nameof(RequestAction.create), nameof(RequestAction.addAfterCreation)];
        private static readonly List<string> ObjectActions =
            [nameof(RequestAction.create), nameof(RequestAction.modify), nameof(RequestAction.delete)];

        /// <summary>Plans the steps of a task in execution order; every step carries the context placeholders.</summary>
        /// <exception cref="ConfigException">Thrown for task types, actions or elements that cannot be written.</exception>
        public static List<FirewallWriteStep> Plan(WfImplTask implTask, FirewallWriteContext context, FirewallRulePlacement? placement)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            FirewallVendorProfile profile = FirewallVendorProfile.For(context.Vendor);
            List<FirewallWriteStep> steps = implTask.TaskType switch
            {
                nameof(WfTaskType.object_create) or nameof(WfTaskType.object_modify) => PlanObjectTask(implTask, profile),
                nameof(WfTaskType.group_create) => PlanGroupCreate(implTask, profile),
                nameof(WfTaskType.group_modify) => PlanGroupModify(implTask, profile),
                nameof(WfTaskType.group_delete) => [GroupStep(FirewallWriteOperation.GroupDelete, implTask, profile, new List<WfImplElement>())],
                nameof(WfTaskType.access) => PlanRuleCreate(implTask, profile),
                nameof(WfTaskType.rule_modify) => [RuleStep(FirewallWriteOperation.RuleModify, implTask, profile, RuleUid(implTask))],
                nameof(WfTaskType.rule_delete) => PlanRuleDelete(implTask, profile),
                _ => throw new ConfigException($"Task type '{implTask.TaskType}' cannot be written to a firewall.")
            };

            IReadOnlyDictionary<string, FirewallPlaceholderValue> contextValues = ContextValues(implTask, context, placement);
            steps.ForEach(step => step.AddMissing(contextValues));
            return steps;
        }

        /// <summary>Returns the placeholder values of the target and the task that every step and session step can use.</summary>
        public static IReadOnlyDictionary<string, FirewallPlaceholderValue> ContextValues(WfImplTask implTask, FirewallWriteContext context,
            FirewallRulePlacement? placement)
        {
            string gatewayName = context.Gateway?.Name ?? "";
            (string deviceName, string vdomName) = context.Vendor == FirewallVendor.FortiManager
                ? FirewallVendorProfile.SplitDeviceAndVdom(gatewayName)
                : (gatewayName, FirewallVendorProfile.SplitDeviceAndVdom("").Vdom);
            string domain = context.Management.ConfigPath ?? "";
            FirewallRulePlacement effectivePlacement = placement ?? FirewallRulePlacement.EndOfRulebase;

            FirewallWriteStep values = new(FirewallWriteOperation.Publish);
            values.SetText(FirewallWritePlaceholders.ManagementName, context.Management.Name)
                .SetText(FirewallWritePlaceholders.GatewayName, gatewayName)
                .SetText(FirewallWritePlaceholders.DeviceName, deviceName)
                .SetText(FirewallWritePlaceholders.VdomName, vdomName)
                .SetText(FirewallWritePlaceholders.PolicyName, context.Gateway?.Package ?? context.Gateway?.LocalRulebase ?? "")
                .SetText(FirewallWritePlaceholders.LayerName, effectivePlacement.LayerUid ?? context.Gateway?.LocalRulebase ?? "")
                .SetText(FirewallWritePlaceholders.DomainName, domain)
                .SetText(FirewallWritePlaceholders.AdomName, domain)
                .SetText(FirewallWritePlaceholders.ResourceGroup, domain)
                .SetText(FirewallWritePlaceholders.SubscriptionId, context.Management.CloudSubscriptionId ?? "")
                .SetText(FirewallWritePlaceholders.TicketId, implTask.TicketId.ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.TaskId, implTask.Id.ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.TaskNumber, implTask.TaskNumber.ToString(CultureInfo.InvariantCulture))
                .SetJson(FirewallWritePlaceholders.RulePosition, effectivePlacement.ToJson());
            return values.Values;
        }

        private static List<FirewallWriteStep> PlanObjectTask(WfImplTask implTask, FirewallVendorProfile profile)
        {
            string defaultAction = implTask.TaskType == nameof(WfTaskType.object_create) ? nameof(RequestAction.create) : nameof(RequestAction.modify);
            List<FirewallWriteStep> steps = [];
            foreach (WfImplElement element in implTask.ImplElements)
            {
                if (element.ImplAction == nameof(RequestAction.unchanged))
                {
                    continue;
                }
                string action = ObjectActions.Contains(element.ImplAction) ? element.ImplAction : defaultAction;
                if (FirewallElementValues.IsServiceElement(element))
                {
                    steps.Add(FirewallElementValues.ServiceStep(element, action, Comment(implTask)));
                }
                else if (FirewallElementValues.IsNetworkElement(element))
                {
                    steps.Add(FirewallElementValues.NetworkObjectStep(element, action, profile, Comment(implTask)));
                }
            }
            return steps;
        }

        private static List<FirewallWriteStep> PlanGroupCreate(WfImplTask implTask, FirewallVendorProfile profile)
        {
            List<WfImplElement> members = GroupMembers(implTask);
            List<WfImplElement> groupMembers = [.. members.Where(element => element.ImplAction != nameof(RequestAction.delete))];
            List<FirewallWriteStep> steps = MemberObjectSteps(implTask, members, profile);
            steps.Add(GroupStep(FirewallWriteOperation.GroupCreate, implTask, profile, groupMembers));
            return steps;
        }

        private static List<FirewallWriteStep> PlanGroupModify(WfImplTask implTask, FirewallVendorProfile profile)
        {
            List<WfImplElement> members = GroupMembers(implTask);
            List<WfImplElement> toAdd = [.. members.Where(element => MemberAddActions.Contains(element.ImplAction))];
            List<WfImplElement> toRemove = [.. members.Where(element => element.ImplAction == nameof(RequestAction.delete))];
            List<FirewallWriteStep> steps = MemberObjectSteps(implTask, members, profile);
            if (toAdd.Count > 0)
            {
                steps.Add(GroupStep(FirewallWriteOperation.GroupAddMembers, implTask, profile, toAdd));
            }
            if (toRemove.Count > 0)
            {
                steps.Add(GroupStep(FirewallWriteOperation.GroupRemoveMembers, implTask, profile, toRemove));
            }
            return steps;
        }

        /// <summary>
        /// Creates or modifies the member objects that the task brings along with addresses, before they are used in a
        /// group or rule. Members without addresses are expected to exist on the firewall.
        /// </summary>
        private static List<FirewallWriteStep> MemberObjectSteps(WfImplTask implTask, List<WfImplElement> members, FirewallVendorProfile profile)
        {
            if (profile.ReferencesObjectsByAddress)
            {
                return [];
            }
            return [.. members
                .Where(element => element.ImplAction is nameof(RequestAction.create) or nameof(RequestAction.modify))
                .Where(element => element.NetworkId == null && !string.IsNullOrWhiteSpace(element.IpString))
                .Select(element => FirewallElementValues.NetworkObjectStep(element, element.ImplAction, profile, Comment(implTask)))];
        }

        private static FirewallWriteStep GroupStep(FirewallWriteOperation operation, WfImplTask implTask, FirewallVendorProfile profile,
            List<WfImplElement> members)
        {
            string groupName = profile.NormalizeName(GroupName(implTask));
            List<string> memberNames;
            List<string> addresses = [];
            if (profile.ReferencesObjectsByAddress)
            {
                addresses = [.. members.Select(FirewallElementValues.AddressNotation).Distinct()];
                memberNames = [.. members.Where(element => !string.IsNullOrWhiteSpace(element.Name)).Select(element => element.Name!.Trim()).Distinct()];
            }
            else
            {
                memberNames = [.. members.Select(element => FirewallElementValues.RequireName(element, profile)).Distinct()];
            }
            List<string> renderedMembers = profile.ReferencesObjectsByAddress ? addresses : memberNames;

            FirewallWriteStep step = new(operation) { TargetName = groupName };
            if (operation == FirewallWriteOperation.GroupRemoveMembers)
            {
                step.MembersToRemove.AddRange(renderedMembers);
            }
            else
            {
                step.MembersToAdd.AddRange(renderedMembers);
            }
            return step.SetText(FirewallWritePlaceholders.GroupName, groupName)
                .SetText(FirewallWritePlaceholders.Comment, Comment(implTask))
                .SetStringList(FirewallWritePlaceholders.Members, memberNames)
                .SetStringList(FirewallWritePlaceholders.MembersToAdd, step.MembersToAdd)
                .SetStringList(FirewallWritePlaceholders.MembersToRemove, step.MembersToRemove)
                .SetNameObjectList(FirewallWritePlaceholders.MembersNameObjects, memberNames)
                .SetText(FirewallWritePlaceholders.MembersLines, string.Join('\n', memberNames))
                .SetStringList(FirewallWritePlaceholders.MemberAddresses, addresses);
        }

        private static List<FirewallWriteStep> PlanRuleCreate(WfImplTask implTask, FirewallVendorProfile profile)
        {
            List<WfImplElement> networkElements = [.. implTask.ImplElements.Where(FirewallElementValues.IsNetworkElement)];
            List<FirewallWriteStep> steps = MemberObjectSteps(implTask, networkElements, profile);
            steps.Add(RuleStep(FirewallWriteOperation.RuleCreate, implTask, profile, ""));
            return steps;
        }

        private static List<FirewallWriteStep> PlanRuleDelete(WfImplTask implTask, FirewallVendorProfile profile)
        {
            List<string> ruleUids = [.. implTask.GetRuleElements().Select(rule => rule.RuleUid).Where(uid => !string.IsNullOrWhiteSpace(uid)).Distinct()];
            if (ruleUids.Count == 0)
            {
                throw new ConfigException($"Implementation task {implTask.Id} has no rule to delete.");
            }
            return [.. ruleUids.Select(uid => RuleStep(FirewallWriteOperation.RuleDelete, implTask, profile, uid))];
        }

        private static FirewallWriteStep RuleStep(FirewallWriteOperation operation, WfImplTask implTask, FirewallVendorProfile profile, string ruleUid)
        {
            FirewallWriteStep step = new(operation) { TargetName = string.IsNullOrWhiteSpace(ruleUid) ? implTask.Title : ruleUid };
            step.SetText(FirewallWritePlaceholders.RuleName, implTask.Title)
                .SetText(FirewallWritePlaceholders.RuleUid, ruleUid)
                .SetText(FirewallWritePlaceholders.RuleComment, Comment(implTask))
                .SetText(FirewallWritePlaceholders.RuleAction, IsAccept(implTask.RuleAction) ? profile.AcceptAction : profile.DropAction)
                .SetText(FirewallWritePlaceholders.RuleTracking, implTask.Tracking == kTrackNone ? profile.LogOff : profile.LogOn);
            FirewallRuleValues.SetRuleElements(step, implTask, profile);
            return step;
        }

        private static bool IsAccept(int? ruleAction) => ruleAction is null or kActionAccept or kActionAccess or kActionPermit;

        private static string RuleUid(WfImplTask implTask)
        {
            string? ruleUid = implTask.GetRuleElements().Select(rule => rule.RuleUid).FirstOrDefault(uid => !string.IsNullOrWhiteSpace(uid));
            return ruleUid ?? throw new ConfigException($"Implementation task {implTask.Id} has no rule to modify.");
        }

        private static List<WfImplElement> GroupMembers(WfImplTask implTask)
        {
            if (implTask.ImplElements.Any(FirewallElementValues.IsServiceElement))
            {
                throw new ConfigException($"Implementation task {implTask.Id}: service groups are not supported by the firewall write library.");
            }
            return [.. implTask.ImplElements.Where(FirewallElementValues.IsNetworkElement)];
        }

        private static string GroupName(WfImplTask implTask)
        {
            string? groupName = implTask.ImplElements.Select(element => element.GroupName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            groupName ??= implTask.Title;
            return string.IsNullOrWhiteSpace(groupName)
                ? throw new ConfigException($"Implementation task {implTask.Id} has no group name.")
                : groupName.Trim();
        }

        private static string Comment(WfImplTask implTask) => implTask.FreeText ?? "";
    }
}
