namespace FWO.Data.Flow
{
    public static class FlowNamingHelper
    {
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
