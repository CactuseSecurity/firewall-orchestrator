using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using Rule = FWO.Data.Rule;

namespace FWO.Services.RuleTreeBuilder
{
    /// <summary>
    /// Builds rule trees by traversing the rulebase-link graph that importers normalize for a
    /// device. The builder does not rely on the incoming order of <see cref="RulebaseLink"/>
    /// records. Instead, it reconstructs the visible tree exclusively from the graph semantics:
    /// exactly one initial link points at the first ordered layer, ordered/domain links chain
    /// top-level layers, section links chain rulebases that belong to the same layer or inline
    /// layer, and inline links attach nested rulebases to specific rules through
    /// <see cref="RulebaseLink.FromRuleId"/>.
    ///
    /// The tree-building pass focuses only on structural correctness. Hierarchical display
    /// numbers are intentionally not assigned while the tree is being assembled. After the full
    /// tree exists, a second pass flattens the visible nodes in display order and derives dotted
    /// order strings as well as sequential <see cref="Rule.OrderNumber"/> values. This keeps
    /// numbering independent from traversal bookkeeping and guarantees that numbers reflect the
    /// final tree shape rather than transient state.
    ///
    /// The builder is scoped as a service, so per-build state is reinitialized inside
    /// <see cref="BuildRuleTree"/>. The only state preserved across builds is the cache of
    /// completed trees and their flattened rule rows.
    /// </summary>
    public class RuleTreeBuilder : IRuleTreeBuilder
    {
        private const string LogMessageTitle = "Rule Tree Builder";
        private const int OrderedLinkType = 2;
        private const int InlineLinkType = 3;
        private const int DomainLinkType = 5;

        /// <summary>
        /// Gets or sets the root node of the most recently built rule tree. The root itself is
        /// never rendered. Its direct children are ordered-layer header nodes and it serves as
        /// the anchor for collapse/expand handling in the UI.
        /// </summary>
        public RuleTreeItem RuleTree { get; set; } = new() { IsRoot = true };

        /// <summary>
        /// Gets or sets the cache of generated rule trees keyed by management and device. Report
        /// generation uses this cache to reuse the tree for later rendering passes and collapse
        /// toggles instead of rebuilding the structure.
        /// </summary>
        public Dictionary<(int managementId, int deviceId), RuleTreeItem> RuleTreeCache { get; set; } = [];

        /// <summary>
        /// Gets or sets the flattened rule-row cache keyed by the corresponding root node. The
        /// flattened representation contains visible ordered-layer headers, section headers, and
        /// rules in final display order.
        /// </summary>
        public Dictionary<RuleTreeItem, Rule[]> FlattenedRules { get; set; } = [];

