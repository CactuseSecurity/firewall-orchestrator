using System.Collections.Frozen;
using System.Net.Sockets;
using System.Numerics;
using System.Text.Json.Nodes;
using FWO.Basics;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Plans the rule steps of vendors that write labels (Guardicore). All ports of a Guardicore rule apply to all its
    /// protocols, so a modelled connection is written as one rule per protocol, each with only the ports of that protocol,
    /// in the Allow section. The rules of a connection are found again by its marker in the rule comment:
    /// <list type="bullet">
    /// <item>access and rule_modify tasks get one <see cref="FirewallWriteOperation.RuleCreate"/> step per protocol of their
    /// target services, which writes the rule of this protocol (created, or the existing one replaced), followed by one
    /// <see cref="FirewallWriteOperation.RuleDelete"/> step that deletes the other rules of the connection,</item>
    /// <item>rule_delete tasks get only the <see cref="FirewallWriteOperation.RuleDelete"/> step, which deletes all rules of
    /// the connection.</item>
    /// </list>
    /// Sources and destinations name labels by "key: value" (the client replaces them by the label ids) and addresses as
    /// CIDR subnets; a side with neither fails instead of being widened to any.
    /// </summary>
    public static class LabelRuleStepPlanner
    {
        private const int kMaxPrefixBits = 128;
        private const int kIpv4Bits = 32;
        /// <summary>Most CIDR blocks an interval can need: at most two per prefix length, one on each side of the largest block.</summary>
        private const int kMaxCidrBlocks = 2 * kMaxPrefixBits;
        private const string kIcmp = "ICMP";

        private static readonly FrozenDictionary<int, string> ProtocolNames = new Dictionary<int, string>
        {
            { 1, kIcmp },
            { 6, "TCP" },
            { 17, "UDP" }
        }.ToFrozenDictionary();

        /// <summary>Plans the rule steps of an access, rule_modify or rule_delete task.</summary>
        /// <exception cref="FirewallWriteException">Thrown for a task without connection marker or app, a side without
        /// labels and addresses, and services of a protocol Guardicore rules cannot express.</exception>
        public static List<FirewallWriteStep> Plan(WfImplTask implTask, FirewallWriteContext context, FirewallVendorProfile profile)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(profile);
            string marker = context.ConnectionMarker ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                $"Implementation task {implTask.Id}: rules are only written for modelled connections, but its request task names no connection marker.");
            List<FirewallWriteStep> steps = [];
            if (implTask.TaskType != nameof(WfTaskType.rule_delete))
            {
                steps.AddRange(RuleSteps(implTask, context, profile, marker));
            }
            List<string> keptProtocols = [.. steps.Select(step => step.Values[FirewallWritePlaceholders.RuleProtocol].Value)];
            steps.Add(OtherRulesDeleteStep(marker, keptProtocols));
            return steps;
        }

        /// <summary>One rule step per protocol of the target services of the task.</summary>
        private static List<FirewallWriteStep> RuleSteps(WfImplTask implTask, FirewallWriteContext context, FirewallVendorProfile profile, string marker)
        {
            string appId = context.ExtAppId ?? throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                $"Implementation task {implTask.Id}: its request task names no app (external app id), which is the ruleset of the rule.");
            List<WfImplElement> target = [.. implTask.ImplElements.Where(element => element.ImplAction != nameof(RequestAction.delete))];
            string sources = SideJson(target, nameof(ElemFieldType.source), implTask);
            string destinations = SideJson(target, nameof(ElemFieldType.destination), implTask);
            List<FirewallWriteStep> steps = [];
            foreach ((string protocol, ProtocolPorts ports) in PortsByProtocol(target, implTask))
            {
                FirewallWriteStep step = new(FirewallWriteOperation.RuleCreate) { TargetName = $"{marker} {protocol}" };
                step.SetText(FirewallWritePlaceholders.RuleName, appId)
                    .SetText(FirewallWritePlaceholders.RuleComment, marker)
                    .SetText(FirewallWritePlaceholders.RuleAction, profile.AcceptAction)
                    .SetText(FirewallWritePlaceholders.RuleProtocol, protocol)
                    .SetJson(FirewallWritePlaceholders.RulePorts, ports.PortsJson())
                    .SetJson(FirewallWritePlaceholders.RulePortRanges, ports.RangesJson())
                    .SetJson(FirewallWritePlaceholders.RuleSources, sources)
                    .SetJson(FirewallWritePlaceholders.RuleDestinations, destinations);
                steps.Add(step);
            }
            return steps;
        }

        /// <summary>
        /// The step that deletes the rules of the connection the rule steps did not write; it names the marker and the
        /// protocols written before (comma separated, empty for a rule_delete task).
        /// </summary>
        private static FirewallWriteStep OtherRulesDeleteStep(string marker, List<string> keptProtocols)
        {
            string target = keptProtocols.Count == 0 ? $"{marker} all rules" : $"{marker} rules of other protocols than {string.Join(", ", keptProtocols)}";
            return new FirewallWriteStep(FirewallWriteOperation.RuleDelete) { TargetName = target }
                .SetText(FirewallWritePlaceholders.RuleComment, marker)
                .SetText(FirewallWritePlaceholders.RuleProtocol, string.Join(",", keptProtocols));
        }

        /// <summary>
        /// A rule side: the label groups as OR of AND groups, and the addresses as CIDR subnets. The elements of an AND-group
        /// share its group name ("OS: Linux &amp; Stage: Prod", see <see cref="LabelAndGroups"/>) and name one label each; an
        /// element of a group without label name (e.g. an app role "AppRole: AR4711-001") stands for the label of that name.
        /// </summary>
        private static string SideJson(List<WfImplElement> elements, string field, WfImplTask implTask)
        {
            List<WfImplElement> sideElements = [.. elements.Where(element => element.Field == field)];
            Dictionary<string, List<string>> labelGroups = [];
            List<string> groupOrder = [];
            List<string> subnets = [];
            foreach (WfImplElement element in sideElements)
            {
                if (IpAddressSet.TryParseInterval(element.IpString, element.IpEnd, out IpInterval interval))
                {
                    subnets.AddRange(CidrBlocks(interval));
                }
                else if (element.GroupName?.Contains(DynamicIpLabel.kKeyValueSeparator, StringComparison.Ordinal) == true)
                {
                    AddToLabelGroup(labelGroups, groupOrder, element);
                }
                else
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Implementation task {implTask.Id}: the {field} " +
                        $"'{element.GroupName ?? element.Name}' is neither a label (\"key{DynamicIpLabel.kKeyValueSeparator}value\") nor an address.");
                }
            }
            if (labelGroups.Count == 0 && subnets.Count == 0)
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData,
                    $"Implementation task {implTask.Id} has no {field}; a rule side is not widened to any.");
            }
            JsonObject side = [];
            if (labelGroups.Count > 0)
            {
                JsonArray orLabels = [.. groupOrder.Select(group => (JsonNode)new JsonObject
                {
                    ["and_labels"] = new JsonArray([.. LabelsOfGroup(group, labelGroups[group]).Select(label => (JsonNode)JsonValue.Create(label))])
                })];
                side["labels"] = new JsonObject { ["or_labels"] = orLabels };
            }
            if (subnets.Count > 0)
            {
                side["subnets"] = new JsonArray([.. subnets.Distinct(StringComparer.Ordinal).Select(subnet => (JsonNode)JsonValue.Create(subnet))]);
            }
            return side.ToJsonString();
        }

        private static void AddToLabelGroup(Dictionary<string, List<string>> labelGroups, List<string> groupOrder, WfImplElement element)
        {
            string groupName = element.GroupName!.Trim();
            if (!labelGroups.TryGetValue(groupName, out List<string>? labels))
            {
                labels = [];
                labelGroups[groupName] = labels;
                groupOrder.Add(groupName);
            }
            string? labelName = element.Name?.Trim();
            if (labelName?.Contains(DynamicIpLabel.kKeyValueSeparator, StringComparison.Ordinal) == true && !labels.Contains(labelName, StringComparer.Ordinal))
            {
                labels.Add(labelName);
            }
        }

        /// <summary>The labels of an AND-group: the label names of its elements, or the group itself if they name none.</summary>
        private static List<string> LabelsOfGroup(string groupName, List<string> labelNames) => labelNames.Count > 0 ? labelNames : [groupName];

        /// <summary>The ports of the service elements grouped by protocol, in protocol order.</summary>
        private static SortedDictionary<string, ProtocolPorts> PortsByProtocol(List<WfImplElement> elements, WfImplTask implTask)
        {
            SortedDictionary<string, ProtocolPorts> portsByProtocol = new(StringComparer.Ordinal);
            foreach (WfImplElement service in elements.Where(FirewallElementValues.IsServiceElement))
            {
                if (service.ProtoId is not int protoId || !ProtocolNames.TryGetValue(protoId, out string? protocol))
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported, $"Implementation task {implTask.Id}: the service " +
                        $"'{service.Name}' has protocol {service.ProtoId?.ToString() ?? "none"}; Guardicore rules are written for TCP, UDP and ICMP only.");
                }
                if (!portsByProtocol.TryGetValue(protocol, out ProtocolPorts? ports))
                {
                    ports = new ProtocolPorts(protocol == kIcmp);
                    portsByProtocol[protocol] = ports;
                }
                ports.Add(service.Port, service.PortEnd);
            }
            if (portsByProtocol.Count == 0)
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Implementation task {implTask.Id} has no service.");
            }
            return portsByProtocol;
        }

        /// <summary>
        /// Splits an address interval into CIDR blocks, as Guardicore rule subnets are CIDR notations: each block is the
        /// largest aligned block starting at the current address that does not pass the end of the interval.
        /// </summary>
        public static List<string> CidrBlocks(IpInterval interval)
        {
            int bits = interval.Family == AddressFamily.InterNetworkV6 ? kMaxPrefixBits : kIpv4Bits;
            List<string> blocks = [];
            BigInteger start = interval.Start;
            for (int count = 0; start <= interval.End && count <= kMaxCidrBlocks; count++)
            {
                int size = 0;
                while (size < bits && (start & (BigInteger.One << size)) == 0 && start + (BigInteger.One << (size + 1)) - 1 <= interval.End)
                {
                    size++;
                }
                BigInteger end = start + (BigInteger.One << size) - 1;
                blocks.Add(IpAddressSet.Format(new IpInterval(interval.Family, start, end)));
                start = end + 1;
            }
            return blocks;
        }

        /// <summary>The ports of one protocol: single ports and ranges; a service without port, and ICMP, allow all.</summary>
        private sealed class ProtocolPorts(bool withoutPorts)
        {
            private readonly SortedSet<int> ports = [];
            private readonly SortedSet<(int Start, int End)> ranges = [];
            private bool allPorts = withoutPorts;

            public void Add(int? port, int? portEnd)
            {
                if (port is not int first || first <= 0)
                {
                    allPorts = true;
                }
                else if (portEnd is int last && last > first)
                {
                    ranges.Add((first, last));
                }
                else
                {
                    ports.Add(first);
                }
            }

            public string PortsJson() => allPorts ? "[]" : new JsonArray([.. ports.Select(port => (JsonNode)JsonValue.Create(port))]).ToJsonString();

            public string RangesJson() => allPorts
                ? "[]"
                : new JsonArray([.. ranges.Select(range => (JsonNode)new JsonObject { ["start"] = range.Start, ["end"] = range.End })]).ToJsonString();
        }
    }
}
