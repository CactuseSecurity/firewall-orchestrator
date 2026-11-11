using FWO.Data;
using FWO.Data.Report;

namespace FWO.Report
{
    /// <summary>
    /// Scopes a Rules report in management rulebases view to the selected start rulebases and the rulebase chains following them,
    /// and describes each chain as a rule tree that is built like the rule tree of a gateway.
    /// </summary>
    internal static class RulebaseChainScope
    {
        private readonly record struct RulebaseEdge(int SourceRulebaseId, int TargetRulebaseId, RulebaseLink Link);

        private sealed record ChainVariant(string Signature, int InitialLinkType, List<int> GatewayIds, List<RulebaseEdge> Edges, List<int> RulebaseIds);

        private sealed record RuleTrees(List<DeviceReport> Trees, List<int> ScopedRulebaseIds);

        /// <summary>
        /// Restricts the management report to the selected start rulebases and the rulebases linked from them, and replaces
        /// its devices by one rule tree per start rulebase. Gateways sharing a start rulebase with identical chains share one tree;
        /// differing chains get one tree each. A rulebase listed completely in an earlier tree is only referenced in later trees.
        /// The incoming links are only needed for scoping and are removed afterwards (not part of exports).
        /// </summary>
        internal static void ScopeToSelectedRulebases(ManagementReport managementReport, List<int> startRulebaseIds)
        {
            Dictionary<int, RulebaseReport> rulebasesById = managementReport.Rulebases.ToDictionary(rulebase => rulebase.Id);
            RuleTrees ruleTrees = BuildRuleTrees(managementReport, startRulebaseIds);
            managementReport.Rulebases = [.. ruleTrees.ScopedRulebaseIds.Select(rulebaseId => rulebasesById[rulebaseId])];
            managementReport.Devices = [.. ruleTrees.Trees];
            foreach (RulebaseReport rulebase in managementReport.Rulebases)
            {
                rulebase.IncomingLinks = null;
            }
        }

        /// <summary>
        /// Returns the ids of the start rulebases of this management and all rulebases reachable from them, without duplicates.
        /// </summary>
        internal static List<int> GetRulebaseIdsReachableFrom(ManagementReport managementReport, List<int> startRulebaseIds)
        {
            return BuildRuleTrees(managementReport, startRulebaseIds).ScopedRulebaseIds;
        }

        /// <summary>
        /// Builds the rule trees of the selected start rulebases in selection order. Tree ids are negative to never collide with gateway ids.
        /// </summary>
        private static RuleTrees BuildRuleTrees(ManagementReport managementReport, List<int> startRulebaseIds)
        {
            Dictionary<int, RulebaseReport> rulebasesById = managementReport.Rulebases.ToDictionary(rulebase => rulebase.Id);
            Dictionary<int, string> gatewayNames = managementReport.Devices.GroupBy(device => device.Id).ToDictionary(group => group.Key, group => group.First().Name ?? "");
            List<RulebaseEdge> edges = GetRulebaseEdges(managementReport, rulebasesById);
            Dictionary<int, int> listingTreeIds = [];
            RuleTrees ruleTrees = new([], []);
            foreach (int startRulebaseId in startRulebaseIds.Distinct().Where(rulebasesById.ContainsKey))
            {
                RulebaseReport startRulebase = rulebasesById[startRulebaseId];
                List<ChainVariant> variants = GetChainVariants(startRulebase, edges);
                foreach (ChainVariant variant in variants)
                {
                    string treeName = variants.Count > 1
                        ? $"{startRulebase.Name} ({string.Join(", ", variant.GatewayIds.Select(gatewayId => gatewayNames.GetValueOrDefault(gatewayId, gatewayId.ToString())))})"
                        : startRulebase.Name ?? "";
                    ruleTrees.Trees.Add(CreateRuleTree(-(ruleTrees.Trees.Count + 1), treeName, startRulebaseId, variant, listingTreeIds, ruleTrees.ScopedRulebaseIds));
                }
            }
            return ruleTrees;
        }

