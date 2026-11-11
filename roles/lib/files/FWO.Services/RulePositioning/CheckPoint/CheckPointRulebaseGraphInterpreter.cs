using System.Collections.ObjectModel;
using FWO.Data;
using FWO.Data.RulePositioning;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>
/// Immutable Check Point interpretation of a normalized rulebase graph.
/// </summary>
internal sealed class CheckPointGraphStructure
{
    /// <summary>Gets all interpreted selectable layers.</summary>
    internal IReadOnlyList<CheckPointLayerStructure> Layers { get; }
    /// <summary>Gets the unique initial layer.</summary>
    internal CheckPointLayerStructure InitialLayer { get; }

    /// <summary>Creates an immutable interpreted graph.</summary>
    private CheckPointGraphStructure(
        IEnumerable<CheckPointLayerStructure> layers,
        CheckPointLayerStructure initialLayer)
    {
        Layers = new ReadOnlyCollection<CheckPointLayerStructure>(layers.ToList());
        InitialLayer = initialLayer;
    }

    /// <summary>
    /// Interprets Check Point layer, section, and inline relationships without performing I/O.
    /// </summary>
    internal static bool TryCreate(
        RulebaseGraphSnapshot snapshot,
        out CheckPointGraphStructure? structure)
    {
        structure = null;
        GraphInterpreter interpreter = new(snapshot);
        if (!interpreter.TryBuild(out List<CheckPointLayerStructure> layers, out CheckPointLayerStructure? initialLayer) ||
            initialLayer == null)
        {
            return false;
        }
        structure = new CheckPointGraphStructure(layers, initialLayer);
        return true;
    }

    /// <summary>Builds the interpreted graph while enforcing structural invariants.</summary>
    private sealed class GraphInterpreter
    {
        private readonly RulebaseGraphSnapshot snapshot;
        private readonly Dictionary<long, RulebaseGraphNode> rulebasesById;
        private readonly Dictionary<long, RulebaseGraphLink> inlineLinksByRuleId;
        private readonly bool hasValidInlineLinks;
        private readonly List<CheckPointLayerStructure> layers = [];
        private readonly HashSet<long> interpretedRulebaseIds = [];

        /// <summary>Creates an interpreter for one immutable graph snapshot.</summary>
        internal GraphInterpreter(RulebaseGraphSnapshot snapshot)
        {
            this.snapshot = snapshot;
            rulebasesById = snapshot.Rulebases.ToDictionary(rulebase => rulebase.Id);
            (inlineLinksByRuleId, hasValidInlineLinks) = BuildInlineLinkLookup(snapshot.Links);
        }

        /// <summary>
        /// Builds the unique selectable layer graph from its initial relationship.
        /// </summary>
        internal bool TryBuild(
            out List<CheckPointLayerStructure> interpretedLayers,
            out CheckPointLayerStructure? initialLayer)
        {
            interpretedLayers = layers;
            initialLayer = null;
            if (!TryValidateTopology())
            {
                return false;
            }
            if (!TryResolveInitialLayerId(out long initialLayerId))
            {
                return false;
            }
            if (!TryAddLayer(initialLayerId, out initialLayer))
            {
                return false;
            }
            if (!TryAddOrderedLayers(initialLayerId))
            {
                return false;
            }
            RulePositioningDiagnosticLog.WriteInfo(
                "topology.validation",
                "success",
                Facts(
                    ("rulebase_count", snapshot.Rulebases.Count),
                    ("link_count", snapshot.Links.Count),
                    ("rule_count", snapshot.Rulebases.Sum(Rulebase => Rulebase.Rules.Count)),
                    ("initial_layer_id", initialLayerId),
                    ("interpreted_layer_count", layers.Count),
                    ("interpreted_section_count", layers.Sum(Layer => Layer.Sections.Count))));
            return true;
        }

