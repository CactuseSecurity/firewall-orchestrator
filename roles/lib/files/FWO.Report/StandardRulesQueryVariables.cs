using FWO.Basics;

namespace FWO.Report
{
    /// <summary>
    /// Query variable helpers for the split standard Rules report queries.
    /// </summary>
    internal static class StandardRulesQueryVariables
    {
        /// <summary>
        /// Builds the minimal variable set accepted by the standard Rules structure query.
        /// </summary>
        internal static Dictionary<string, object> BuildStructureQueryVariables(string structureQuery, Dictionary<string, object> queryVariables)
        {
            Dictionary<string, object> structureQueryVariables = new()
            {
                [QueryVar.MgmId] = queryVariables[QueryVar.MgmId],
            };

            if (structureQuery.Contains($"${QueryVar.ImportIdStart}", StringComparison.Ordinal) && queryVariables.TryGetValue(QueryVar.ImportIdStart, out object? importIdStart))
            {
                structureQueryVariables[QueryVar.ImportIdStart] = importIdStart;
            }
            if (structureQuery.Contains($"${QueryVar.ImportIdEnd}", StringComparison.Ordinal) && queryVariables.TryGetValue(QueryVar.ImportIdEnd, out object? importIdEnd))
            {
                structureQueryVariables[QueryVar.ImportIdEnd] = importIdEnd;
            }

            return structureQueryVariables;
        }
    }
}
