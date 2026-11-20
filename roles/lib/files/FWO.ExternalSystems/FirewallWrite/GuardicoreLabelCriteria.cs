using FWO.Basics;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>A dynamic criterion of a Guardicore label.</summary>
    /// <param name="Field">Criterion field, e.g. numeric_ip_addresses.</param>
    /// <param name="Op">Operator, e.g. SUBNET or RANGE.</param>
    /// <param name="Argument">Argument, e.g. "10.0.0.0/24".</param>
    /// <param name="ReadOnly">Whether Guardicore manages the criterion itself.</param>
    public sealed record GuardicoreCriterion(string Field, string Op, string Argument, bool ReadOnly = false);

    /// <summary>The change of an existing label, see <see cref="GuardicoreLabelCriteria.PlanUpdate"/>.</summary>
    /// <param name="IpCriteria">The IP criteria the label is to have.</param>
    /// <param name="IpCriteriaChange">Whether these differ from the current IP criteria of the label.</param>
    /// <param name="RemovesStaticAssignments">Whether the static asset assignments of the label are removed.</param>
    /// <param name="NotRemovable">Addresses to remove that neither an IP criterion nor a removed static assignment holds.</param>
    public sealed record GuardicoreLabelUpdate(List<GuardicoreCriterion> IpCriteria, bool IpCriteriaChange, bool RemovesStaticAssignments,
        List<IpInterval> NotRemovable)
    {
        /// <summary>Whether the label already is as requested, so nothing is sent.</summary>
        public bool WritesNothing => !IpCriteriaChange && !RemovesStaticAssignments;
    }

    /// <summary>
    /// IP criteria of Guardicore labels: one criterion per address entry, "SUBNET" with a CIDR block ("1.2.3.4/32",
    /// "2.3.4.0/24") or "RANGE" with "start-end" for other ranges. FWO manages the IP criteria that are not read-only;
    /// when it replaces the criteria of a label, all other criteria are written back unchanged, and so are the static
    /// asset assignments ("static_criteria": field id EQUALS asset id) unless the label is one FWO owns (app role, app
    /// zone, network area), which hold IP criteria only.
    /// </summary>
    public static class GuardicoreLabelCriteria
    {
        /// <summary>Criterion field of IP addresses.</summary>
        public const string kIpField = "numeric_ip_addresses";

        /// <summary>Operator of CIDR blocks.</summary>
        public const string kSubnetOp = "SUBNET";

        /// <summary>Operator of address ranges.</summary>
        public const string kRangeOp = "RANGE";

        private const string kEqualsOp = "EQUALS";
        private static readonly HashSet<string> IpOperators = [kSubnetOp, kRangeOp, kEqualsOp];

        /// <summary>The properties of a criterion that are written back; ids and flags set by Guardicore are not.</summary>
        private static readonly List<string> WritableProperties = ["field", "op", "argument", "compound_criteria"];

        /// <summary>Returns the IP criterion of an address entry.</summary>
        public static GuardicoreCriterion ToCriterion(IpInterval interval)
        {
            string argument = IpAddressSet.Format(interval);
            return new GuardicoreCriterion(kIpField, argument.Contains('/') ? kSubnetOp : kRangeOp, argument);
        }

        /// <summary>
        /// Parses address entries ("1.2.3.4/32", "10.0.0.0/24", "10.0.0.1-10.0.0.9").
        /// </summary>
        /// <exception cref="FirewallWriteException">Thrown for an entry that is no address.</exception>
        public static List<IpInterval> ParseEntries(IEnumerable<string> entries)
        {
            List<IpInterval> intervals = [];
            foreach (string entry in entries)
            {
                if (!IpAddressSet.TryParseInterval(entry, out IpInterval interval))
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"'{entry}' is no IP address, network or range.");
                }
                intervals.Add(interval);
            }
            return intervals;
        }

        /// <summary>
        /// Returns the address entries a new label is created with: the members in their order, without those already
        /// covered by an earlier member.
        /// </summary>
        public static List<IpInterval> CreationEntries(IEnumerable<IpInterval> members)
        {
            IpAddressSet covered = new();
            List<IpInterval> entries = [];
            foreach (IpInterval member in members.Where(member => !covered.Contains(member)))
            {
                entries.Add(member);
                covered.Add(member);
            }
            return entries;
        }

        /// <summary>The IP criteria of a new label as JSON array.</summary>
        public static string CreationJson(IEnumerable<IpInterval> members)
        {
            JsonArray criteria = [.. CreationEntries(members).Select(entry => (JsonNode?)ToJson(ToCriterion(entry)))];
            return criteria.ToJsonString();
        }

        /// <summary>
        /// Returns the IP criteria that give an existing label its target addresses, or null if it already has them. The
        /// target is the members to keep (<paramref name="replace"/>, e.g. for a newly created group), else the current
        /// addresses without the members to remove plus the members to keep. Current IP criteria inside the target stay
        /// as they are, missing members are added as one criterion each, and addresses of the target that are still not
        /// covered (the rest of a criterion that was partly removed) are added as further criteria.
        /// </summary>
        /// <param name="current">Dynamic criteria of the label.</param>
        /// <param name="keep">Members the label is to hold.</param>
        /// <param name="remove">Members the label is no longer to hold.</param>
        /// <param name="replace">True to give the label exactly the members to keep.</param>
        public static List<GuardicoreCriterion>? TargetIpCriteria(IReadOnlyList<GuardicoreCriterion> current, List<IpInterval> keep,
            List<IpInterval> remove, bool replace)
        {
            List<(GuardicoreCriterion Criterion, IpInterval Interval)> managed = ManagedCriteria(current);
            IpAddressSet currentSet = IpAddressSet.Of(managed.Select(entry => entry.Interval));
            IpAddressSet target = replace ? new IpAddressSet() : currentSet.Copy();
            target.Remove(IpAddressSet.Of(remove));
            target.Add(IpAddressSet.Of(keep));
            if (target.SetEquals(currentSet))
            {
                return null;
            }

            List<(GuardicoreCriterion Criterion, IpInterval Interval)> kept = [.. managed.Where(entry => target.Contains(entry.Interval))];
            List<GuardicoreCriterion> criteria = [.. kept.Select(entry => entry.Criterion)];
            IpAddressSet covered = IpAddressSet.Of(kept.Select(entry => entry.Interval));
            foreach (IpInterval member in keep.Where(member => !covered.Contains(member)))
            {
                criteria.Add(ToCriterion(member));
                covered.Add(member);
            }
            IpAddressSet rest = target.Copy();
            rest.Remove(covered);
            criteria.AddRange(rest.Intervals.Select(ToCriterion));
            return criteria;
        }

        /// <summary>
        /// Plans the change of an existing label. The imported members of a label are the addresses of its IP criteria
        /// plus all addresses of the assets carrying it, so the variance analysis may ask to remove addresses that no IP
        /// criterion holds. A label FWO owns loses its static asset assignments, which may hold them; any other address
        /// to remove that no IP criterion holds and that is not kept stems from an asset carrying the label through
        /// another of its addresses, an implicit criterion or a static assignment that is kept, and cannot be removed.
        /// </summary>
        /// <param name="label">The label as read from Guardicore.</param>
        /// <param name="keep">Members the label is to hold.</param>
        /// <param name="remove">Members the label is no longer to hold.</param>
        /// <param name="replace">True to give the label exactly the members to keep.</param>
        /// <param name="ownsAssignments">True for labels FWO owns (app roles, app zones, network areas): their static asset assignments are removed.</param>
        public static GuardicoreLabelUpdate PlanUpdate(JsonNode label, List<IpInterval> keep, List<IpInterval> remove, bool replace, bool ownsAssignments)
        {
            List<GuardicoreCriterion> current = ReadCriteria(label);
            List<GuardicoreCriterion>? target = TargetIpCriteria(current, keep, remove, replace);
            bool removesStaticAssignments = ownsAssignments && StaticAssignmentsOf(label).Any();
            List<IpInterval> notRemovable = removesStaticAssignments ? [] : NotHeldByIpCriteria(current, keep, remove);
            List<GuardicoreCriterion> ipCriteria = target ?? [.. ManagedCriteria(current).Select(entry => entry.Criterion)];
            return new GuardicoreLabelUpdate(ipCriteria, target != null, removesStaticAssignments, notRemovable);
        }

        /// <summary>
        /// Returns the body of the bulk update that replaces the criteria of a label: its criteria that FWO does not
        /// manage and its static asset assignments, written back unchanged, followed by the given IP criteria.
        /// </summary>
        /// <param name="key">Label key.</param>
        /// <param name="value">Label value.</param>
        /// <param name="label">The label as read from Guardicore.</param>
        /// <param name="ipCriteria">The IP criteria the label is to have.</param>
        /// <param name="keepStaticAssignments">False to leave out the static asset assignments, which removes them.</param>
        public static string UpdateJson(string key, string value, JsonNode label, IEnumerable<GuardicoreCriterion> ipCriteria, bool keepStaticAssignments = true)
        {
            JsonArray criteria = [];
            foreach (JsonObject criterion in CriteriaOf(label).Where(criterion => !IsManaged(ToCriterion(criterion), out _)))
            {
                criteria.Add(Writable(criterion));
            }
            if (keepStaticAssignments)
            {
                foreach (JsonObject assignment in StaticAssignmentsOf(label))
                {
                    criteria.Add(Writable(assignment));
                }
            }
            foreach (GuardicoreCriterion criterion in ipCriteria)
            {
                criteria.Add(ToJson(criterion));
            }
            JsonObject update = new() { ["key"] = key, ["value"] = value, ["criteria"] = criteria };
            JsonArray body = [update];
            return body.ToJsonString();
        }

        /// <summary>
        /// Reads the dynamic criteria of a label from its JSON ("dynamic_criteria": [{field, op, argument, read_only}]).
        /// </summary>
        public static List<GuardicoreCriterion> ReadCriteria(JsonNode? label) => [.. CriteriaOf(label).Select(ToCriterion)];

        /// <summary>Returns the text of a JSON string or number; empty for anything else.</summary>
        public static string Text(JsonNode? node)
        {
            if (node is not JsonValue value)
            {
                return "";
            }
            return value.TryGetValue(out string? text) ? text : node.ToJsonString();
        }

        private static IEnumerable<JsonObject> CriteriaOf(JsonNode? label) =>
            label?["dynamic_criteria"] is JsonArray criteria ? criteria.OfType<JsonObject>() : [];

        /// <summary>The static asset assignments of a label ("static_criteria": field id EQUALS asset id).</summary>
        private static IEnumerable<JsonObject> StaticAssignmentsOf(JsonNode? label) =>
            label?["static_criteria"] is JsonArray assignments ? assignments.OfType<JsonObject>() : [];

        /// <summary>The addresses to remove that are not kept and that no IP criterion FWO manages holds.</summary>
        private static List<IpInterval> NotHeldByIpCriteria(IReadOnlyList<GuardicoreCriterion> current, List<IpInterval> keep, List<IpInterval> remove)
        {
            IpAddressSet notHeld = IpAddressSet.Of(remove);
            notHeld.Remove(IpAddressSet.Of(keep));
            notHeld.Remove(IpAddressSet.Of(ManagedCriteria(current).Select(entry => entry.Interval)));
            return [.. notHeld.Intervals];
        }

        private static GuardicoreCriterion ToCriterion(JsonObject criterion) => new(Text(criterion["field"]), Text(criterion["op"]),
            Text(criterion["argument"]), criterion["read_only"] is JsonValue readOnly && readOnly.TryGetValue(out bool isReadOnly) && isReadOnly);

        private static JsonObject ToJson(GuardicoreCriterion criterion) =>
            new() { ["field"] = criterion.Field, ["op"] = criterion.Op, ["argument"] = criterion.Argument };

        private static JsonObject Writable(JsonObject criterion)
        {
            JsonObject writable = [];
            foreach (string property in WritableProperties.Where(criterion.ContainsKey))
            {
                writable[property] = criterion[property]?.DeepClone();
            }
            return writable;
        }

        /// <summary>Whether FWO manages a criterion: an IP criterion that is not read-only and whose argument is an address.</summary>
        private static bool IsManaged(GuardicoreCriterion criterion, out IpInterval interval)
        {
            interval = default;
            return !criterion.ReadOnly && criterion.Field == kIpField && IpOperators.Contains(criterion.Op)
                && IpAddressSet.TryParseInterval(criterion.Argument, out interval);
        }

        private static List<(GuardicoreCriterion Criterion, IpInterval Interval)> ManagedCriteria(IReadOnlyList<GuardicoreCriterion> current)
        {
            List<(GuardicoreCriterion, IpInterval)> managed = [];
            foreach (GuardicoreCriterion criterion in current)
            {
                if (IsManaged(criterion, out IpInterval interval))
                {
                    managed.Add((criterion, interval));
                }
            }
            return managed;
        }
    }
}
