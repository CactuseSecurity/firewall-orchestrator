using System.Text.Json;
using FWO.Data;

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

        /// <summary>
        /// Resolves the preferred display name for a flow network object from management-linked candidates.
        /// </summary>
        public static string ResolveNwObjectNameByRanking(FlowNwObject nwObject, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return ResolveNameByRanking(
                nwObject.Id,
                preferredManagementRanking,
                new NameResolutionContext<NetworkObject>(
                    managements,
                    management => management.Objects,
                    candidate => candidate.FlowNetworkObjectId,
                    candidate => candidate.Name,
                    candidate => candidate.FlowActive),
                fallbackName);
        }

        /// <summary>
        /// Resolves a flow network object name only when the current name is missing.
        /// </summary>
        public static string ResolveMissingNwObjectNameByRanking(FlowNwObject nwObject, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return string.IsNullOrWhiteSpace(nwObject.Name)
                ? ResolveNwObjectNameByRanking(nwObject, managements, preferredManagementRanking, fallbackName)
                : nwObject.Name!;
        }

        /// <summary>
        /// Resolves the preferred display name for a flow network group from management-linked candidates.
        /// </summary>
        public static string ResolveNwGroupNameByRanking(FlowNwGroup nwGroup, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return ResolveNameByRanking(
                nwGroup.Id,
                preferredManagementRanking,
                new NameResolutionContext<NetworkObject>(
                    managements,
                    management => management.Objects,
                    candidate => candidate.FlowNetworkGroupId,
                    candidate => candidate.Name,
                    candidate => candidate.FlowActive),
                fallbackName);
        }

        /// <summary>
        /// Resolves the preferred display name for a flow service object from management-linked candidates.
        /// </summary>
        public static string ResolveSvcObjectNameByRanking(FlowSvcObject svcObject, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return ResolveNameByRanking(
                svcObject.Id,
                preferredManagementRanking,
                new NameResolutionContext<NetworkService>(
                    managements,
                    management => management.Services,
                    candidate => candidate.FlowServiceObjectId,
                    candidate => candidate.Name,
                    candidate => candidate.FlowActive),
                fallbackName);
        }

        /// <summary>
        /// Resolves a flow service object name only when the current name is missing.
        /// </summary>
        public static string ResolveMissingSvcObjectNameByRanking(FlowSvcObject svcObject, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return string.IsNullOrWhiteSpace(svcObject.Name)
                ? ResolveSvcObjectNameByRanking(svcObject, managements, preferredManagementRanking, fallbackName)
                : svcObject.Name;
        }

        /// <summary>
        /// Resolves the preferred display name for a flow service group from management-linked candidates.
        /// </summary>
        public static string ResolveSvcGroupNameByRanking(FlowSvcGroup svcGroup, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return ResolveNameByRanking(
                svcGroup.Id,
                preferredManagementRanking,
                new NameResolutionContext<NetworkService>(
                    managements,
                    management => management.Services,
                    candidate => candidate.FlowServiceGroupId,
                    candidate => candidate.Name,
                    candidate => candidate.FlowActive),
                fallbackName);
        }

        /// <summary>
        /// Resolves the preferred display name for a flow time object from management-linked candidates.
        /// </summary>
        public static string ResolveTimeObjectNameByRanking(FlowTimeObject timeObject, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return ResolveNameByRanking(
                timeObject.Id,
                preferredManagementRanking,
                new NameResolutionContext<TimeObject>(
                    managements,
                    management => management.TimeObjects,
                    candidate => candidate.FlowTimeObjectId,
                    candidate => candidate.Name,
                    candidate => candidate.FlowActive),
                fallbackName);
        }

        /// <summary>
        /// Resolves a flow time object name only when the current name is missing.
        /// </summary>
        public static string ResolveMissingTimeObjectNameByRanking(FlowTimeObject timeObject, IEnumerable<Management>? managements, IReadOnlyList<int>? preferredManagementRanking, string fallbackName = "")
        {
            return string.IsNullOrWhiteSpace(timeObject.Name)
                ? ResolveTimeObjectNameByRanking(timeObject, managements, preferredManagementRanking, fallbackName)
                : timeObject.Name;
        }

        /// <summary>
        /// Returns the selected duplicate name when the current flow object name is missing.
        /// Existing names are preserved to avoid overwriting values from higher-ranked managements.
        /// </summary>
        public static string? ResolveMissingNameFromDuplicateSelection(string? currentName, string? selectedName)
        {
            return string.IsNullOrWhiteSpace(currentName) && !string.IsNullOrWhiteSpace(selectedName)
                ? selectedName
                : null;
        }

        /// <summary>
        /// Resolves the preferred display name for a flow object by checking managements in ranking order first,
        /// then active candidates across all managements, then any remaining candidate, and finally the fallback.
        /// </summary>
        private static string ResolveNameByRanking<TCandidate>(
            long flowObjectId,
            IReadOnlyList<int>? preferredManagementRanking,
            NameResolutionContext<TCandidate> context,
            string fallbackName = "")
        {
            List<Management> managementList = context.Managements?.ToList() ?? [];
            Dictionary<int, List<TCandidate>> candidatesByManagementId = managementList.ToDictionary(
                management => management.Id,
                management => (context.CandidateSelector(management) ?? [])
                    .Where(candidate => context.FlowObjectIdSelector(candidate) == flowObjectId)
                    .ToList());

            string? preferredName = ResolvePreferredNameByRanking(
                preferredManagementRanking,
                managementId => GetBestName(candidatesByManagementId.GetValueOrDefault(managementId), context.NameSelector, context.ActiveSelector),
                fallbackName: "");
            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                return preferredName;
            }

            string? activeName = GetBestName(
                candidatesByManagementId.Values.SelectMany(candidates => candidates),
                context.NameSelector,
                context.ActiveSelector);
            if (!string.IsNullOrWhiteSpace(activeName))
            {
                return activeName;
            }

            string? firstName = GetBestName(
                candidatesByManagementId.Values.SelectMany(candidates => candidates),
                context.NameSelector,
                _ => false,
                preferActive: false);
            return string.IsNullOrWhiteSpace(firstName) ? fallbackName : firstName;
        }

        private sealed record NameResolutionContext<TCandidate>(
            IEnumerable<Management>? Managements,
            Func<Management, IEnumerable<TCandidate>?> CandidateSelector,
            Func<TCandidate, long?> FlowObjectIdSelector,
            Func<TCandidate, string?> NameSelector,
            Func<TCandidate, bool> ActiveSelector);

        /// <summary>
        /// Returns the best available candidate name, preferring active candidates when requested.
        /// </summary>
        private static string? GetBestName<TCandidate>(
            IEnumerable<TCandidate>? candidates,
            Func<TCandidate, string?> nameSelector,
            Func<TCandidate, bool> activeSelector,
            bool preferActive = true)
        {
            List<TCandidate> candidateList = candidates?.ToList() ?? [];
            if (preferActive)
            {
                string? activeName = candidateList
                    .Where(activeSelector)
                    .Select(nameSelector)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                if (!string.IsNullOrWhiteSpace(activeName))
                {
                    return activeName;
                }
            }

            return candidateList
                .Select(nameSelector)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        }
    }
}
