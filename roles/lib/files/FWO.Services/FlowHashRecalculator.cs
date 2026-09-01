using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Flow;
using FWO.Logging;

namespace FWO.Services
{
    /// <summary>
    /// Repairs the hashes stored in the flow database when they no longer match the current hash logic,
    /// e.g. after a change to the hash generation. Technical entries are recalculated from their own data,
    /// non-technical entries keep their randomly generated hash, and groups and accesses are recalculated
    /// from the recalculated hashes of their members. Only entries whose hash actually changed are written.
    ///
    /// The recalculation is not interlocked with flow creation, which is an accepted risk: it rewrites the
    /// hash of a flow entry, which is its deduplication identity, while FlowDbCreator resolves flow entries by
    /// hash from its own snapshot. A flow creation from the request module that overlaps a recalculation can
    /// therefore reuse an entry whose hash has just moved, or fail on the unique hash constraint of a hash the
    /// recalculation has just assigned to another entry; repeating the create flow action resolves it. In the
    /// other direction the recalculation itself fails, rolls back as one transaction and is retried by the next
    /// flow sync. Hashes only change when the hash logic changes, so a recalculation happens once per such
    /// change rather than regularly, which keeps the window for both cases very small.
    /// </summary>
    public class FlowHashRecalculator
    {
        private const string LogMessageTitle = "Flow hash recalculation";

        private readonly ApiConnection apiConnection;

        /// <summary>
        /// Creates a new flow hash recalculator with API access.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        public FlowHashRecalculator(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// Recalculates all flow hashes and sends the ones that changed to the flow database. The mutation result
        /// is not evaluated, so a returned outcome states what was sent, not what the database applied.
        /// </summary>
        /// <param name="flowData">Flow data holding all flow entries with their stored hashes.</param>
        /// <returns>Whether changed hashes were sent, were already up to date, or could not be made unique.</returns>
        public async Task<FlowHashRecalculationOutcome> RecalculateFlowHashesAsync(FlowSyncFlowData flowData)
        {
            FlowHashRecalculationResult recalculation = FlowHashRecalculation.Calculate(flowData);

            if (recalculation.HasConflicts)
            {
                Log.WriteError(LogMessageTitle, $"Flow hashes cannot be recalculated because the recalculated hashes would no longer be unique: {string.Join("; ", recalculation.Conflicts)}. The affected flow entries have to be merged manually.");
                return FlowHashRecalculationOutcome.Conflict;
            }

            if (!recalculation.HasChanges)
            {
                Log.WriteInfo(LogMessageTitle, "No flow hashes needed to be updated.");
                return FlowHashRecalculationOutcome.NoChanges;
            }

            // the mutation writes all entry types in one transaction, of which only the first result is returned,
            // so the number of rows the database actually changed is not available here
            await apiConnection.SendQueryAsync<List<MutationResult>>(FlowQueries.updateFlowHashes, BuildUpdateVariables(recalculation));

            Log.WriteInfo(LogMessageTitle, $"Sent {recalculation.ChangeCount} intended flow hash changes " +
                $"({recalculation.NwObjects.Count} network objects, {recalculation.NwGroups.Count} network groups, " +
                $"{recalculation.SvcObjects.Count} service objects, {recalculation.SvcGroups.Count} service groups, " +
                $"{recalculation.TimeObjects.Count} time objects, {recalculation.Accesses.Count} accesses). " +
                "These are the changes the recalculation asked for, not confirmed row counts: whether they were " +
                "applied is reported by the next hash consistency check.");
            return FlowHashRecalculationOutcome.Updated;
        }

