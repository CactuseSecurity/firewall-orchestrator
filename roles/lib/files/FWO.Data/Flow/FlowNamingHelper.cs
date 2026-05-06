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
    }
}
