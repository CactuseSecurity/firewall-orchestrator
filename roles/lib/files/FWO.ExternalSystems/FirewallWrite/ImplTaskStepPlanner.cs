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
        private const string kNoGateway = "the task names no gateway";
        private const string kNoConfigPath = "the management has no config path (domain, ADOM or resource group)";
        private const string kNoPolicy = "neither the gateway's package nor its imported rulebase links name a policy package";
        private const string kNoLayer = "neither the rule position nor the gateway's imported rulebase links name a layer";
        private const string kNoRuleUid = "a rule to be created has no uid yet";
        private const string kNoRuleAction = "the task has no rule action";
        private const string kNotOneMember = "the call does not write exactly one member; use @@MEMBERS_TO_ADD@@ or @@MEMBERS_TO_REMOVE@@";

        private static readonly List<string> MemberAddActions =
            [nameof(RequestAction.create), nameof(RequestAction.addAfterCreation)];
        private static readonly List<string> ObjectActions =
            [nameof(RequestAction.create), nameof(RequestAction.modify), nameof(RequestAction.delete)];

        /// <summary>Plans the steps of a task in execution order; every step carries the context placeholders.</summary>
        /// <exception cref="FirewallWriteException">Thrown for task types, actions or elements that cannot be written.</exception>
        public static List<FirewallWriteStep> Plan(WfImplTask implTask, FirewallWriteContext context, FirewallRulePlacement? placement)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            FirewallVendorProfile profile = FirewallVendorProfile.For(context.Vendor);
            List<FirewallWriteStep> steps = profile.WritesLabels ? LabelStepPlanner.Plan(implTask, context) : implTask.TaskType switch
            {
                nameof(WfTaskType.object_create) or nameof(WfTaskType.object_modify) => PlanObjectTask(implTask, profile),
                nameof(WfTaskType.group_create) => PlanGroupCreate(implTask, profile),
                nameof(WfTaskType.group_modify) => PlanGroupModify(implTask, profile),
                nameof(WfTaskType.group_delete) => [GroupStep(FirewallWriteOperation.GroupDelete, implTask, profile, [])],
                nameof(WfTaskType.access) => PlanRuleCreate(implTask, profile),
                nameof(WfTaskType.rule_modify) => PlanRuleModify(implTask, profile, context),
                nameof(WfTaskType.rule_delete) => PlanRuleDelete(implTask, profile, context),
                _ => throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported, $"Task type '{implTask.TaskType}' cannot be written to a firewall.")
            };

            IReadOnlyDictionary<string, FirewallPlaceholderValue> contextValues = ContextValues(implTask, context, placement);
            steps.ForEach(step => step.AddMissing(contextValues));
            return steps;
        }

        /// <summary>
        /// Returns the placeholder values of the target and the task that every step and session step can use. Target
        /// values that cannot be determined get no value, so a template that needs them fails before anything is sent.
        /// The policy package is the package of the gateway's current imported rulebase links, else the gateway's package
        /// (not maintained by current imports, so only a fallback), else its rulebase (FortiManager); the layer is the one of the rule position, else the first imported access layer, else the
        /// vendor's own default, else the gateway's rulebase.
        /// </summary>
        public static IReadOnlyDictionary<string, FirewallPlaceholderValue> ContextValues(WfImplTask implTask, FirewallWriteContext context,
            FirewallRulePlacement? placement)
        {
            string gatewayName = context.Gateway?.Name ?? "";
            (string deviceName, string vdomName) = context.Vendor == FirewallVendor.FortiManager
                ? FirewallVendorProfile.SplitDeviceAndVdom(gatewayName)
                : (gatewayName, FirewallVendorProfile.SplitDeviceAndVdom("").Vdom);
            string domain = context.Management.ConfigPath ?? "";
            FirewallRulePlacement effectivePlacement = placement ?? FirewallRulePlacement.EndOfRulebase;
            FirewallVendorProfile profile = FirewallVendorProfile.For(context.Vendor);
            string? policyName = FirstNonBlank(context.Rulebases.PolicyName, context.Gateway?.Package, context.Gateway?.LocalRulebase);
            string? layerName = FirstNonBlank(effectivePlacement.LayerUid, context.Rulebases.FirstLayerName, profile.DefaultLayerName,
                context.Gateway?.LocalRulebase);

            FirewallWriteStep values = new(FirewallWriteOperation.Publish);
            values.SetText(FirewallWritePlaceholders.ManagementName, context.Management.Name)
                .SetRequiredText(FirewallWritePlaceholders.GatewayName, gatewayName, kNoGateway)
                .SetRequiredText(FirewallWritePlaceholders.DeviceName, deviceName, kNoGateway)
                .SetText(FirewallWritePlaceholders.VdomName, vdomName)
                .SetRequiredText(FirewallWritePlaceholders.PolicyName, policyName, kNoPolicy, FirewallWriteErrorKind.Configuration)
                .SetRequiredText(FirewallWritePlaceholders.LayerName, layerName, kNoLayer, FirewallWriteErrorKind.Configuration)
                .SetRequiredText(FirewallWritePlaceholders.DomainName, domain, kNoConfigPath, FirewallWriteErrorKind.Configuration)
                .SetRequiredText(FirewallWritePlaceholders.AdomName, domain, kNoConfigPath, FirewallWriteErrorKind.Configuration)
                .SetRequiredText(FirewallWritePlaceholders.ResourceGroup, domain, kNoConfigPath, FirewallWriteErrorKind.Configuration)
                .SetRequiredText(FirewallWritePlaceholders.SubscriptionId, context.Management.CloudSubscriptionId,
                    "the management has no cloud subscription id", FirewallWriteErrorKind.Configuration)
                .SetText(FirewallWritePlaceholders.TicketId, implTask.TicketId.ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.TaskId, implTask.Id.ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.TaskNumber, implTask.TaskNumber.ToString(CultureInfo.InvariantCulture))
                .SetJson(FirewallWritePlaceholders.RulePosition, effectivePlacement.ToJson());
            SetInstallOn(values, context, profile);
            return values.Values;
        }

        /// <summary>
        /// Sets the install-on list of a new rule along the install-on setting: with gateways in path the uids (else names)
        /// of the gateways to install on, otherwise the vendor's value for all targets of the policy (Check Point: "Policy
        /// Targets"). A vendor without such a value gets the gateways as well.
        /// </summary>
        private static void SetInstallOn(FirewallWriteStep values, FirewallWriteContext context, FirewallVendorProfile profile)
        {
            if (context.Settings.InstallOn != ProvisioningInstallOn.GatewaysInPath && profile.AllTargetsInstallOn != null)
            {
                List<string> allTargets = [profile.AllTargetsInstallOn];
                values.SetStringList(FirewallWritePlaceholders.InstallOn, allTargets);
                return;
            }
            List<string> gateways = [.. context.EffectiveInstallOnGateways
                .Select(gateway => FirstNonBlank(gateway.Uid, gateway.Name))
                .OfType<string>()
                .Distinct()];
            if (gateways.Count == 0)
            {
                values.SetMissing(FirewallWritePlaceholders.InstallOn, kNoGateway);
                return;
            }
            values.SetStringList(FirewallWritePlaceholders.InstallOn, gateways);
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
                    steps.Add(FirewallElementValues.ServiceStep(element, action, profile, Comment(implTask)));
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
            List<FirewallWriteStep> steps = GroupMemberObjectSteps(implTask, members, profile);
            steps.Add(GroupStep(FirewallWriteOperation.GroupCreate, implTask, profile, groupMembers));
            return steps;
        }

        private static List<FirewallWriteStep> PlanGroupModify(WfImplTask implTask, FirewallVendorProfile profile)
        {
            List<WfImplElement> members = GroupMembers(implTask);
            List<WfImplElement> toAdd = [.. members.Where(element => MemberAddActions.Contains(element.ImplAction))];
            List<WfImplElement> toRemove = [.. members.Where(element => element.ImplAction == nameof(RequestAction.delete))];
            List<FirewallWriteStep> steps = GroupMemberObjectSteps(implTask, members, profile);
            steps.AddRange(MemberChangeSteps(FirewallWriteOperation.GroupAddMembers, implTask, profile, toAdd));
            steps.AddRange(MemberChangeSteps(FirewallWriteOperation.GroupRemoveMembers, implTask, profile, toRemove));
            return steps;
        }

        /// <summary>One step for all members, or one step per member for vendors that change the member table entry by entry.</summary>
        private static List<FirewallWriteStep> MemberChangeSteps(FirewallWriteOperation operation, WfImplTask implTask, FirewallVendorProfile profile,
            List<WfImplElement> members)
        {
            if (members.Count == 0)
            {
                return [];
            }
            if (!profile.WritesMembersIndividually)
            {
                return [GroupStep(operation, implTask, profile, members)];
            }
            if (profile.GroupsHoldAddresses)
            {
                List<string> entries = [.. MemberValues(members, profile).Names.Select(FirewallElementValues.AddressUtilityEntry)];
                return [.. entries.Select(entry => GroupStep(operation, implTask, profile, SingleEntry(entry), SingleEntry(entry)))];
            }
            return [.. members.Select(member => GroupStep(operation, implTask, profile, SingleMember(member)))];
        }

        private static List<WfImplElement> SingleMember(WfImplElement member) => [member];

        private static List<string> SingleEntry(string entry) => [entry];

        /// <summary>Member objects of a group task; none for vendors whose groups hold the members' addresses themselves.</summary>
        private static List<FirewallWriteStep> GroupMemberObjectSteps(WfImplTask implTask, List<WfImplElement> members, FirewallVendorProfile profile)
        {
            return profile.GroupsHoldAddresses ? [] : MemberObjectSteps(implTask, members, profile);
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
            (List<string> memberNames, List<string> addresses) = MemberValues(members, profile);
            return GroupStep(operation, implTask, profile, memberNames, addresses);
        }

        /// <summary>
        /// Returns how a group refers to its members and their addresses: by name, by name and address (Azure), or only
        /// by their CIDR entries (OPNsense), which then also serve as the member names.
        /// </summary>
        private static (List<string> Names, List<string> Addresses) MemberValues(List<WfImplElement> members, FirewallVendorProfile profile)
        {
            if (profile.GroupsHoldAddresses)
            {
                List<string> entries = [.. members.SelectMany(FirewallElementValues.GroupAddressEntries).Distinct()];
                return (entries, entries);
            }
            if (profile.ReferencesObjectsByAddress)
            {
                return ([.. members.Where(element => !string.IsNullOrWhiteSpace(element.Name)).Select(element => element.Name!.Trim()).Distinct()],
                    [.. members.Select(FirewallElementValues.AddressNotation).Distinct()]);
            }
            return ([.. members.Select(element => FirewallElementValues.RequireName(element, profile)).Distinct()], []);
        }

        private static FirewallWriteStep GroupStep(FirewallWriteOperation operation, WfImplTask implTask, FirewallVendorProfile profile,
            List<string> memberNames, List<string> addresses)
        {
            string groupName = profile.NormalizeName(GroupName(implTask));
            List<string> renderedMembers = profile.ReferencesObjectsByAddress ? addresses : memberNames;

            FirewallWriteStep step = new(operation) { TargetName = groupName };
            if (renderedMembers.Count == 1)
            {
                step.SetText(FirewallWritePlaceholders.MemberName, renderedMembers[0]);
            }
            else
            {
                step.SetMissing(FirewallWritePlaceholders.MemberName, kNotOneMember, FirewallWriteErrorKind.Configuration);
            }
            if (operation == FirewallWriteOperation.GroupRemoveMembers)
            {
                step.MembersToRemove.AddRange(renderedMembers);
            }
            else
            {
                step.MembersToAdd.AddRange(renderedMembers);
            }
            return step.SetText(FirewallWritePlaceholders.GroupName, groupName)
                .SetText(FirewallWritePlaceholders.GroupNameInUrl, profile.NameInUrl(groupName))
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

        /// <summary>
        /// Plans the change of an existing rule, written as its complete target state; a task without a current element
        /// on one side is refused, see <see cref="FirewallRuleValues.EnsureCompleteTargetState"/>.
        /// </summary>
        private static List<FirewallWriteStep> PlanRuleModify(WfImplTask implTask, FirewallVendorProfile profile, FirewallWriteContext context)
        {
            FirewallRuleValues.EnsureCompleteTargetState(implTask);
            return [ExistingRuleStep(FirewallWriteOperation.RuleModify, implTask, profile, RuleUid(implTask), context)];
        }

        /// <summary>Plans the deletion of the existing rules a rule_delete task names, one step per rule in its imported layer.</summary>
        private static List<FirewallWriteStep> PlanRuleDelete(WfImplTask implTask, FirewallVendorProfile profile, FirewallWriteContext context)
        {
            List<string> ruleUids = implTask.GetRuleUids();
            if (ruleUids.Count == 0)
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Implementation task {implTask.Id} has no rule to delete.");
            }
            return [.. ruleUids.Select(uid => ExistingRuleStep(FirewallWriteOperation.RuleDelete, implTask, profile, uid, context))];
        }

        /// <summary>
        /// Plans the step of an existing rule. Where the vendor addresses rules within their layer, the step gets the layer
        /// of this rule from the imported configuration, as the rules of one task may lie in different layers; a rule
        /// missing in the import fails before anything is sent instead of being looked for in a guessed layer.
        /// </summary>
        private static FirewallWriteStep ExistingRuleStep(FirewallWriteOperation operation, WfImplTask implTask, FirewallVendorProfile profile,
            string ruleUid, FirewallWriteContext context)
        {
            FirewallWriteStep step = RuleStep(operation, implTask, profile, ruleUid);
            if (!profile.AddressesRulesInImportedLayer)
            {
                return step;
            }
            if (context.RuleLayerUids.TryGetValue(ruleUid, out string? layerUid))
            {
                step.SetText(FirewallWritePlaceholders.LayerName, layerUid);
            }
            else
            {
                step.SetMissing(FirewallWritePlaceholders.LayerName, $"the rule '{ruleUid}' is not found in the imported configuration of the management",
                    FirewallWriteErrorKind.Configuration);
            }
            return step;
        }

        private static FirewallWriteStep RuleStep(FirewallWriteOperation operation, WfImplTask implTask, FirewallVendorProfile profile, string ruleUid)
        {
            FirewallWriteStep step = new(operation) { TargetName = string.IsNullOrWhiteSpace(ruleUid) ? implTask.Title : ruleUid };
            step.SetText(FirewallWritePlaceholders.RuleName, implTask.Title)
                .SetRequiredText(FirewallWritePlaceholders.RuleUid, ruleUid, kNoRuleUid, FirewallWriteErrorKind.Configuration)
                .SetText(FirewallWritePlaceholders.RuleComment, Comment(implTask))
                .SetText(FirewallWritePlaceholders.RuleTracking, implTask.Tracking == kTrackNone ? profile.LogOff : profile.LogOn);
            if (implTask.RuleAction == null)
            {
                step.SetMissing(FirewallWritePlaceholders.RuleAction, kNoRuleAction);
            }
            else
            {
                step.SetText(FirewallWritePlaceholders.RuleAction, IsAccept(implTask.RuleAction.Value) ? profile.AcceptAction : profile.DropAction);
            }
            FirewallRuleValues.SetRuleElements(step, implTask, profile);
            return step;
        }

        private static bool IsAccept(int ruleAction) => ruleAction is kActionAccept or kActionAccess or kActionPermit;

        private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private static string RuleUid(WfImplTask implTask)
        {
            string? ruleUid = implTask.GetRuleUids().FirstOrDefault();
            return ruleUid ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Implementation task {implTask.Id} has no rule to modify.");
        }

        /// <summary>The network elements of a group task; service groups are refused.</summary>
        internal static List<WfImplElement> GroupMembers(WfImplTask implTask)
        {
            if (implTask.ImplElements.Any(FirewallElementValues.IsServiceElement))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                    $"Implementation task {implTask.Id}: service groups are not supported by the firewall write library.");
            }
            return [.. implTask.ImplElements.Where(FirewallElementValues.IsNetworkElement)];
        }

        /// <summary>The group a group task changes: the group name of its elements, else the task title.</summary>
        internal static string GroupName(WfImplTask implTask)
        {
            string? groupName = implTask.ImplElements.Select(element => element.GroupName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            groupName ??= implTask.Title;
            return string.IsNullOrWhiteSpace(groupName)
                ? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Implementation task {implTask.Id} has no group name.")
                : groupName.Trim();
        }

        internal static string Comment(WfImplTask implTask) => implTask.FreeText ?? "";
    }
}