        /// <summary>Resolves the first selectable layer from the unique initial relationship.</summary>
        private bool TryResolveInitialLayerId(out long initialLayerId)
        {
            initialLayerId = 0;
            List<RulebaseGraphLink> initialLinks = snapshot.Links.Where(link => link.IsInitial).ToList();
            if (initialLinks.Count != 1 || initialLinks[0].IsSection ||
                initialLinks[0].LinkType == RulebaseLinkTypes.Inline)
            {
                TopologyFailure(
                    initialLinks.Count != 1 ? "initial_link_cardinality" : "initial_link_invalid",
                    ("initial_link_count", initialLinks.Count),
                    ("first_target_rulebase_id", initialLinks.FirstOrDefault()?.TargetRulebaseId ?? 0),
                    ("first_link_type", initialLinks.FirstOrDefault()?.LinkType ?? 0),
                    ("first_is_section", initialLinks.FirstOrDefault()?.IsSection ?? false));
                return false;
            }

            initialLayerId = initialLinks[0].TargetRulebaseId;
            if (initialLinks[0].LinkType != RulebaseLinkTypes.Policy)
            {
                return true;
            }
            RulebaseGraphLink? policySuccessor = GetUniqueOrderedSuccessor(initialLayerId);
            if (policySuccessor == null)
            {
                TopologyFailure("policy_successor_missing", ("policy_rulebase_id", initialLayerId));
                return false;
            }
            initialLayerId = policySuccessor.TargetRulebaseId;
            return true;
        }

        /// <summary>Adds the ordered layer chain after the initial layer.</summary>
        private bool TryAddOrderedLayers(long initialLayerId)
        {
            long currentLayerId = initialLayerId;
            HashSet<long> orderedLayerIds = [currentLayerId];
            while (true)
            {
                RulebaseGraphLink? successor = GetUniqueOrderedSuccessor(currentLayerId);
                if (successor == null)
                {
                    return true;
                }
                currentLayerId = successor.TargetRulebaseId;
                if (!orderedLayerIds.Add(currentLayerId))
                {
                    TopologyFailure("ordered_cycle", ("rulebase_id", currentLayerId));
                    return false;
                }
                if (!TryAddLayer(currentLayerId, out _))
                {
                    TopologyFailure("ordered_layer_invalid", ("rulebase_id", currentLayerId));
                    return false;
                }
            }
        }

        /// <summary>
        /// Adds one layer and all attached inline layers without using the call stack.
        /// Traversal is bounded by the number of rulebases in the validated snapshot.
        /// </summary>
        private bool TryAddLayer(long rulebaseId, out CheckPointLayerStructure? layer)
        {
            layer = null;
            Stack<(long RulebaseId, int Depth)> pendingLayers = new();
            pendingLayers.Push((rulebaseId, 1));
            while (pendingLayers.TryPop(out (long RulebaseId, int Depth) pending))
            {
                if (pending.Depth > rulebasesById.Count ||
                    !TryCreateLayer(pending.RulebaseId, out CheckPointLayerStructure? currentLayer))
                {
                    TopologyFailure(
                        pending.Depth > rulebasesById.Count ? "inline_depth_exceeded" : "inline_layer_invalid",
                        ("rulebase_id", pending.RulebaseId),
                        ("depth", pending.Depth));
                    return false;
                }

                layer ??= currentLayer;
                List<long> inlineLayerIds = currentLayer!.OrderedRules
                    .Where(rule => inlineLinksByRuleId.ContainsKey(rule.Id))
                    .Select(rule => inlineLinksByRuleId[rule.Id].TargetRulebaseId)
                    .ToList();
                for (int index = inlineLayerIds.Count - 1; index >= 0; index--)
                {
                    pendingLayers.Push((inlineLayerIds[index], pending.Depth + 1));
                }
            }
            return true;
        }

        /// <summary>Validates and adds one layer without traversing its inline successors.</summary>
        private bool TryCreateLayer(long rulebaseId, out CheckPointLayerStructure? layer)
        {
            layer = null;
            if (!interpretedRulebaseIds.Add(rulebaseId))
            {
                TopologyFailure("rulebase_revisited", ("rulebase_id", rulebaseId));
                return false;
            }
            if (!rulebasesById.TryGetValue(rulebaseId, out RulebaseGraphNode? node))
            {
                TopologyFailure("rulebase_missing", ("rulebase_id", rulebaseId));
                return false;
            }
            if (!TryBuildSections(rulebaseId, out List<CheckPointSectionStructure> sections))
            {
                return false;
            }

            layer = new CheckPointLayerStructure(node, sections);
            layers.Add(layer);
            return true;
        }

