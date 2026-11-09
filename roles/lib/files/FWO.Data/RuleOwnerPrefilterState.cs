namespace FWO.Data
{
    /// <summary>
    /// State of the rule_owner mapping, read by the variance analysis before it decides between the
    /// rule_owner prefilter and the marker query. Assembled from the three state queries rather than
    /// deserialized, so the questions the analysis actually asks stay answerable on their own.
    /// </summary>
    public class RuleOwnerPrefilterState
    {
        /// <summary>
        /// Full reinitializes that are currently replacing the whole mapping.
        /// </summary>
        public List<ImportControl> RunningRuleOwnerRebuild { get; init; } = [];

        /// <summary>
        /// Imports that still wait to be mapped and that can have changed a marker. An entry without
        /// mgm_id is an owner import and affects every management.
        /// </summary>
        public List<ImportControl> PendingRuleAffectingImports { get; init; } = [];

        /// <summary>
        /// True once the mapping has been built at least once. Only then does an empty prefilter
        /// result mean "this owner has nothing here" rather than "nothing is mapped yet".
        /// </summary>
        public bool MappingExists { get; init; }

        /// <summary>
        /// True while a full reinitialize is rewriting the mapping. The prefilter would then read a
        /// partially rebuilt state, so the marker query is the only safe option.
        /// </summary>
        public bool RebuildRunning => RunningRuleOwnerRebuild.Count > 0;

        /// <summary>
        /// Checks whether an unprocessed import can affect the given managements.
        /// </summary>
        /// <param name="managementIds">Management and sub management ids the analysis reads.</param>
        /// <returns>True if at least one pending import can have changed their mapping.</returns>
        public bool HasPendingImportFor(HashSet<int> managementIds)
        {
            return PendingRuleAffectingImports.Exists(import => !import.MgmId.HasValue || managementIds.Contains(import.MgmId.Value));
        }
    }
}
