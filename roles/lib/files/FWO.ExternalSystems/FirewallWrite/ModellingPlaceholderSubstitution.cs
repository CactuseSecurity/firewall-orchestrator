using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Kind of objects a modelling placeholder area stands in for.</summary>
    public enum ModellingPlaceholderKind
    {
        /// <summary>User objects, e.g. Check Point access roles ("Network Areas for Users").</summary>
        User,
        /// <summary>Updatable objects maintained by the vendor ("Network Areas for Updatable Objects").</summary>
        UpdatableObject
    }

    /// <summary>
    /// A network area configured as placeholder in the modelling settings, with the rule sides it may be used on.
    /// </summary>
    /// <param name="IdString">Id string of the area, the group name a modelling request names it by.</param>
    /// <param name="Kind">Kind of objects the area stands in for.</param>
    /// <param name="InSource">Whether the area may be used in the source.</param>
    /// <param name="InDestination">Whether the area may be used in the destination.</param>
    public sealed record ModellingPlaceholderArea(string IdString, ModellingPlaceholderKind Kind, bool InSource, bool InDestination);

    /// <summary>What the substitution of the placeholders of an implementation task needs.</summary>
    /// <param name="Areas">Placeholder areas of the modelling settings.</param>
    /// <param name="ExtraConfigs">Extra configurations of the modelled connection of the request task; null if the
    /// request task has no modelled connection or it could not be used.</param>
    /// <param name="InterfaceExtraConfigs">Extra configurations of the interface the connection uses; empty without interface.</param>
    public sealed record ModellingPlaceholderInput(IReadOnlyList<ModellingPlaceholderArea> Areas, IReadOnlyList<ModellingExtraConfig>? ExtraConfigs,
        IReadOnlyList<ModellingExtraConfig> InterfaceExtraConfigs);

    /// <summary>Result of the substitution of the placeholders of an implementation task.</summary>
    /// <param name="ImplTask">The task to write: a copy with the placeholders substituted, or the task itself if nothing was substituted.</param>
    /// <param name="Note">Which placeholders were substituted by which objects; null if nothing was substituted.</param>
    /// <param name="NotWritableReason">Why the task cannot be written automatically; null if it can.</param>
    public sealed record ModellingPlaceholderResult(WfImplTask ImplTask, string? Note, string? NotWritableReason)
    {
        /// <summary>Whether the task can be written automatically.</summary>
        public bool IsWritable => NotWritableReason == null;
    }

    /// <summary>
    /// Substitutes the placeholder areas of network modelling in a rule to write by the real objects on the firewall. A
    /// placeholder area stands in for objects that cannot be modelled as network objects; the extra configurations of the
    /// connection name them ("…_user"/"…-user": user objects, "updatable…": updatable objects). A person implementing the
    /// request does this by hand, and the variance analysis accepts the real objects in place of the placeholder; the
    /// direct firewall write does it here, along the same rules. A rule is never written with a placeholder area: if the
    /// objects cannot be assigned unambiguously, or the connection has extra configurations that are not implemented
    /// automatically (NAT, free instructions), the task is not writable. Only new and modified rules are concerned; the
    /// elements of a rule to delete come from the implemented rule.
    /// </summary>
    public static class ModellingPlaceholderSubstitution
    {
        private static readonly List<string> SourceFields = [nameof(ElemFieldType.source), nameof(ElemFieldType.modelled_source)];
        private static readonly List<string> DestinationFields = [nameof(ElemFieldType.destination), nameof(ElemFieldType.modelled_destination)];
        private static readonly List<string> SubstitutedTaskTypes = [nameof(WfTaskType.access), nameof(WfTaskType.rule_modify)];
        private static readonly List<ModellingPlaceholderKind> Kinds = [ModellingPlaceholderKind.User, ModellingPlaceholderKind.UpdatableObject];
        private static readonly List<ModellingExtraConfig> NoExtraConfigs = [];

        private sealed record Placeholder(WfImplElement Element, ModellingPlaceholderArea Area, bool IsSource);

        /// <summary>Whether the placeholders of a task type are substituted: new and modified rules.</summary>
        public static bool AppliesTo(string taskType) => SubstitutedTaskTypes.Contains(taskType);

        /// <summary>
        /// Substitutes the placeholder areas of a new or modified rule by the objects the extra configurations name. Other
        /// tasks are returned unchanged.
        /// </summary>
        /// <param name="implTask">Implementation task to write.</param>
        /// <param name="input">Placeholder areas and extra configurations of the modelled connection.</param>
        /// <returns>The task to write with a note on the substitution, or the reason why it cannot be written automatically.</returns>
        public static ModellingPlaceholderResult Apply(WfImplTask implTask, ModellingPlaceholderInput input)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(input);
            if (!AppliesTo(implTask.TaskType))
            {
                return new(implTask, null, null);
            }
            string? reason = UnsupportedExtraConfigReason(input.ExtraConfigs ?? NoExtraConfigs, input.InterfaceExtraConfigs);
            if (reason != null)
            {
                return new(implTask, null, reason);
            }
            List<Placeholder> placeholders = FindPlaceholders(implTask, input.Areas);
            if (input.ExtraConfigs == null)
            {
                return placeholders.Count == 0
                    ? new(implTask, null, null)
                    : new(implTask, null, $"the rule contains the placeholder area {placeholders[0].Area.IdString}, but no modelled connection " +
                        "of the request task names the objects it stands in for; implement it manually.");
            }
            Dictionary<ModellingPlaceholderKind, List<string>> namesByKind = Kinds.ToDictionary(kind => kind, kind => ObjectNames(input.ExtraConfigs, kind));
            reason = Kinds
                .Select(kind => KindReason(kind, namesByKind[kind], placeholders.Where(p => p.Area.Kind == kind).ToList()))
                .FirstOrDefault(kindReason => kindReason != null);
            if (reason != null)
            {
                return new(implTask, null, reason);
            }
            return placeholders.Count == 0 ? new(implTask, null, null) : Substitute(implTask, placeholders, namesByKind);
        }

        /// <summary>
        /// Extra configurations that are not implemented automatically: NAT and free instructions of the connection, and
        /// any extra configuration of the interface it uses, whose objects the connection's request does not name.
        /// Documentation markers are no instructions.
        /// </summary>
        private static string? UnsupportedExtraConfigReason(IReadOnlyList<ModellingExtraConfig> extraConfigs, IReadOnlyList<ModellingExtraConfig> interfaceExtraConfigs)
        {
            ModellingExtraConfig? unsupported = extraConfigs.FirstOrDefault(config => !config.IsDocumentationOnly() && !IsSubstitutable(config));
            if (unsupported != null)
            {
                return $"the modelled connection has the extra configuration '{Describe(unsupported)}', which is not implemented automatically; implement it manually.";
            }
            ModellingExtraConfig? fromInterface = interfaceExtraConfigs.FirstOrDefault(config => !config.IsDocumentationOnly());
            if (fromInterface != null)
            {
                return $"the interface used by the modelled connection has the extra configuration '{Describe(fromInterface)}', " +
                    "which is not implemented automatically; implement it manually.";
            }
            return null;
        }

        /// <summary>Type and text of an extra configuration; only the type if it has no text, e.g. "NAT".</summary>
        private static string Describe(ModellingExtraConfig config) =>
            string.IsNullOrWhiteSpace(config.ExtraConfigText) ? config.ExtraConfigType : $"{config.ExtraConfigType}: {config.ExtraConfigText}";

        private static bool IsSubstitutable(ModellingExtraConfig config) => config.IsSpecialUser() || config.IsUpdatableObject();

        private static bool IsKind(ModellingExtraConfig config, ModellingPlaceholderKind kind) =>
            kind == ModellingPlaceholderKind.User ? config.IsSpecialUser() : config.IsUpdatableObject();

        /// <summary>The object names of one kind, each once, ignoring case like the firewall and the variance analysis.</summary>
        private static List<string> ObjectNames(IReadOnlyList<ModellingExtraConfig> extraConfigs, ModellingPlaceholderKind kind)
        {
            return [.. extraConfigs.Where(config => IsKind(config, kind))
                .Select(config => config.ExtraConfigText.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// The network elements of the rule that name a placeholder area by its id string: as a group without name and
        /// address, the way modelling requests name network areas.
        /// </summary>
        private static List<Placeholder> FindPlaceholders(WfImplTask implTask, IReadOnlyList<ModellingPlaceholderArea> areas)
        {
            List<Placeholder> placeholders = [];
            foreach (WfImplElement element in implTask.ImplElements.Where(IsGroupReference))
            {
                bool isSource = SourceFields.Contains(element.Field);
                ModellingPlaceholderArea? area = areas.FirstOrDefault(a => string.Equals(a.IdString, element.GroupName!.Trim(), StringComparison.OrdinalIgnoreCase));
                if (area != null && (isSource || DestinationFields.Contains(element.Field)))
                {
                    placeholders.Add(new(element, area, isSource));
                }
            }
            return placeholders;
        }

        private static bool IsGroupReference(WfImplElement element) =>
            string.IsNullOrWhiteSpace(element.Name) && string.IsNullOrWhiteSpace(element.IpString) && !string.IsNullOrWhiteSpace(element.GroupName);

        /// <summary>
        /// Why the placeholders of one kind cannot be substituted unambiguously; null if they can. The extra configurations
        /// name objects without side, so all placeholders of a kind must be on one side, where the settings allow them. As
        /// in the variance analysis, a placeholder area stands in for any number of objects, but one object must not stand
        /// in for several areas.
        /// </summary>
        private static string? KindReason(ModellingPlaceholderKind kind, List<string> names, List<Placeholder> placeholders)
        {
            string kindText = KindText(kind);
            Placeholder? notAllowed = placeholders.FirstOrDefault(p => p.IsSource ? !p.Area.InSource : !p.Area.InDestination);
            if (notAllowed != null)
            {
                return $"the placeholder area {notAllowed.Area.IdString} is not allowed in the {SideText(notAllowed.IsSource)} by the modelling settings; implement it manually.";
            }
            if (placeholders.Select(p => p.IsSource).Distinct().Count() > 1)
            {
                return $"placeholder areas for {kindText} are used in source and destination, and the extra configurations do not tell " +
                    "which side their objects belong to; implement it manually.";
            }
            if (placeholders.Count > 0 && names.Count == 0)
            {
                return $"no extra configuration names the {kindText} the placeholder area {placeholders[0].Area.IdString} stands in for; implement it manually.";
            }
            if (placeholders.Count == 0 && names.Count > 0)
            {
                return $"extra configurations name {kindText} ({string.Join(", ", names)}), but the rule contains no placeholder area for them; implement it manually.";
            }
            int areaCount = placeholders.Select(p => p.Area.IdString.ToLowerInvariant()).Distinct().Count();
            if (names.Count < areaCount)
            {
                return $"{names.Count} {kindText} are named for {areaCount} placeholder areas, but each placeholder area needs an object of its own; implement it manually.";
            }
            return null;
        }

        private static string KindText(ModellingPlaceholderKind kind) => kind == ModellingPlaceholderKind.User ? "user objects" : "updatable objects";

        private static string SideText(bool isSource) => isSource ? "source" : "destination";

        /// <summary>
        /// Returns a copy of the task whose placeholder elements are replaced by elements naming the objects of their kind,
        /// on the side and with the action of the placeholder.
        /// </summary>
        private static ModellingPlaceholderResult Substitute(WfImplTask implTask, List<Placeholder> placeholders,
            Dictionary<ModellingPlaceholderKind, List<string>> namesByKind)
        {
            HashSet<WfImplElement> placeholderElements = [.. placeholders.Select(p => p.Element)];
            List<WfImplElement> elements = [.. implTask.ImplElements.Where(element => !placeholderElements.Contains(element))];
            foreach (Placeholder placeholder in placeholders)
            {
                foreach (string name in namesByKind[placeholder.Area.Kind].Where(name => !ContainsObject(elements, placeholder.Element, name)))
                {
                    elements.Add(ObjectElement(placeholder.Element, name));
                }
            }
            WfImplTask substituted = new(implTask) { ImplElements = elements };
            return new(substituted, Note(placeholders, namesByKind), null);
        }

        private static bool ContainsObject(List<WfImplElement> elements, WfImplElement placeholder, string name) =>
            elements.Any(element => element.KeepNameVerbatim && element.Field == placeholder.Field && element.ImplAction == placeholder.ImplAction
                && string.Equals(element.GroupName, name, StringComparison.OrdinalIgnoreCase));

        private static WfImplElement ObjectElement(WfImplElement placeholder, string name)
        {
            return new WfImplElement
            {
                ImplTaskId = placeholder.ImplTaskId,
                ImplAction = placeholder.ImplAction,
                Field = placeholder.Field,
                GroupName = name,
                KeepNameVerbatim = true
            };
        }

        private static string Note(List<Placeholder> placeholders, Dictionary<ModellingPlaceholderKind, List<string>> namesByKind)
        {
            IEnumerable<string> substitutions = placeholders
                .Select(p => p.Area)
                .DistinctBy(area => area.IdString.ToLowerInvariant())
                .Select(area => $"{area.IdString} by {string.Join(", ", namesByKind[area.Kind])}");
            return $"Modelling placeholders substituted: {string.Join("; ", substitutions)}.";
        }
    }
}