        /// <summary>
        /// Follows the unique ordered section chain owned by one layer.
        /// </summary>
        private bool TryBuildSections(
            long layerRulebaseId,
            out List<CheckPointSectionStructure> sections)
        {
            sections = [];
            long currentRulebaseId = layerRulebaseId;
            HashSet<long> sectionIds = [];
            while (true)
            {
                RulebaseGraphLink? successor = GetUniqueSectionSuccessor(currentRulebaseId);
                if (successor == null)
                {
                    return true;
                }
                currentRulebaseId = successor.TargetRulebaseId;
                if (!sectionIds.Add(currentRulebaseId))
                {
                    TopologyFailure("section_cycle", ("layer_rulebase_id", layerRulebaseId), ("section_rulebase_id", currentRulebaseId));
                    return false;
                }
                if (!interpretedRulebaseIds.Add(currentRulebaseId))
                {
                    TopologyFailure("section_revisited", ("layer_rulebase_id", layerRulebaseId), ("section_rulebase_id", currentRulebaseId));
                    return false;
                }
                if (!rulebasesById.TryGetValue(currentRulebaseId, out RulebaseGraphNode? sectionNode))
                {
                    TopologyFailure("section_missing", ("layer_rulebase_id", layerRulebaseId), ("section_rulebase_id", currentRulebaseId));
                    return false;
                }
                sections.Add(new CheckPointSectionStructure(sectionNode));
            }
        }

        /// <summary>
        /// Returns the already-validated single ordered or domain successor.
        /// </summary>
        private RulebaseGraphLink? GetUniqueOrderedSuccessor(long sourceRulebaseId)
        {
            return snapshot.Links.SingleOrDefault(link =>
                link.SourceRulebaseId == sourceRulebaseId &&
                !link.IsSection &&
                (link.LinkType == RulebaseLinkTypes.Ordered || link.LinkType == RulebaseLinkTypes.Domain));
        }

        /// <summary>
        /// Returns the already-validated single section successor.
        /// </summary>
        private RulebaseGraphLink? GetUniqueSectionSuccessor(long sourceRulebaseId)
        {
            return snapshot.Links.SingleOrDefault(link =>
                link.SourceRulebaseId == sourceRulebaseId && link.IsSection);
        }

        /// <summary>
        /// Creates a rule-to-inline-layer lookup and marks ambiguity with an impossible sentinel entry.
        /// </summary>
        private static (Dictionary<long, RulebaseGraphLink> Links, bool IsValid) BuildInlineLinkLookup(
            IEnumerable<RulebaseGraphLink> links)
        {
            Dictionary<long, RulebaseGraphLink> inlineLinks = [];
            foreach (RulebaseGraphLink link in links.Where(link => link.LinkType == RulebaseLinkTypes.Inline))
            {
                if (!link.SourceRuleId.HasValue || !inlineLinks.TryAdd(link.SourceRuleId.Value, link))
                {
                    return (inlineLinks, false);
                }
            }
            return (inlineLinks, true);
        }

