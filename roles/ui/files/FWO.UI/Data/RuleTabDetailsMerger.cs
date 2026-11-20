using FWO.Data;

namespace FWO.Ui.Data
{
    /// <summary>
    /// Merges the details fetched for a rule of the right sidebar rule tab into the rule shown in the report.
    /// The fetched (detailed) objects take precedence, because the objects of a report rule may only contain an
    /// overview (e.g. no group members). Objects only contained in the report rule are kept.
    /// </summary>
    public static class RuleTabDetailsMerger
    {
        /// <summary>
        /// Merges the source, destination and service details (including NAT translations) of the fetched rule into the rule.
        /// </summary>
        /// <param name="rule">Rule of the rule tab.</param>
        /// <param name="detailedRule">Rule with fetched details.</param>
        public static void Merge(Rule rule, Rule detailedRule)
        {
            rule.Froms = MergeLocations(rule.Froms, detailedRule.Froms);
            rule.Tos = MergeLocations(rule.Tos, detailedRule.Tos);
            rule.Services = MergeServices(rule.Services, detailedRule.Services);
            if (detailedRule.NatData != null)
            {
                rule.NatData ??= new();
                rule.NatData.TranslatedFroms = MergeLocations(rule.NatData.TranslatedFroms, detailedRule.NatData.TranslatedFroms);
                rule.NatData.TranslatedTos = MergeLocations(rule.NatData.TranslatedTos, detailedRule.NatData.TranslatedTos);
                rule.NatData.TranslatedServices = MergeServices(rule.NatData.TranslatedServices, detailedRule.NatData.TranslatedServices);
            }
        }

        /// <summary>
        /// Returns the detailed network locations followed by the locations only contained in the reported ones.
        /// </summary>
        public static NetworkLocation[] MergeLocations(NetworkLocation[]? reported, NetworkLocation[]? detailed)
        {
            NetworkLocation[] detailedLocations = detailed ?? [];
            HashSet<(long, long)> detailedKeys = [.. detailedLocations.Select(LocationKey)];
            return [.. detailedLocations, .. (reported ?? []).Where(location => !detailedKeys.Contains(LocationKey(location)))];
        }

        /// <summary>
        /// Returns the detailed services followed by the services only contained in the reported ones.
        /// </summary>
        public static ServiceWrapper[] MergeServices(ServiceWrapper[]? reported, ServiceWrapper[]? detailed)
        {
            ServiceWrapper[] detailedServices = detailed ?? [];
            HashSet<long> detailedIds = [.. detailedServices.Select(service => service.Content.Id)];
            return [.. detailedServices, .. (reported ?? []).Where(service => !detailedIds.Contains(service.Content.Id))];
        }

        private static (long, long) LocationKey(NetworkLocation location)
        {
            return (location.Object?.Id ?? 0, location.User?.Id ?? 0);
        }
    }
}
