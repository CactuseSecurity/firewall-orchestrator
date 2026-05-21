using FWO.Data;

namespace FWO.Data.Flow
{
    public static class FlowAdminHelper
    {
        /// <summary>
        /// Builds the list of unresolved duplicate flow object links.
        /// A group only qualifies if it has multiple linked objects and none of them are active.
        /// </summary>
        public static List<FlowNwObjectDuplicateGroup> BuildDuplicateGroups(IEnumerable<FlowNwObject>? flowObjects)
        {
            List<FlowNwObjectDuplicateGroup> duplicateGroups = [];

            foreach (FlowNwObject flowObject in flowObjects ?? [])
            {
                List<NetworkObject> linkedObjects = [.. (flowObject.Objects ?? [])];
                if (linkedObjects.Count <= 1 || linkedObjects.Any(nwObject => nwObject.FlowActive))
                {
                    continue;
                }

                duplicateGroups.Add(new FlowNwObjectDuplicateGroup
                {
                    FlowNwObjectId = flowObject.Id,
                    FlowNwObjectName = flowObject.Name ?? "",
                    Objects = [.. linkedObjects
                        .OrderBy(nwObject => nwObject.Name ?? "", StringComparer.OrdinalIgnoreCase)
                        .ThenBy(nwObject => nwObject.Id)]
                });
            }

            return [.. duplicateGroups
                .OrderBy(group => group.FlowNwObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.FlowNwObjectId)
                .ThenBy(group => group.Objects.Count)];
        }

        /// <summary>
        /// Builds a searchable text blob for a custom flow object candidate.
        /// </summary>
        public static string BuildCustomObjectSearchText(NetworkObject candidate)
        {
            return string.Join(' ', [
                candidate.Id.ToString(),
                candidate.Name ?? "",
                candidate.IP ?? "",
                candidate.IpEnd ?? "",
                candidate.Uid ?? "",
                candidate.Active ? "active" : "inactive",
                candidate.Type?.Id.ToString() ?? "",
                candidate.Type?.Name ?? ""
            ]).ToLowerInvariant();
        }

        /// <summary>
        /// Filters custom flow object candidates by a case-insensitive search string.
        /// </summary>
        public static List<NetworkObject> FilterCustomObjectCandidates(IEnumerable<NetworkObject>? candidates, string? searchText)
        {
            IEnumerable<NetworkObject> filteredCandidates = candidates ?? [];

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string normalizedSearchText = searchText.Trim();
                filteredCandidates = filteredCandidates.Where(candidate =>
                    BuildCustomObjectSearchText(candidate).Contains(normalizedSearchText, StringComparison.OrdinalIgnoreCase));
            }

            return [.. filteredCandidates
                .OrderBy(candidate => candidate.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Id)];
        }

        /// <summary>
        /// Builds a searchable text blob for a custom flow service object candidate.
        /// </summary>
        public static string BuildCustomServiceSearchText(NetworkService candidate)
        {
            return string.Join(' ', [
                candidate.Id.ToString(),
                candidate.Name ?? "",
                candidate.Uid ?? "",
                candidate.DestinationPort?.ToString() ?? "",
                candidate.DestinationPortEnd?.ToString() ?? "",
                candidate.SourcePort?.ToString() ?? "",
                candidate.SourcePortEnd?.ToString() ?? "",
                candidate.Code ?? "",
                candidate.Protocol?.Name ?? "",
                candidate.Type?.Name ?? "",
                candidate.Active ? "active" : "inactive",
                candidate.FlowActive ? "flow-active" : "flow-inactive"
            ]).ToLowerInvariant();
        }

        /// <summary>
        /// Filters custom flow service object candidates by a case-insensitive search string.
        /// </summary>
        public static List<NetworkService> FilterCustomServiceCandidates(IEnumerable<NetworkService>? candidates, string? searchText)
        {
            IEnumerable<NetworkService> filteredCandidates = candidates ?? [];

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string normalizedSearchText = searchText.Trim();
                filteredCandidates = filteredCandidates.Where(candidate =>
                    BuildCustomServiceSearchText(candidate).Contains(normalizedSearchText, StringComparison.OrdinalIgnoreCase));
            }

            return [.. filteredCandidates
                .OrderBy(candidate => candidate.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Id)];
        }
    }

    public class FlowNwObjectDuplicateGroup
    {
        public long FlowNwObjectId { get; set; }
        public string FlowNwObjectName { get; set; } = "";
        public int ManagementId { get; set; }
        public string ManagementName { get; set; } = "";
        public List<NetworkObject> Objects { get; set; } = [];
    }
}
