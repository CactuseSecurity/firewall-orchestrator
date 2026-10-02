using FWO.Data;
using FWO.Data.Report;

namespace FWO.Report
{
    /// <summary>
    /// Scopes a Rules report in management rulebases view to the selected start rulebases and the rulebase chains following them.
    /// </summary>
    internal static class RulebaseChainScope
    {
        /// <summary>
        /// Restricts the management report to the selected start rulebases and the rulebases linked from them.
        /// The remaining rulebases are ordered chain by chain, each start rulebase first. A rulebase contained in the chains
        /// of several start rulebases is kept once; its later appearances are recorded as repeated occurrences for display.
        /// The incoming links are only needed for scoping and are removed afterwards (not part of exports).
        /// </summary>
        internal static void ScopeToSelectedRulebases(ManagementReport managementReport, List<int> startRulebaseIds)
        {
            List<RulebaseOccurrence> occurrences = GetRulebaseOccurrences(managementReport, startRulebaseIds);
            managementReport.Rulebases = [.. occurrences.Where(occurrence => !occurrence.IsRepeated).Select(occurrence => occurrence.Rulebase)];
            managementReport.RulebaseOccurrences = occurrences;
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
            return [.. GetRulebaseOccurrences(managementReport, startRulebaseIds)
                .Where(occurrence => !occurrence.IsRepeated)
                .Select(occurrence => occurrence.Rulebase.Id)];
        }

        /// <summary>
        /// Lists the chain of each start rulebase in turn. A rulebase already listed in the chain of an earlier
        /// start rulebase is marked as repeated.
        /// </summary>
        internal static List<RulebaseOccurrence> GetRulebaseOccurrences(ManagementReport managementReport, List<int> startRulebaseIds)
        {
            Dictionary<int, RulebaseReport> rulebasesById = managementReport.Rulebases.ToDictionary(rulebase => rulebase.Id);
            List<RulebaseEdge> edges = GetRulebaseEdges(managementReport, rulebasesById);
            HashSet<int> shownRulebaseIds = [];
            List<RulebaseOccurrence> occurrences = [];
            foreach (int startRulebaseId in startRulebaseIds.Distinct().Where(rulebasesById.ContainsKey))
            {
                foreach (int rulebaseId in GetChain(startRulebaseId, rulebasesById, edges))
                {
                    occurrences.Add(new RulebaseOccurrence
                    {
                        Rulebase = rulebasesById[rulebaseId],
                        StartRulebaseId = startRulebaseId,
                        IsRepeated = !shownRulebaseIds.Add(rulebaseId)
                    });
                }
            }
            return occurrences;
        }

        /// <summary>
        /// Follows the active non-NAT rulebase links starting at the given rulebase, without duplicates.
        /// Chains are followed per gateway leading into the start rulebase, using only the links of that gateway,
        /// so a rulebase shared by several policies does not pull in the rulebases following it in the other policies.
        /// Without any gateway leading into the start rulebase (e.g. rulebases without gateway), all links are followed.
        /// </summary>
        private static List<int> GetChain(int startRulebaseId, Dictionary<int, RulebaseReport> rulebasesById, List<RulebaseEdge> edges)
        {
            List<int> chainIds = [];
            List<int> gatewayIds = GetGatewaysLeadingInto(rulebasesById[startRulebaseId]);
            if (gatewayIds.Count == 0)
            {
                AddReachableRulebaseIds(startRulebaseId, edges, chainIds);
            }
            foreach (int gatewayId in gatewayIds)
            {
                List<RulebaseEdge> gatewayEdges = [.. edges.Where(edge => edge.GatewayId == gatewayId)];
                AddReachableRulebaseIds(startRulebaseId, gatewayEdges, chainIds);
            }
            return chainIds;
        }

        private readonly record struct RulebaseEdge(int SourceRulebaseId, int TargetRulebaseId, int GatewayId);

        /// <summary>
        /// Appends the start rulebase and all rulebases reachable via the given edges (breadth first) to the ordered ids.
        /// </summary>
        private static void AddReachableRulebaseIds(int startRulebaseId, List<RulebaseEdge> edges, List<int> orderedIds)
        {
            ILookup<int, int> successorsById = edges.ToLookup(edge => edge.SourceRulebaseId, edge => edge.TargetRulebaseId);
            HashSet<int> visitedIds = [];
            Queue<int> pendingIds = new();
            pendingIds.Enqueue(startRulebaseId);
            while (pendingIds.TryDequeue(out int rulebaseId))
            {
                if (!visitedIds.Add(rulebaseId))
                {
                    continue;
                }
                if (!orderedIds.Contains(rulebaseId))
                {
                    orderedIds.Add(rulebaseId);
                }
                foreach (int successorId in successorsById[rulebaseId])
                {
                    pendingIds.Enqueue(successorId);
                }
            }
        }

        /// <summary>
        /// Returns the gateways having a (non-NAT) link into the rulebase, e.g. the initial link of their policy.
        /// </summary>
        private static List<int> GetGatewaysLeadingInto(RulebaseReport rulebase)
        {
            return [.. GetNonNatIncomingLinks(rulebase).Select(link => link.GatewayId).Where(gatewayId => gatewayId > 0).Distinct().Order()];
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
                        edges.Add(new RulebaseEdge(sourceRulebaseId.Value, rulebase.Id, link.GatewayId));
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