        /// <summary>
        /// Creates the rule tree of one chain: a synthetic initial link into the start rulebase plus the links of the chain.
        /// Rulebases already listed in an earlier tree become references; of their links only the next-layer link is kept,
        /// so the tree continues with the following layer.
        /// </summary>
        private static DeviceReport CreateRuleTree(int treeId, string treeName, int startRulebaseId, ChainVariant variant,
            Dictionary<int, int> listingTreeIds, List<int> scopedRulebaseIds)
        {
            Dictionary<int, int> references = variant.RulebaseIds.Where(listingTreeIds.ContainsKey).ToDictionary(rulebaseId => rulebaseId, rulebaseId => listingTreeIds[rulebaseId]);
            foreach (int rulebaseId in variant.RulebaseIds.Where(rulebaseId => !references.ContainsKey(rulebaseId)))
            {
                listingTreeIds[rulebaseId] = treeId;
                scopedRulebaseIds.Add(rulebaseId);
            }
            RulebaseLink initialLink = new()
            {
                GatewayId = treeId,
                NextRulebaseId = startRulebaseId,
                LinkType = variant.InitialLinkType,
                IsInitial = true
            };
            List<RulebaseLink> links = [initialLink, .. variant.Edges
                .Where(edge => !references.ContainsKey(edge.SourceRulebaseId) || IsNextLayerLink(edge.Link))
                .Select(edge => CopyLinkForTree(edge, treeId))];
            return new DeviceReport { Id = treeId, Name = treeName, RulebaseLinks = [.. links], ReferencedRulebaseTreeIds = references };
        }

        /// <summary>
        /// Checks whether a link leads from a layer to the next ordered or domain layer (as opposed to sections and inline layers).
        /// </summary>
        private static bool IsNextLayerLink(RulebaseLink link)
        {
            return link.FromRuleId == null && !link.IsSection && (link.LinkType == RulebaseLinkTypes.Ordered || link.LinkType == RulebaseLinkTypes.Domain);
        }

        /// <summary>
        /// Copies a chain link for a rule tree, assigning it to the tree instead of the gateway it was imported for.
        /// </summary>
        private static RulebaseLink CopyLinkForTree(RulebaseEdge edge, int treeId)
        {
            return new RulebaseLink
            {
                GatewayId = treeId,
                FromRulebaseId = edge.Link.FromRulebaseId,
                FromRuleId = edge.Link.FromRuleId,
                NextRulebaseId = edge.TargetRulebaseId,
                LinkType = edge.Link.LinkType,
                IsGlobal = edge.Link.IsGlobal,
                IsSection = edge.Link.IsSection
            };
        }

        /// <summary>
        /// Follows the chain of each gateway leading into the start rulebase, using only the links of that gateway, and groups
        /// gateways with identical chains. Without any gateway leading into it (e.g. a rulebase without gateway) the chain is the
        /// start rulebase alone.
        /// </summary>
        private static List<ChainVariant> GetChainVariants(RulebaseReport startRulebase, List<RulebaseEdge> edges)
        {
            List<ChainVariant> variants = [];
            foreach (int gatewayId in GetGatewaysLeadingInto(startRulebase))
            {
                List<RulebaseEdge> gatewayEdges = [.. edges.Where(edge => edge.Link.GatewayId == gatewayId)];
                (List<int> rulebaseIds, List<RulebaseEdge> chainEdges) = FollowChain(startRulebase.Id, gatewayEdges);
                int initialLinkType = GetNonNatIncomingLinks(startRulebase).FirstOrDefault(link => link.GatewayId == gatewayId && link.IsInitial)?.LinkType
                    ?? RulebaseLinkTypes.Ordered;
                string signature = $"{initialLinkType}|{string.Join(";", chainEdges.Select(GetEdgeSignature).Order())}";
                ChainVariant? variant = variants.FirstOrDefault(existing => existing.Signature == signature);
                if (variant == null)
                {
                    variants.Add(new ChainVariant(signature, initialLinkType, [gatewayId], chainEdges, rulebaseIds));
                }
                else
                {
                    variant.GatewayIds.Add(gatewayId);
                }
            }
            if (variants.Count == 0)
            {
                List<int> noGatewayIds = [];
                List<RulebaseEdge> noEdges = [];
                List<int> startRulebaseOnly = [startRulebase.Id];
                variants.Add(new ChainVariant("", RulebaseLinkTypes.Ordered, noGatewayIds, noEdges, startRulebaseOnly));
            }
            return variants;
        }

