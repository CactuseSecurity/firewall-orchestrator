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
        /// The remaining rulebases are ordered chain by chain, each start rulebase first.
        /// The incoming links are only needed for scoping and are removed afterwards (not part of exports).
        /// </summary>
        internal static void ScopeToSelectedRulebases(ManagementReport managementReport, List<int> startRulebaseIds)
        {
            Dictionary<int, RulebaseReport> rulebasesById = managementReport.Rulebases.ToDictionary(rulebase => rulebase.Id);
            managementReport.Rulebases = [.. GetRulebaseIdsReachableFrom(managementReport, startRulebaseIds).Select(rulebaseId => rulebasesById[rulebaseId])];
            foreach (RulebaseReport rulebase in managementReport.Rulebases)
            {
                rulebase.IncomingLinks = null;
            }
        }

        /// <summary>
        /// Follows the active non-NAT rulebase links of a management, starting at each given rulebase in turn.
        /// Chains are followed per gateway leading into the start rulebase, using only the links of that gateway,
        /// so a rulebase shared by several policies does not pull in the rulebases following it in the other policies.
        /// Without any gateway leading into the start rulebase (e.g. rulebases without gateway), all links are followed.
        /// </summary>
        /// <returns>ids of the start rulebases of this management and all rulebases reachable from them, without duplicates</returns>
        internal static List<int> GetRulebaseIdsReachableFrom(ManagementReport managementReport, List<int> startRulebaseIds)
        {
            Dictionary<int, RulebaseReport> rulebasesById = managementReport.Rulebases.ToDictionary(rulebase => rulebase.Id);
            List<RulebaseEdge> edges = GetRulebaseEdges(managementReport, rulebasesById);
            List<int> orderedIds = [];
            foreach (int startRulebaseId in startRulebaseIds.Where(rulebasesById.ContainsKey))
            {
                List<int> gatewayIds = GetGatewaysLeadingInto(rulebasesById[startRulebaseId]);
                if (gatewayIds.Count == 0)
                {
                    AddReachableRulebaseIds(startRulebaseId, edges, orderedIds);
                }
                foreach (int gatewayId in gatewayIds)
                {
                    List<RulebaseEdge> gatewayEdges = [.. edges.Where(edge => edge.GatewayId == gatewayId)];
                    AddReachableRulebaseIds(startRulebaseId, gatewayEdges, orderedIds);
                }
            }
            return orderedIds;
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
