using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;


namespace FWO.Services
{
    /// <summary>
    /// Handles the rule owner mapping while the mapping source is disabled. No mapping is calculated and the
    /// mappings left over from a previously configured source are removed when a full reinitialize is requested.
    /// </summary>
    public class UpdateRuleOwnerMappingDisabled : UpdateRuleOwnerMappingBase
    {
        private const int kActiveRuleOwnerProbeLimit = 1;

        /// <inheritdoc/>
        public override OwnerMappingSourceStm Source => OwnerMappingSourceStm.Disabled;

        /// <summary>
        /// Creates the handler for the disabled owner mapping source.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public UpdateRuleOwnerMappingDisabled(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig)
        {
        }

        /// <summary>
        /// Removes all existing rule owner mappings when a full reinitialize was requested, otherwise does nothing.
        /// </summary>
        /// <param name="eventArgs">Arguments of the triggering event.</param>
        /// <returns>True if the disabled mapping state was established successfully.</returns>
        public override async Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            // this source does not use UpdateRuleOwners, so the change note of the triggering save would
            // otherwise be lost and switching the mapping off would look like drift
            TakeOverEventArgs(eventArgs);

            if (!(eventArgs?.isFullReInitialize ?? false))
            {
                // the scheduled run has nothing to do while the owner mapping is disabled
                return false;
            }
            return await RemoveAllRuleOwnerMappings();
        }

        /// <summary>
        /// Marks all active rule owner mappings as removed, skipping the import control when there is nothing to remove.
        /// </summary>
        /// <returns>True if the rule owner mappings are gone afterwards.</returns>
        private async Task<bool> RemoveAllRuleOwnerMappings()
        {
            if (!await ActiveRuleOwnersExist())
            {
                Log.WriteInfo(LogMessageTitle, "Owner mapping is disabled, no active rule_owner mapping left to remove.");
                return true;
            }

            List<long> pendingImportsBefore = await LoadPendingImportControlIds();
            long importControlId = await CreateImportControl();
            List<RuleOwner> previousRuleOwners = await SetAllActiveRuleOwnersRemoved(importControlId);

            // switching the mapping off removes every mapping, which belongs in the run history just like any
            // other rebuild - otherwise the entry that explains an empty mapping table would be missing.
            // recorded before the completion so a failure there does not lose the result of the run
            await RecordRun(importControlId, previousRuleOwners, NoRuleOwners, pendingImportsBefore);
            await CompleteImportControlFullReInit(importControlId);

            Log.WriteInfo(LogMessageTitle, "All rule_owner mappings removed because the owner mapping source is disabled.");
            return true;
        }

        /// <summary>
        /// Checks whether any rule owner mapping is currently active.
        /// </summary>
        /// <returns>True if at least one active mapping exists.</returns>
        private async Task<bool> ActiveRuleOwnersExist()
        {
            List<RuleOwner>? activeRuleOwners = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getActiveRuleOwners, new { limit = kActiveRuleOwnerProbeLimit });
            return activeRuleOwners?.Count > 0;
        }
    }
}