        /// <summary>
        /// Describes a chain link independent of the gateway it was imported for, to detect gateways with identical chains.
        /// </summary>
        private static string GetEdgeSignature(RulebaseEdge edge)
        {
            return $"{edge.SourceRulebaseId}/{edge.Link.FromRuleId}/{edge.TargetRulebaseId}/{edge.Link.LinkType}/{edge.Link.IsSection}";
        }

        /// <summary>
        /// Collects the rulebases reachable from the start rulebase via the given edges (breadth first) and the edges leaving them.
        /// </summary>
        private static (List<int> RulebaseIds, List<RulebaseEdge> ChainEdges) FollowChain(int startRulebaseId, List<RulebaseEdge> edges)
        {
            ILookup<int, RulebaseEdge> edgesBySource = edges.ToLookup(edge => edge.SourceRulebaseId);
            List<int> rulebaseIds = [];
            List<RulebaseEdge> chainEdges = [];
            HashSet<int> visitedIds = [];
            Queue<int> pendingIds = new();
            pendingIds.Enqueue(startRulebaseId);
            while (pendingIds.TryDequeue(out int rulebaseId))
            {
                if (!visitedIds.Add(rulebaseId))
                {
                    continue;
                }
                rulebaseIds.Add(rulebaseId);
                foreach (RulebaseEdge edge in edgesBySource[rulebaseId])
                {
                    chainEdges.Add(edge);
                    pendingIds.Enqueue(edge.TargetRulebaseId);
                }
            }
            return (rulebaseIds, chainEdges);
        }

        /// <summary>
        /// Returns the gateways having a (non-NAT) link into the rulebase, e.g. the initial link of their policy.
        /// </summary>
        private static List<int> GetGatewaysLeadingInto(RulebaseReport rulebase)
        {
            return [.. GetNonNatIncomingLinks(rulebase).Select(link => link.GatewayId).Distinct().Order()];
        }

        /// <summary>
        /// Builds the links between rulebases of the management from the incoming rulebase and rule links (NAT links excluded).
        /// </summary>
        private static List<RulebaseEdge> GetRulebaseEdges(ManagementReport managementReport, Dictionary<int, RulebaseReport> rulebasesById)
        {
            List<RulebaseEdge> edges = [];
            foreach (RulebaseReport rulebase in managementReport.Rulebases)
            {
                foreach (RulebaseLink link in GetNonNatIncomingLinks(rulebase))
                {
                    int? sourceRulebaseId = link.FromRulebaseId ?? link.FromRule?.RulebaseId;
                    if (sourceRulebaseId != null && rulebasesById.ContainsKey(sourceRulebaseId.Value))
                    {
                        edges.Add(new RulebaseEdge(sourceRulebaseId.Value, rulebase.Id, link));
                    }
                }
            }
            return edges;
        }

        /// <summary>
        /// Returns the incoming links of the rulebase without NAT links (NAT rulebases are not part of the Rules report).
        /// </summary>
        private static IEnumerable<RulebaseLink> GetNonNatIncomingLinks(RulebaseReport rulebase)
        {
            return (rulebase.IncomingLinks ?? []).Where(link => link.LinkType != RulebaseLinkTypes.Nat);
        }
    }
}
