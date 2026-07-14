using System.Text.Json;

namespace FWO.Data.Flow
{
    public static class FlowNamingHelper
    {
        /// <summary>
        /// Parses the configured ranking of management ids.
        /// Invalid or empty values are treated as an empty ranking.
        /// </summary>
        public static List<int> ParseManagementRanking(string? serializedRanking)
        {
            if (string.IsNullOrWhiteSpace(serializedRanking))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<int>>(serializedRanking)?
                    .Where(managementId => managementId > 0)
                    .Distinct()
                    .ToList() ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// Normalizes the configured ranking by removing invalid ids and appending the remaining available managements.
        /// </summary>
        public static List<int> NormalizeManagementRanking(IEnumerable<int>? preferredManagementRanking, IEnumerable<int> availableManagementIds)
        {
            List<int> availableManagementIdsList = [.. availableManagementIds.Where(managementId => managementId > 0)];
            HashSet<int> availableManagementIdSet = [.. availableManagementIdsList];
            List<int> normalizedRanking = [];
            HashSet<int> seenManagementIds = [];

            foreach (int managementId in preferredManagementRanking ?? [])
            {
                if (availableManagementIdSet.Contains(managementId) && seenManagementIds.Add(managementId))
                {
                    normalizedRanking.Add(managementId);
                }
            }

            foreach (int managementId in availableManagementIdsList)
            {
                if (seenManagementIds.Add(managementId))
                {
                    normalizedRanking.Add(managementId);
                }
            }

            return normalizedRanking;
        }

        /// <summary>
        /// Serializes a ranking of management ids.
        /// </summary>
        public static string SerializeManagementRanking(IEnumerable<int>? managementRanking)
        {
            return JsonSerializer.Serialize((managementRanking ?? []).Where(managementId => managementId > 0).Distinct().ToList());
        }

        /// <summary>
        /// Resolves the best available name for a flow object from a ranked list of management ids.
        /// The first management with a usable name wins.
        /// </summary>
        public static string ResolvePreferredNameByRanking(IReadOnlyList<int>? preferredManagementRanking, Func<int, string?> nameSelector, string fallbackName = "")
        {
            if (preferredManagementRanking != null)
            {
                foreach (int managementId in preferredManagementRanking)
                {
                    string? preferredName = nameSelector(managementId);
                    if (!string.IsNullOrWhiteSpace(preferredName))
                    {
                        return preferredName;
                    }
                }
            }

            return fallbackName;
        }

        /// <summary>
        /// Resolves the best available name for a flow object.
        /// The preferred management wins when it has a usable name.
        /// Otherwise the first usable name is returned, which keeps the rule easy to replace later.
        /// </summary>
        public static string ResolvePreferredName<T>(
            IEnumerable<T>? candidates,
            int? preferredManagementId,
            Func<T, int?> managementIdSelector,
            Func<T, string?> nameSelector,
            string fallbackName = "")
        {
            List<T> candidateList = candidates?.ToList() ?? [];
            if (candidateList.Count == 0)
            {
                return fallbackName;
            }

            if (preferredManagementId.HasValue)
            {
                string? preferredName = candidateList
                    .Where(candidate => managementIdSelector(candidate) == preferredManagementId.Value)
                    .Select(nameSelector)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

                if (!string.IsNullOrWhiteSpace(preferredName))
                {
                    return preferredName;
                }
            }

            return candidateList
                .Select(nameSelector)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? fallbackName;
        }

        /// <summary>
        /// Resolves the preferred display name for a flow network object.
        /// Active linked objects are preferred for the rewrite pass; if none are usable, any usable link is considered.
        /// </summary>
        public static string ResolveNwObjectName(FlowNwObject nwObject, int? preferredManagementId, string fallbackName = "")
        {
            List<NetworkObject> activeObjects = [.. (nwObject.Objects ?? []).Where(nwObject => nwObject.FlowActive)];
            string resolvedName = ResolvePreferredName(
                activeObjects,
                preferredManagementId,
                _ => null,
                nwObject => nwObject.Name,
                fallbackName: "");

            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                return resolvedName;
            }

            resolvedName = ResolvePreferredName(
                nwObject.Objects,
                preferredManagementId,
                _ => null,
                nwObject => nwObject.Name,
                fallbackName: "");

            return string.IsNullOrWhiteSpace(resolvedName) ? fallbackName : resolvedName;
        }

        /// <summary>
        /// Resolves a name only when the current flow object name is missing.
        /// Existing non-empty names are left untouched so the save action only backfills gaps.
        /// </summary>
        public static string ResolveMissingNwObjectName(FlowNwObject nwObject, int? preferredManagementId, string fallbackName = "")
        {
            if (!string.IsNullOrWhiteSpace(nwObject.Name))
            {
                return nwObject.Name!;
            }

            return ResolveNwObjectName(nwObject, preferredManagementId, fallbackName);
        }
    }
}
