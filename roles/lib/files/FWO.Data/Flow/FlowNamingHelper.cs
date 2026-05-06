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
        /// Active mappings are preferred for the rewrite pass; if none are usable, any usable mapping is considered.
        /// </summary>
        public static string ResolveNwObjectName(FlowNwObject nwObject, int? preferredManagementId, string fallbackName = "")
        {
            List<FlowNwObjectMapping> activeMappings = [.. (nwObject.NwObjectMappings ?? []).Where(mapping => mapping.ActiveOnMgm)];
            string resolvedName = ResolvePreferredName(
                activeMappings,
                preferredManagementId,
                mapping => mapping.MgmId,
                mapping => mapping.Object?.Name,
                fallbackName: "");

            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                return resolvedName;
            }

            resolvedName = ResolvePreferredName(
                nwObject.NwObjectMappings,
                preferredManagementId,
                mapping => mapping.MgmId,
                mapping => mapping.Object?.Name,
                fallbackName: "");

            return string.IsNullOrWhiteSpace(resolvedName) ? fallbackName : resolvedName;
        }
    }
}
