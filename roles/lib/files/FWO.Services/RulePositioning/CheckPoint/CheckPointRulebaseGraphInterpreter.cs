using System.Collections.ObjectModel;
using FWO.Data;
using FWO.Data.RulePositioning;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>
/// Immutable Check Point interpretation of a normalized rulebase graph.
/// </summary>
internal sealed class CheckPointGraphStructure
{
    internal IReadOnlyList<CheckPointLayerStructure> Layers { get; }
    internal CheckPointLayerStructure InitialLayer { get; }

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

    private sealed class GraphInterpreter
    {
        private readonly RulebaseGraphSnapshot snapshot;
        private readonly Dictionary<long, RulebaseGraphNode> rulebasesById;
        private readonly Dictionary<long, RulebaseGraphLink> inlineLinksByRuleId;
        private readonly bool hasValidInlineLinks;
        private readonly List<CheckPointLayerStructure> layers = [];
        private readonly HashSet<long> interpretedRulebaseIds = [];

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
            if (!hasValidInlineLinks || !HasUnambiguousSuccessors(snapshot.Links))
            {
                return false;
            }

            List<RulebaseGraphLink> initialLinks = snapshot.Links.Where(link => link.IsInitial).ToList();
            if (initialLinks.Count != 1 || initialLinks[0].IsSection || initialLinks[0].LinkType == RulebaseLinkTypes.Inline)
            {
                return false;
            }

            long initialLayerId = initialLinks[0].TargetRulebaseId;
            if (initialLinks[0].LinkType == RulebaseLinkTypes.Policy)
            {
                RulebaseGraphLink? policySuccessor = GetUniqueOrderedSuccessor(initialLayerId);
                if (policySuccessor == null)
                {
                    return false;
                }
                initialLayerId = policySuccessor.TargetRulebaseId;
            }

            if (!TryAddLayer(initialLayerId, out initialLayer))
            {
                return false;
            }

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
                if (!orderedLayerIds.Add(currentLayerId) || !TryAddLayer(currentLayerId, out _))
                {
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
            if (!interpretedRulebaseIds.Add(rulebaseId) ||
                !rulebasesById.TryGetValue(rulebaseId, out RulebaseGraphNode? node) ||
                !TryBuildSections(rulebaseId, out List<CheckPointSectionStructure> sections))
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
                if (!sectionIds.Add(currentRulebaseId) || !interpretedRulebaseIds.Add(currentRulebaseId) ||
                    !rulebasesById.TryGetValue(currentRulebaseId, out RulebaseGraphNode? sectionNode))
                {
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
        private static bool HasUnambiguousSuccessors(IEnumerable<RulebaseGraphLink> links)
        {
            List<RulebaseGraphLink> graphLinks = links.ToList();
            bool hasInvalidStructuralSource = graphLinks.Any(link =>
                (link.IsSection || link.LinkType == RulebaseLinkTypes.Ordered || link.LinkType == RulebaseLinkTypes.Domain) &&
                !link.IsInitial && !link.SourceRulebaseId.HasValue);
            bool hasAmbiguousOrderedSuccessor = graphLinks
                .Where(link => !link.IsSection &&
                    (link.LinkType == RulebaseLinkTypes.Ordered || link.LinkType == RulebaseLinkTypes.Domain))
                .Where(link => link.SourceRulebaseId.HasValue)
                .GroupBy(link => link.SourceRulebaseId)
                .Any(group => group.Count() > 1);
            bool hasAmbiguousSectionSuccessor = graphLinks
                .Where(link => link.IsSection && link.SourceRulebaseId.HasValue)
                .GroupBy(link => link.SourceRulebaseId)
                .Any(group => group.Count() > 1);
            bool hasAmbiguousInlineLink = graphLinks
                .Where(link => link.LinkType == RulebaseLinkTypes.Inline && link.SourceRuleId.HasValue)
                .GroupBy(link => link.SourceRuleId)
                .Any(group => group.Count() > 1);
            bool hasInlineLinkWithoutRule = graphLinks.Any(link =>
                link.LinkType == RulebaseLinkTypes.Inline && !link.SourceRuleId.HasValue);
            return !hasInvalidStructuralSource && !hasAmbiguousOrderedSuccessor &&
                !hasAmbiguousSectionSuccessor && !hasAmbiguousInlineLink && !hasInlineLinkWithoutRule;
        }
    }
}

/// <summary>
/// One selectable Check Point layer and its structural content.
/// </summary>
internal sealed class CheckPointLayerStructure
{
    internal RulebaseGraphNode Node { get; }
    internal IReadOnlyList<CheckPointLayerItemStructure> Items { get; }
    internal IReadOnlyList<CheckPointSectionStructure> Sections { get; }
    internal IReadOnlyList<RulebaseGraphRule> OrderedRules { get; }

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
    private protected CheckPointLayerItemStructure()
    { }
}

/// <summary>
/// One direct rule inside a Check Point layer.
/// </summary>
internal sealed class CheckPointRuleItemStructure : CheckPointLayerItemStructure
{
    internal RulebaseGraphRule Rule { get; }

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
    internal RulebaseGraphNode Node { get; }
    internal IReadOnlyList<RulebaseGraphRule> Rules => Node.Rules;

    internal CheckPointSectionStructure(RulebaseGraphNode node)
    {
        Node = node;
    }
}