        /// <summary>
        /// Builds the variables of the update mutation. Entries whose current hash is claimed by another entry
        /// are moved to a temporary random hash first, within the same transaction, so that the unique hash
        /// constraint holds no matter in which order the updates are applied.
        /// </summary>
        private static object BuildUpdateVariables(FlowHashRecalculationResult recalculation)
        {
            return new
            {
                nwObjectTempHashes = BuildNwObjectUpdates(recalculation.NwObjects, true),
                nwObjectHashes = BuildNwObjectUpdates(recalculation.NwObjects, false),
                nwGroupTempHashes = BuildNwGroupUpdates(recalculation.NwGroups, true),
                nwGroupHashes = BuildNwGroupUpdates(recalculation.NwGroups, false),
                svcObjectTempHashes = BuildSvcObjectUpdates(recalculation.SvcObjects, true),
                svcObjectHashes = BuildSvcObjectUpdates(recalculation.SvcObjects, false),
                svcGroupTempHashes = BuildSvcGroupUpdates(recalculation.SvcGroups, true),
                svcGroupHashes = BuildSvcGroupUpdates(recalculation.SvcGroups, false),
                timeObjectTempHashes = BuildTimeObjectUpdates(recalculation.TimeObjects, true),
                timeObjectHashes = BuildTimeObjectUpdates(recalculation.TimeObjects, false),
                accessTempHashes = BuildAccessUpdates(recalculation.Accesses, true),
                accessHashes = BuildAccessUpdates(recalculation.Accesses, false)
            };
        }

        private static List<object> BuildNwObjectUpdates(List<FlowHashChange> changes, bool temporary)
        {
            return BuildUpdates(changes, temporary, id => new { nwobj_id = new { _eq = id } }, hash => new { nwobj_hash = hash });
        }

        private static List<object> BuildNwGroupUpdates(List<FlowHashChange> changes, bool temporary)
        {
            return BuildUpdates(changes, temporary, id => new { nwgrp_id = new { _eq = id } }, hash => new { nwgrp_hash = hash });
        }

        private static List<object> BuildSvcObjectUpdates(List<FlowHashChange> changes, bool temporary)
        {
            return BuildUpdates(changes, temporary, id => new { svcobj_id = new { _eq = id } }, hash => new { svcobj_hash = hash });
        }

        private static List<object> BuildSvcGroupUpdates(List<FlowHashChange> changes, bool temporary)
        {
            return BuildUpdates(changes, temporary, id => new { svcgrp_id = new { _eq = id } }, hash => new { svcgrp_hash = hash });
        }

        private static List<object> BuildTimeObjectUpdates(List<FlowHashChange> changes, bool temporary)
        {
            return BuildUpdates(changes, temporary, id => new { timeobj_id = new { _eq = id } }, hash => new { timeobj_hash = hash });
        }

        private static List<object> BuildAccessUpdates(List<FlowHashChange> changes, bool temporary)
        {
            return BuildUpdates(changes, temporary, id => new { access_id = new { _eq = id } }, hash => new { access_hash = hash });
        }

        /// <summary>
        /// Builds the update entries for one flow entry type: either the temporary hashes of the entries that
        /// have to vacate their current hash, or the recalculated hashes of all changed entries.
        /// </summary>
        private static List<object> BuildUpdates(
            List<FlowHashChange> changes,
            bool temporary,
            Func<long, object> buildWhere,
            Func<string, object> buildSet)
        {
            List<FlowHashChange> entriesToUpdate = temporary ? SelectChangesNeedingTemporaryHash(changes) : changes;

            return [.. entriesToUpdate.Select(change => (object)new
            {
                where = buildWhere(change.Id),
                _set = buildSet(temporary ? FlowHashGenerator.GenerateRandomHash() : change.NewHash)
            })];
        }

        /// <summary>
        /// Selects the entries whose current hash is the recalculated hash of another entry. Only those have to
        /// be moved out of the way first: every other entry either keeps a hash nobody claims or moves to a hash
        /// no entry holds any more, which the unique hash constraint accepts in any update order. Entries that
        /// do not change are not considered, because a recalculated hash colliding with one of them is reported
        /// as a conflict before anything is written.
        /// </summary>
        private static List<FlowHashChange> SelectChangesNeedingTemporaryHash(List<FlowHashChange> changes)
        {
            HashSet<string> claimedHashes = [.. changes.Select(change => change.NewHash)];
            return [.. changes.Where(change => claimedHashes.Contains(change.OldHash))];
        }
    }
}