        /// <summary>
        /// Rejects topology that would require choosing one structural successor.
        /// </summary>
        private bool TryValidateTopology()
        {
            List<RulebaseGraphLink> graphLinks = snapshot.Links.ToList();
            if (!hasValidInlineLinks)
            {
                TopologyFailure("inline_lookup_invalid");
                return false;
            }
            RulebaseGraphLink? invalidSource = graphLinks.FirstOrDefault(link =>
                (link.IsSection || link.LinkType == RulebaseLinkTypes.Ordered ||
                    link.LinkType == RulebaseLinkTypes.Domain) &&
                !link.IsInitial && !link.SourceRulebaseId.HasValue);
            if (invalidSource != null)
            {
                TopologyFailure("structural_source_missing", ("target_rulebase_id", invalidSource.TargetRulebaseId));
                return false;
            }
            IGrouping<long?, RulebaseGraphLink>? ordered = graphLinks
                .Where(link => !link.IsSection && link.SourceRulebaseId.HasValue &&
                    (link.LinkType == RulebaseLinkTypes.Ordered || link.LinkType == RulebaseLinkTypes.Domain))
                .GroupBy(link => link.SourceRulebaseId)
                .FirstOrDefault(group => group.Count() > 1);
            if (ordered != null)
            {
                TopologyFailure("ordered_successor_ambiguous", ("source_rulebase_id", ordered.Key ?? 0), ("violation_count", ordered.Count()));
                return false;
            }
            IGrouping<long?, RulebaseGraphLink>? sections = graphLinks
                .Where(link => link.IsSection && link.SourceRulebaseId.HasValue)
                .GroupBy(link => link.SourceRulebaseId)
                .FirstOrDefault(group => group.Count() > 1);
            if (sections != null)
            {
                TopologyFailure("section_successor_ambiguous", ("source_rulebase_id", sections.Key ?? 0), ("violation_count", sections.Count()));
                return false;
            }
            IGrouping<long?, RulebaseGraphLink>? inline = graphLinks
                .Where(link => link.LinkType == RulebaseLinkTypes.Inline && link.SourceRuleId.HasValue)
                .GroupBy(link => link.SourceRuleId)
                .FirstOrDefault(group => group.Count() > 1);
            if (inline != null)
            {
                TopologyFailure("inline_successor_ambiguous", ("source_rule_id", inline.Key ?? 0), ("violation_count", inline.Count()));
                return false;
            }
            RulebaseGraphLink? inlineWithoutRule = graphLinks.FirstOrDefault(link =>
                link.LinkType == RulebaseLinkTypes.Inline && !link.SourceRuleId.HasValue);
            if (inlineWithoutRule != null)
            {
                TopologyFailure("inline_source_rule_missing", ("target_rulebase_id", inlineWithoutRule.TargetRulebaseId));
                return false;
            }
            return true;
        }

        /// <summary>Writes the first failed topology invariant with bounded graph totals.</summary>
        private void TopologyFailure(string invariant, params (string Key, object? Value)[] values)
        {
            Dictionary<string, object?> facts = Facts(values);
            facts["invariant"] = invariant;
            facts.TryAdd("violation_count", 1);
            facts["rulebase_count"] = snapshot.Rulebases.Count;
            facts["link_count"] = snapshot.Links.Count;
            facts["rule_count"] = snapshot.Rulebases.Sum(Rulebase => Rulebase.Rules.Count);
            RulePositioningDiagnosticLog.WriteFailure("topology.validation", facts);
        }

        /// <summary>Builds one bounded topology fact map.</summary>
        private static Dictionary<string, object?> Facts(params (string Key, object? Value)[] values) =>
            values.ToDictionary(Value => Value.Key, Value => Value.Value);
    }
}

/// <summary>
/// One selectable Check Point layer and its structural content.
/// </summary>
internal sealed class CheckPointLayerStructure
{
    /// <summary>Gets the normalized rulebase node represented by this layer.</summary>
    internal RulebaseGraphNode Node { get; }
    /// <summary>Gets direct rules and sections in effective order.</summary>
    internal IReadOnlyList<CheckPointLayerItemStructure> Items { get; }
    /// <summary>Gets sections in their structural chain order.</summary>
    internal IReadOnlyList<CheckPointSectionStructure> Sections { get; }
    /// <summary>Gets all rules flattened in effective order.</summary>
    internal IReadOnlyList<RulebaseGraphRule> OrderedRules { get; }

    /// <summary>Creates an immutable interpreted layer.</summary>
    internal CheckPointLayerStructure(
        RulebaseGraphNode node,
        IEnumerable<CheckPointSectionStructure> sections)
    {
        Node = node;
        List<CheckPointSectionStructure> copiedSections = sections.ToList();
        Sections = new ReadOnlyCollection<CheckPointSectionStructure>(copiedSections);
        List<CheckPointLayerItemStructure> items = BuildOrderedItems(node.Rules, copiedSections);
        Items = new ReadOnlyCollection<CheckPointLayerItemStructure>(items);
        OrderedRules = new ReadOnlyCollection<RulebaseGraphRule>(
            items.SelectMany(ItemRules).ToList());
    }

