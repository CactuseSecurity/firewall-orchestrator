using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Flow;

namespace FWO.Services.FlowSync
{
    /// <summary>
    /// Applies management naming precedence to flow objects after their normalized mappings exist.
    /// </summary>
    public class FlowNaming(ApiConnection apiConnection)
    {
        /// <summary>
        /// Names all supported flow objects from their mapped normalized objects.
        /// </summary>
        public async Task ApplyNamesAsync(IReadOnlyList<int>? managementRanking, bool overwrite = false)
        {
            await ApplyNamesAsync(FlowQueries.getFlowNwObjectNamingCandidates, FlowMutations.updateFlowNwObjects, managementRanking, overwrite,
                (id, name) => new { where = new { nwobj_id = new { _eq = id } }, _set = new { name } });
            await ApplyNamesAsync(FlowQueries.getFlowNwGroupNamingCandidates, FlowMutations.updateFlowNwGroups, managementRanking, overwrite,
                (id, name) => new { where = new { nwgrp_id = new { _eq = id } }, _set = new { name } });
            await ApplyNamesAsync(FlowQueries.getFlowSvcObjectNamingCandidates, FlowMutations.updateFlowSvcObjects, managementRanking, overwrite,
                (id, name) => new { where = new { svcobj_id = new { _eq = id } }, _set = new { name } });
            await ApplyNamesAsync(FlowQueries.getFlowSvcGroupNamingCandidates, FlowMutations.updateFlowSvcGroups, managementRanking, overwrite,
                (id, name) => new { where = new { svcgrp_id = new { _eq = id } }, _set = new { name } });
            await ApplyNamesAsync(FlowQueries.getFlowTimeObjectNamingCandidates, FlowMutations.updateFlowTimeObjects, managementRanking, overwrite,
                (id, name) => new { where = new { timeobj_id = new { _eq = id } }, _set = new { name } });
        }

        private async Task ApplyNamesAsync(string query, string mutation, IReadOnlyList<int>? managementRanking, bool overwrite, Func<long, string, object> createUpdate)
        {
            List<FlowNamingCandidate> candidates = await apiConnection.SendQueryAsync<List<FlowNamingCandidate>>(query) ?? [];
            List<object> updates = [];

            foreach (FlowNamingCandidate candidate in candidates.Where(candidate => overwrite || candidate.Name == null))
            {
                string? name = ResolveName(candidate.Mappings, managementRanking);
                if (!string.IsNullOrWhiteSpace(name) && (overwrite || !string.Equals(candidate.Name, name, StringComparison.Ordinal)))
                {
                    updates.Add(createUpdate(candidate.Id, name));
                }
            }

            if (updates.Count > 0)
            {
                await apiConnection.SendQueryAsync<List<MutationResult>>(mutation, new { updates });
            }
        }

        private static string? ResolveName(IEnumerable<FlowNamingMapping> mappings, IReadOnlyList<int>? managementRanking)
        {
            List<FlowNamingMapping> mappingList = mappings.Where(mapping => !string.IsNullOrWhiteSpace(mapping.Name)).ToList();
            Dictionary<int, int> rankingPositions = FlowNamingHelper.NormalizeManagementRanking(
                managementRanking,
                mappingList.Select(mapping => mapping.ManagementId))
                .Select((managementId, index) => new { managementId, index })
                .ToDictionary(item => item.managementId, item => item.index);

            return mappingList
                .OrderBy(mapping => rankingPositions.GetValueOrDefault(mapping.ManagementId, int.MaxValue))
                .Select(mapping => mapping.Name)
                .FirstOrDefault();
        }
    }
}