        /// <summary>
        /// Gets or sets the rulebase lookup for the build currently in progress. The lookup is
        /// recreated for every call to <see cref="BuildRuleTree"/> so that the builder can remain
        /// a reusable scoped service while still behaving like a fresh single-use traversal
        /// object.
        /// </summary>
        public Dictionary<int, RulebaseReport> RulebasesById { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of links that have not yet been consumed by the graph traversal.
        /// A link is considered consumed as soon as the traversal resolves it as the initial
        /// entry point, a next ordered/domain layer, a section successor, or an inline-layer
        /// attachment. Links left in this list after the build finishes are treated as
        /// unresolved-data warnings rather than hard failures.
        /// </summary>
        public List<RulebaseLink> LinksToBeProcessed { get; set; } = [];

        /// <summary>
        /// Gets or sets the lookup from a rule id to the single inline-layer link originating
        /// from that rule. The normalized graph model allows at most one inline layer per rule,
        /// so the lookup is intentionally one-to-one. Inline links are never discovered by array
        /// order. They are resolved only while emitting the owning rule, ensuring that the inline
        /// layer appears exactly at the rule node it belongs to.
        /// </summary>
        public Dictionary<long, RulebaseLink> InlineLinkByFromRuleId { get; set; } = [];

        /// <summary>
        /// Gets or sets the lookup from a rulebase id to structural successor links. Structural
        /// links are all non-inline links that express graph relationships between rulebases:
        /// sections/concatenations inside a layer and ordered/domain links between layers.
        /// </summary>
        public Dictionary<int, List<RulebaseLink>> StructuralLinksByFromRulebaseId { get; set; } = [];

        /// <summary>
        /// Gets or sets the set of rule ids that have already been emitted into the current tree.
        /// Duplicate real rules are considered invalid input because the rewritten builder no
        /// longer clones or silently tolerates duplicates; it fails fast so malformed graph data
        /// is surfaced immediately.
        /// </summary>
        public HashSet<long> SeenRuleIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the sequential order-number counter used during the final flattening pass
        /// The value starts at 1 for every build and is assigned to visible rows only after the
        /// tree has been completely assembled.
        /// </summary>
        public int NextOrderNumber { get; set; } = 1;

        /// <summary>
        /// Creates a reusable builder service. The constructor only initializes stable cache
        /// containers and the root placeholder. All per-build graph state is populated inside
        /// <see cref="BuildRuleTree"/> so the existing DI registration can continue to reuse the
        /// service instance across reports without retaining stale traversal state.
        /// </summary>
        public RuleTreeBuilder()
        {
        }

        /// <summary>
        /// Builds a rule tree for one management/device pair from normalized rulebases and
        /// rulebase links. The method creates a fresh per-build graph view, traverses ordered
        /// layers, sections, and inline layers by following their graph relationships, then
        /// performs a second pass that flattens the visible nodes and assigns final display
        /// numbers. The returned list contains exactly the visible report rows in display order:
        /// ordered-layer header placeholders, section-header placeholders, and real rules.
        ///
        /// The build is intentionally strict about invalid structural data. It throws when the
        /// graph lacks exactly one initial link, when a required target rulebase cannot be
        /// resolved, when an ambiguous “next layer” or “next section” relationship exists, or
        /// when the same real rule id would be emitted twice. Links that are merely unreachable
        /// from the initial graph entry point do not fail the build; they stay in
        /// <see cref="LinksToBeProcessed"/> and are reported as a warning after traversal
        /// completes.
        /// </summary>
        public List<Rule> BuildRuleTree(RulebaseReport[] rulebases, RulebaseLink[] links, int managementId, int deviceId)
        {
            InitializeBuildState(rulebases, links);

            TraverseOrderedLayers();
            List<Rule> flattenedRules = FlattenTreeAndAssignDisplayNumbers();

            RuleTreeCache[(managementId, deviceId)] = RuleTree;
            FlattenedRules[RuleTree] = [.. flattenedRules];

            if (LinksToBeProcessed.Count > 0)
            {
                Log.WriteWarning(
                    LogMessageTitle,
                    $"Finished building the rule tree with {LinksToBeProcessed.Count} unresolved rulebase link(s).");
            }

            return flattenedRules;
        }

        /// <summary>
        /// Initializes all mutable state required for exactly one build. This method replaces the
        /// former public reset workflow and deliberately recreates every traversal lookup from
        /// scratch so the builder behaves like a fresh single-use object while preserving its DI
        /// friendly public constructor.
        ///
        /// The initialization step prepares:
        /// - a rulebase lookup by id
        /// - the working list of links that still need to be consumed
        /// - an inline-link lookup by <see cref="RulebaseLink.FromRuleId"/>
        /// - a structural-link lookup by <see cref="RulebaseLink.FromRulebaseId"/>
        /// - the duplicate-rule guard set
        /// - a fresh root tree node
        /// - the final numbering counter reset to 1
        ///
        /// No traversal decisions are made here. The method merely establishes the graph view
        /// that later traversal helpers query.
        /// </summary>
        private void InitializeBuildState(RulebaseReport[] rulebases, RulebaseLink[] links)
        {
            RulebasesById = rulebases.ToDictionary(rulebase => rulebase.Id);
            LinksToBeProcessed = [.. links];
            InlineLinkByFromRuleId = BuildInlineLinkLookup(links);
            StructuralLinksByFromRulebaseId = BuildStructuralLinkLookup(links);
            SeenRuleIds = [];
            NextOrderNumber = 1;
            RuleTree = new RuleTreeItem
            {
                IsRoot = true,
                IsExpanded = true,
                IsVisible = true
            };
            RuleTree.ElementsFlat.Clear();
        }

        /// <summary>
        /// Builds the inline-link lookup for the current rulebase-link set. Each key represents a
        /// rule id and maps to the single inline-layer link that originates from that rule.
        /// Inline layers are attached while the owning rule is emitted, so indexing them up front
        /// avoids global scanning and prevents accidental dependency on link ordering.
        ///
        /// The normalized model allows at most one inline layer per rule. If multiple inline
        /// links reference the same <see cref="RulebaseLink.FromRuleId"/>, the graph is
        /// ambiguous and the build fails immediately instead of silently picking one link or
        /// processing multiple inline-layer branches beneath the same rule node.
        /// </summary>
        private static Dictionary<long, RulebaseLink> BuildInlineLinkLookup(IEnumerable<RulebaseLink> links)
        {
            Dictionary<long, RulebaseLink> inlineLinkByFromRuleId = [];

            foreach (RulebaseLink inlineLink in links.Where(link => link.LinkType == InlineLinkType && link.FromRuleId.HasValue))
            {
                long fromRuleId = inlineLink.FromRuleId!.Value;
                if (!inlineLinkByFromRuleId.TryAdd(fromRuleId, inlineLink))
                {
                    throw new InvalidOperationException(
                        $"Rule {fromRuleId} has multiple inline-layer links, but at most one inline layer per rule is allowed.");
                }
            }

            return inlineLinkByFromRuleId;
        }

        /// <summary>
        /// Builds the structural-link lookup for the current rulebase-link set. Structural links
        /// are all non-inline links that connect one rulebase to another through
        /// <see cref="RulebaseLink.FromRulebaseId"/> and are therefore suitable for layer and
        /// section traversal.
        ///
        /// The lookup does not attempt to interpret the links yet. Traversal helpers later decide
        /// whether a given successor is a section chain element or a next ordered/domain layer.
        /// </summary>
        private static Dictionary<int, List<RulebaseLink>> BuildStructuralLinkLookup(IEnumerable<RulebaseLink> links)
        {
            return links
                .Where(link => link.LinkType != InlineLinkType && link.FromRulebaseId.HasValue)
                .GroupBy(link => link.FromRulebaseId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        /// <summary>
        /// Traverses the ordered-layer chain for the current device. The traversal begins by
        /// resolving exactly one initial link, removing it from <see cref="LinksToBeProcessed"/>,
        /// and taking its target rulebase as the first layer. After each layer has been processed,
        /// the traversal resolves at most one ordered/domain successor from the original layer
        /// rulebase id and continues until no next layer exists.
        ///
        /// This method intentionally ignores the physical order of the incoming link array. The
        /// only valid ordering is the one implied by graph edges:
        /// initial -> ordered/domain successor -> ordered/domain successor -> ...
        ///
        /// Sections are not advanced here. They are considered content of the current layer and
        /// are therefore handled inside <see cref="ProcessOrderedLayer"/>.
        /// </summary>
        private void TraverseOrderedLayers()
        {
            RulebaseLink initialLink = FindInitialLink();
            RemoveLinkFromProcessingQueue(initialLink);

            int currentLayerRulebaseId = initialLink.NextRulebaseId;

            while (true)
            {
                ProcessOrderedLayer(currentLayerRulebaseId, RuleTree);

                RulebaseLink? nextLayerLink = FindNextLayerLink(currentLayerRulebaseId);
                if (nextLayerLink == null)
                {
                    break;
                }

                RemoveLinkFromProcessingQueue(nextLayerLink);
                currentLayerRulebaseId = nextLayerLink.NextRulebaseId;
            }
        }

        /// <summary>
        /// Processes one ordered layer by creating its visible header node below the supplied
        /// parent node, emitting the layer’s direct rules as children of that header, and then
        /// traversing the section chain that belongs to the layer.
        ///
        /// The ordered-layer node is the stable parent for everything that belongs “inside” the
        /// layer:
        /// - direct rules of the layer
        /// - section headers chained from the layer rulebase
        /// - rules inside those sections
        ///
        /// By passing the parent node explicitly rather than inferring it from mutable global
        /// state, the method guarantees that sections remain siblings under the current layer even
        /// when the source links arrive in random order.
        /// </summary>
        private void ProcessOrderedLayer(int layerRulebaseId, RuleTreeItem parentNode)
        {
            RulebaseReport rulebase = ResolveRulebase(layerRulebaseId);
            RuleTreeItem orderedLayerNode = CreateOrderedLayerNode(rulebase);
            AttachChild(parentNode, orderedLayerNode);

            EmitRules(rulebase, orderedLayerNode);
            TraverseSections(layerRulebaseId, orderedLayerNode);
        }

        /// <summary>
        /// Traverses the section chain that belongs to one ordered layer or inline layer. The
        /// method starts from the owning rulebase id and repeatedly resolves the single section
        /// successor whose <see cref="RulebaseLink.FromRulebaseId"/> matches the current rulebase
        /// and whose <see cref="RulebaseLink.IsSection"/> flag is true.
        ///
        /// The supplied <paramref name="parentNode"/> stays constant during the entire traversal.
        /// Every discovered section header is attached directly beneath that parent so that all
        /// sections of one layer remain siblings in the tree. The graph walk advances through
        /// section rulebase ids only to preserve chain order; it does not change the UI parent.
        ///
        /// If more than one section successor exists for the current rulebase, the method throws
        /// because the traversal would otherwise have to guess which branch reflects the intended
        /// display order.
        /// </summary>
        private void TraverseSections(int layerRulebaseId, RuleTreeItem parentNode)
        {
            int currentRulebaseId = layerRulebaseId;

            while (true)
            {
                RulebaseLink? nextSectionLink = FindNextSectionLink(currentRulebaseId);
                if (nextSectionLink == null)
                {
                    break;
                }

                RemoveLinkFromProcessingQueue(nextSectionLink);
                currentRulebaseId = nextSectionLink.NextRulebaseId;
                ProcessSection(currentRulebaseId, parentNode);
            }
        }

        /// <summary>
        /// Processes one section rulebase by creating a visible section-header node beneath the
        /// supplied parent node and emitting the section’s rules under that header.
        ///
        /// The parent node is explicit on purpose. For sections inside ordered layers it is the
        /// ordered-layer header node; for sections inside inline layers it is the inline-layer
        /// root node. This makes parent ownership obvious and avoids the former “last added item”
        /// heuristics.
        /// </summary>
        private void ProcessSection(int sectionRulebaseId, RuleTreeItem parentNode)
        {
            RulebaseReport rulebase = ResolveRulebase(sectionRulebaseId);
            RuleTreeItem sectionNode = CreateSectionNode(rulebase);
            AttachChild(parentNode, sectionNode);

            EmitRules(rulebase, sectionNode);
        }

        /// <summary>
        /// Emits the direct rules of one rulebase under the supplied parent node in native rule
        /// order. Each emitted rule becomes a child node in the tree and then acts as the anchor
        /// for any inline layers originating from that rule.
        ///
        /// Duplicate real rules are not tolerated. If a rule id has already been emitted
        /// elsewhere in the current tree, the method throws immediately instead of cloning the
        /// rule or trying to preserve legacy behavior. This keeps malformed normalized data from
        /// silently producing inconsistent trees.
        /// </summary>
        private void EmitRules(RulebaseReport rulebase, RuleTreeItem parentNode)
        {
            foreach (Rule rule in rulebase.Rules)
            {
                if (!SeenRuleIds.Add(rule.Id))
                {
                    throw new InvalidOperationException($"Rule id {rule.Id} was encountered more than once while building the rule tree.");
                }

                RuleTreeItem ruleNode = CreateRuleNode(rule);
                AttachChild(parentNode, ruleNode);

                TraverseInlineLayers(rule.Id, ruleNode);
            }
        }

        /// <summary>
        /// Resolves and emits the inline layer attached to one already-emitted rule. Inline
        /// layers are discovered exclusively through <see cref="RulebaseLink.FromRuleId"/>, so
        /// this method is called exactly at the point where the owning rule node is available.
        ///
        /// The method:
        /// - resolves the single inline link for the rule, if present
        /// - removes the link from <see cref="LinksToBeProcessed"/>
        /// - resolves the inline rulebase
        /// - creates an inline-layer root node below the owning rule node
        /// - emits the inline layer’s direct rules below that root
        /// - traverses any section chain that starts from the inline layer rulebase
        ///
        /// The inline-layer root is structural only and is not itself added to the flattened
        /// report output later. Its children inherit their hierarchical display numbers from the
        /// owning rule during the final flattening pass.
        /// </summary>
        private void TraverseInlineLayers(long ruleId, RuleTreeItem parentNode)
        {
            if (!InlineLinkByFromRuleId.TryGetValue(ruleId, out RulebaseLink? inlineLink) || !LinksToBeProcessed.Contains(inlineLink))
            {
                return;
            }

            RemoveLinkFromProcessingQueue(inlineLink);

            RulebaseReport rulebase = ResolveRulebase(inlineLink.NextRulebaseId);
            RuleTreeItem inlineLayerNode = CreateInlineLayerNode(rulebase);
            AttachChild(parentNode, inlineLayerNode);

            EmitRules(rulebase, inlineLayerNode);
            TraverseSections(rulebase.Id, inlineLayerNode);
        }

        /// <summary>
        /// Resolves the single initial link for the current device graph. The rewritten builder
        /// requires exactly one initial link because ordered-layer traversal must have a unique
        /// graph entry point. Missing or multiple initial links are treated as hard data errors.
        /// </summary>
        private RulebaseLink FindInitialLink()
        {
            List<RulebaseLink> initialLinks = [.. LinksToBeProcessed.Where(link => link.IsInitial)];

            return initialLinks.Count switch
            {
                1 => initialLinks[0],
                0 => throw new InvalidOperationException("Exactly one initial rulebase link is required, but none were found."),
                _ => throw new InvalidOperationException("Exactly one initial rulebase link is required, but multiple were found.")
            };
        }

        /// <summary>
        /// Resolves the ordered/domain successor for a top-level layer. The method inspects the
        /// structural links that originate from the supplied rulebase id, filters them to links
        /// that represent a next top-level layer, and returns the single remaining candidate.
        ///
        /// Section links are explicitly excluded because sections are handled inside the current
        /// layer. Concatenated non-section links are also excluded from next-layer traversal; the
        /// new implementation does not preserve that legacy quirk and instead leaves such links in
        /// <see cref="LinksToBeProcessed"/>, which later produces a warning.
        /// </summary>
        private RulebaseLink? FindNextLayerLink(int fromRulebaseId)
        {
            if (!StructuralLinksByFromRulebaseId.TryGetValue(fromRulebaseId, out List<RulebaseLink>? candidates))
            {
                return null;
            }

            List<RulebaseLink> nextLayerCandidates = [.. candidates
                .Where(LinksToBeProcessed.Contains)
                .Where(link => !link.IsSection) // also implies LinkType == ConcatenatedLinkType = 4
                .Where(link => link.LinkType == OrderedLinkType || link.LinkType == DomainLinkType)];

            return nextLayerCandidates.Count switch
            {
                0 => null,
                1 => nextLayerCandidates[0],
                _ => throw new InvalidOperationException(
                    $"Rulebase {fromRulebaseId} has multiple ordered/domain successors, so the next layer is ambiguous.")
            };
        }

        /// <summary>
        /// Resolves the next section successor in a section chain. The method looks at structural
        /// links whose <see cref="RulebaseLink.FromRulebaseId"/> matches the supplied rulebase id
        /// and returns the single remaining link marked with <see cref="RulebaseLink.IsSection"/>.
        ///
        /// Returning at most one successor is central to preserving section order without relying
        /// on input ordering. If multiple section successors exist, the graph no longer encodes a
        /// unique display order and the build therefore fails.
        /// </summary>
        private RulebaseLink? FindNextSectionLink(int fromRulebaseId)
        {
            if (!StructuralLinksByFromRulebaseId.TryGetValue(fromRulebaseId, out List<RulebaseLink>? candidates))
            {
                return null;
            }

            List<RulebaseLink> nextSectionCandidates = [.. candidates
                .Where(LinksToBeProcessed.Contains)
                .Where(link => link.IsSection)];

            return nextSectionCandidates.Count switch
            {
                0 => null,
                1 => nextSectionCandidates[0],
                _ => throw new InvalidOperationException(
                    $"Rulebase {fromRulebaseId} has multiple section successors, so the section chain is ambiguous.")
            };
        }

        /// <summary>
        /// Removes a consumed link from the working processing queue. Link consumption is tracked
        /// by physical removal from <see cref="LinksToBeProcessed"/>, as agreed in the plan, so
        /// unresolved leftovers can be detected simply by inspecting the queue after traversal.
        /// </summary>
        private void RemoveLinkFromProcessingQueue(RulebaseLink link)
        {
            LinksToBeProcessed.Remove(link);
        }

        /// <summary>
        /// Resolves a rulebase id through <see cref="RulebasesById"/> and throws if the target is
        /// missing. Every traversal stage relies on this helper so that missing targets fail
        /// consistently whether they are referenced by an ordered layer, a section, or an inline
        /// layer.
        /// </summary>
        private RulebaseReport ResolveRulebase(int rulebaseId)
        {
            if (!RulebasesById.TryGetValue(rulebaseId, out RulebaseReport? rulebase))
            {
                throw new InvalidOperationException($"Rulebase {rulebaseId} referenced by the rulebase-link graph could not be found.");
            }

            return rulebase;
        }

        /// <summary>
        /// Creates an ordered-layer header node and its synthetic placeholder rule. Ordered-layer
        /// headers are visible report rows, so the placeholder rule carries the layer name in
        /// <see cref="Rule.SectionHeader"/> and later receives display numbering during the
        /// flattening pass.
        /// </summary>
        private static RuleTreeItem CreateOrderedLayerNode(RulebaseReport rulebase)
        {
            return new RuleTreeItem
            {
                Header = rulebase.Name ?? string.Empty,
                Data = CreateHeaderPlaceholderRule(rulebase.Name),
                IsOrderedLayerHeader = true,
                IsExpanded = true,
                IsVisible = true
            };
        }

        /// <summary>
        /// Creates a section header node and its synthetic placeholder rule. The placeholder rule
        /// is rendered exactly like an ordered-layer header row later, but it remains a child of
        /// the layer or inline-layer node that owns the section chain.
        /// </summary>
        private static RuleTreeItem CreateSectionNode(RulebaseReport rulebase)
        {
            return new RuleTreeItem
            {
                Header = rulebase.Name ?? string.Empty,
                Data = CreateHeaderPlaceholderRule(rulebase.Name),
                IsSectionHeader = true,
                IsExpanded = true,
                IsVisible = true
            };
        }

        /// <summary>
        /// Creates an inline-layer root node. Inline-layer roots are structural grouping nodes and
        /// therefore do not receive a placeholder rule. Their header remains useful for debugging
        /// and tree serialization, but they are skipped during flattening so only the rules inside
        /// the inline layer become visible report rows.
        /// </summary>
        private static RuleTreeItem CreateInlineLayerNode(RulebaseReport rulebase)
        {
            return new RuleTreeItem
            {
                Header = rulebase.Name ?? string.Empty,
                IsInlineLayerRoot = true,
                IsExpanded = true,
                IsVisible = true
            };
        }

        /// <summary>
        /// Creates a visible rule node for a real rule record. The rule object itself is kept as
        /// the node payload; numbering and position are filled in later by the final flattening
        /// pass after the complete tree shape is known.
        /// </summary>
        private static RuleTreeItem CreateRuleNode(Rule rule)
        {
            return new RuleTreeItem
            {
                Data = rule,
                IsRule = true,
                IsExpanded = true,
                IsVisible = true
            };
        }

        /// <summary>
        /// Creates the synthetic rule object used for visible ordered-layer and section header
        /// rows. The placeholder carries the header text in <see cref="Rule.SectionHeader"/> so
        /// existing report rendering continues to treat these nodes as header rows without any UI
        /// changes.
        /// </summary>
        private static Rule CreateHeaderPlaceholderRule(string? header)
        {
            return new Rule
            {
                SectionHeader = header ?? string.Empty
            };
        }

        /// <summary>
        /// Attaches a child node to its parent and preserves the fully connected tree structure
        /// needed by UI expand/collapse behavior. This explicit parent/child wiring replaces the
        /// legacy “last added item” inference so that every caller always controls exactly where
        /// the new node is inserted.
        /// </summary>
        private static void AttachChild(RuleTreeItem parentNode, RuleTreeItem childNode)
        {
            childNode.Parent = parentNode;
            parentNode.Children.Add(childNode);
        }

        /// <summary>
        /// Flattens the finished tree into the exact row sequence used by reports and assigns
        /// dotted display numbers and sequential numeric order values in that same pass. The
        /// method clears the root’s flat-node cache and rebuilds it from the visible tree order so
        /// the cache always mirrors the completed tree.
        ///
        /// Numbering rules:
        /// - ordered-layer headers consume top-level numbers: 1, 2, 3, ...
        /// - real rules consume the next nested number within their current visible scope
        /// - section headers are visible rows but do not consume a dotted display number
        /// - rules inside a section continue numbering in the surrounding layer/inline scope
        /// - inline-layer roots are structural only and do not consume a number themselves
        /// - children beneath an inline root inherit the owning rule’s position as their base
        ///
        /// This ensures that display numbering reflects the final rendered structure rather than
        /// the order in which traversal happened to discover nodes.
        /// </summary>
        private List<Rule> FlattenTreeAndAssignDisplayNumbers()
        {
            RuleTree.ElementsFlat.Clear();
            NextOrderNumber = 1;

            List<Rule> flattenedRules = [];
            int rootVisibleChildIndex = 0;
            FlattenChildren(RuleTree.Children, [], flattenedRules, ref rootVisibleChildIndex);

            return flattenedRules;
        }

        /// <summary>
        /// Recursively flattens one ordered sequence of sibling nodes into the final report row
        /// order. The <paramref name="visibleChildIndex"/> parameter is passed by reference so
        /// numbering can continue across transparent structural nodes such as section headers and
        /// inline-layer roots.
        ///
        /// Transparent nodes behave as follows:
        /// - inline-layer roots are skipped as rows and simply forward their descendants into the
        ///   current numbering scope
        /// - section headers are emitted as visible rows but do not increment the dotted display
        ///   number; their children continue numbering in the surrounding scope
        ///
        /// Numbered nodes (ordered-layer headers and real rules) increment the current scope,
        /// receive a new dotted position, and then start a fresh nested child scope for their own
        /// descendants.
        ///
        /// The method is the central place where visible tree order turns into dotted numbering,
        /// flat-list order, and cached <see cref="RuleTree.ElementsFlat"/> entries.
        /// </summary>
        private void FlattenChildren(IEnumerable<RuleTreeItem> childNodes, List<int> parentPosition, List<Rule> flattenedRules, ref int visibleChildIndex)
        {
            foreach (RuleTreeItem childNode in childNodes)
            {
                if (childNode.IsInlineLayerRoot)
                {
                    FlattenChildren(childNode.Children, parentPosition, flattenedRules, ref visibleChildIndex);
                    continue;
                }

                if (childNode.IsSectionHeader)
                {
                    childNode.Position = [.. parentPosition];
                    AssignOrderNumber(childNode, parentPosition, assignDisplayNumber: false);
                    RuleTree.ElementsFlat.Add(childNode);
                    flattenedRules.Add(childNode.Data!);
                    FlattenChildren(childNode.Children, parentPosition, flattenedRules, ref visibleChildIndex);
                    continue;
                }

                visibleChildIndex++;
                List<int> childPosition = [.. parentPosition, visibleChildIndex];
                childNode.Position = childPosition;
                AssignOrderNumber(childNode, childPosition, assignDisplayNumber: true);
                RuleTree.ElementsFlat.Add(childNode);
                flattenedRules.Add(childNode.Data!);

                int nestedVisibleChildIndex = 0;
                FlattenChildren(childNode.Children, childPosition, flattenedRules, ref nestedVisibleChildIndex);
            }
        }

        /// <summary>
        /// Assigns display numbering information to the placeholder rule or real rule stored in a
        /// visible tree node. Ordered-layer headers and real rules receive a dotted display
        /// string, while section headers intentionally stay unnumbered in the UI even though they
        /// still receive sequential numeric order values for stable sorting and export ordering.
        /// The method then advances <see cref="NextOrderNumber"/>.
        /// </summary>
        private void AssignOrderNumber(RuleTreeItem node, List<int> position, bool assignDisplayNumber)
        {
            if (node.Data == null)
            {
                throw new InvalidOperationException("Visible rule-tree nodes must always carry a rule payload.");
            }

            node.Data.DisplayOrderNumberString = assignDisplayNumber ? string.Join(".", position) : string.Empty;
            node.Data.DisplayOrderNumber = NextOrderNumber;
            node.Data.OrderNumber = NextOrderNumber;
            NextOrderNumber++;
        }

    }
}