    /// <summary>
    /// Merges direct rules and populated sections by native order, then places empty sections by their chain.
    /// </summary>
    private static List<CheckPointLayerItemStructure> BuildOrderedItems(
        IEnumerable<RulebaseGraphRule> directRules,
        List<CheckPointSectionStructure> sections)
    {
        List<CheckPointLayerItemStructure> items = directRules
            .Select(rule => (CheckPointLayerItemStructure)new CheckPointRuleItemStructure(rule))
            .Concat(sections.Where(section => section.Rules.Count > 0))
            .OrderBy(FirstNativeOrder)
            .ThenBy(item => item is CheckPointSectionStructure ? 1 : 0)
            .ToList();

        CheckPointSectionStructure? previousSection = null;
        for (int index = 0; index < sections.Count; index++)
        {
            CheckPointSectionStructure section = sections[index];
            if (section.Rules.Count == 0)
            {
                CheckPointSectionStructure? nextPopulatedSection = sections
                    .Skip(index + 1)
                    .FirstOrDefault(candidate => candidate.Rules.Count > 0);
                InsertEmptySection(items, section, previousSection, nextPopulatedSection);
            }
            previousSection = section;
        }
        return items;
    }

    /// <summary>
    /// Inserts an empty section next to its closest section-chain neighbor.
    /// </summary>
    private static void InsertEmptySection(
        List<CheckPointLayerItemStructure> items,
        CheckPointSectionStructure section,
        CheckPointSectionStructure? previousSection,
        CheckPointSectionStructure? nextPopulatedSection)
    {
        if (nextPopulatedSection != null)
        {
            items.Insert(items.IndexOf(nextPopulatedSection), section);
        }
        else if (previousSection != null)
        {
            items.Insert(items.IndexOf(previousSection) + 1, section);
        }
        else
        {
            items.Add(section);
        }
    }

    /// <summary>
    /// Returns the first native rule order represented by one populated item.
    /// </summary>
    private static double FirstNativeOrder(CheckPointLayerItemStructure item)
    {
        return item switch
        {
            CheckPointRuleItemStructure rule => rule.Rule.NativeOrder,
            CheckPointSectionStructure section => section.Rules[0].NativeOrder,
            _ => double.MaxValue
        };
    }

    /// <summary>
    /// Flattens one layer item without splitting a section's rules.
    /// </summary>
    private static IEnumerable<RulebaseGraphRule> ItemRules(CheckPointLayerItemStructure item)
    {
        return item switch
        {
            CheckPointRuleItemStructure rule => [rule.Rule],
            CheckPointSectionStructure section => section.Rules,
            _ => []
        };
    }
}

/// <summary>
/// Base value for an ordered item inside a Check Point layer.
/// </summary>
internal abstract class CheckPointLayerItemStructure
{
    /// <summary>Prevents construction outside the structural item hierarchy.</summary>
    private protected CheckPointLayerItemStructure()
    { }
}

/// <summary>
/// One direct rule inside a Check Point layer.
/// </summary>
internal sealed class CheckPointRuleItemStructure : CheckPointLayerItemStructure
{
    /// <summary>Gets the direct structural rule.</summary>
    internal RulebaseGraphRule Rule { get; }

    /// <summary>Creates a direct-rule layer item.</summary>
    internal CheckPointRuleItemStructure(RulebaseGraphRule rule)
    {
        Rule = rule;
    }
}

/// <summary>
/// One section rulebase in its owning Check Point layer.
/// </summary>
internal sealed class CheckPointSectionStructure : CheckPointLayerItemStructure
{
    /// <summary>Gets the normalized section rulebase.</summary>
    internal RulebaseGraphNode Node { get; }
    /// <summary>Gets the rules contained in the section.</summary>
    internal IReadOnlyList<RulebaseGraphRule> Rules => Node.Rules;

    /// <summary>Creates a section layer item.</summary>
    internal CheckPointSectionStructure(RulebaseGraphNode node)
    {
        Node = node;
    }
}
