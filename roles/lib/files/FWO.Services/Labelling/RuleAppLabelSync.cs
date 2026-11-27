using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Logging;

namespace FWO.Services.Labelling
{
    /// <summary>
    /// Keeps the App labels of the rules (labelling.rule_label) in line with the active rule owner mappings of the
    /// configured mapping source: a rule carries the App label of every owner with an app id it is mapped to.
    /// </summary>
    /// <remarks>
    /// Runs after the rule owner mapping persisted a run. Missing assignments are added and obsolete ones closed, with one
    /// label change event per import control the changes are stamped with: the relevant import of the management of the
    /// rule, so that the reports show the change from now on (see <see cref="RuleAppLabelImportStamps"/>). A full sync
    /// compares all rules; it runs after a full reinitialize, on the first run of the process (which fills the App labels
    /// of existing mappings after the upgrade), after a failed sync and after the App key or the format of the App label
    /// values changed. The sync reads and
    /// closes only the assignments it wrote itself (label source <see cref="RuleLabelSource.kAppSync"/>): labels of a former
    /// App key are closed, without App key all of its labels, while labels of rules written by others are kept.
    /// A changed app id of an owner (or a changed name, if the values contain it) reaches the rules through the owner
    /// change it is recorded as (see <see cref="OwnerChangeRecorder"/>). Errors are logged instead of failing the mapping run.
    /// </remarks>
    /// <param name="apiConnection">Api connection with write access to the labelling tables.</param>
    /// <param name="globalConfig">Global config with the App key and the configured mapping source.</param>
    public class RuleAppLabelSync(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        private const string LogMessageTitle = "Rule App labels";
        private const int kChunkSize = 500;
        private static bool fullSyncDone;
        // the App key and value format the last full sync ran with: after a change of either all rules are compared again
        private static string fullSyncKeyName = "";
        private static string fullSyncValueFormat = "";

        private readonly AppLabelHandler appLabels = new(apiConnection, globalConfig.AppLabelKey, globalConfig.AppLabelValueFormat);
        private readonly Dictionary<long, long> changeEventIds = [];

        /// <summary>
        /// Forces the next sync to compare all rules, e.g. in tests.
        /// </summary>
        public static void RequestFullSync()
        {
            fullSyncDone = false;
        }

        private static void MarkFullSyncDone(string keyName, string valueFormat)
        {
            fullSyncKeyName = keyName;
            fullSyncValueFormat = valueFormat;
            fullSyncDone = true;
        }

        /// <summary>
        /// True if all rules have to be compared: no full sync ran yet, the last sync failed or the App key or the value
        /// format changed since the last full sync, as the labels of the former key or format are closed only by comparing
        /// all rules.
        /// </summary>
        private bool FullSyncNeeded => !fullSyncDone
            || !string.Equals(fullSyncKeyName, appLabels.KeyName, StringComparison.Ordinal)
            || !string.Equals(fullSyncValueFormat, appLabels.ValueFormat.Format, StringComparison.Ordinal);

        /// <summary>
        /// Syncs the App labels of the rules whose mappings changed, or of all rules if no full sync ran yet with the
        /// configured App key.
        /// </summary>
        /// <param name="affectedRuleIds">Rules whose rule owner mappings were added or removed.</param>
        /// <param name="importControlId">Import control of the mapping run, stamped on the changes of rules of managements without import.</param>
        public async Task Sync(IEnumerable<long> affectedRuleIds, long importControlId)
        {
            if (FullSyncNeeded)
            {
                await SyncAll(importControlId);
                return;
            }
            List<long> ruleIds = [.. affectedRuleIds.Distinct()];
            if (ruleIds.Count > 0)
            {
                await Run(ruleIds, importControlId);
            }
        }

        /// <summary>
        /// Syncs the App labels of all rules.
        /// </summary>
        /// <param name="importControlId">Import control of the mapping run, stamped on the changes of rules of managements without import.</param>
        public async Task SyncAll(long importControlId)
        {
            if (await Run(null, importControlId))
            {
                MarkFullSyncDone(appLabels.KeyName, appLabels.ValueFormat.Format);
            }
        }

        private async Task<bool> Run(List<long>? ruleIds, long importControlId)
        {
            try
            {
                // without App key (App labels switched off) no label is wanted, so the labels of the former key are closed
                HashSet<(long ruleId, long labelId)> wanted = appLabels.Enabled ? await LoadWantedAppLabels(ruleIds) : [];
                List<RuleLabelRow> activeLabels = await LoadActiveAppLabels(ruleIds);
                HashSet<(long ruleId, long labelId)> active = [.. activeLabels.Select(label => (label.RuleId, label.LabelId))];
                List<(long ruleId, long labelId)> toClose = [.. active.Except(wanted)];
                List<(long ruleId, long labelId)> toAdd = [.. wanted.Except(active)];
                Dictionary<long, long> stamps = await new RuleAppLabelImportStamps(apiConnection)
                    .Load(toClose.Concat(toAdd).Select(assignment => assignment.ruleId), importControlId);
                await Close(toClose, stamps);
                await Add(toAdd, stamps);
                Log.WriteDebug(LogMessageTitle, $"Import control {importControlId}: {toAdd.Count} App labels of rules added, {toClose.Count} closed.");
                return true;
            }
            catch (Exception exception)
            {
                // the next run compares all rules again, so that no change gets lost
                RequestFullSync();
                Log.WriteError(LogMessageTitle, $"App labels of rules could not be synced for import control {importControlId}.", exception);
                return false;
            }
        }

        /// <summary>
        /// The App labels the rules should carry: the App label of every owner with an app id they are mapped to.
        /// </summary>
        private async Task<HashSet<(long ruleId, long labelId)>> LoadWantedAppLabels(List<long>? ruleIds)
        {
            List<RuleOwnerApp> mappings = await LoadMappings(ruleIds);
            IEnumerable<AppLabelOwner> owners = mappings.Select(mapping => new AppLabelOwner(mapping.Owner.Id, mapping.Owner.Name, mapping.Owner.AppIdExternal));
            Dictionary<int, long> appLabelIds = await appLabels.GetAppLabelIds(owners);
            return [.. mappings
                .Where(mapping => appLabelIds.ContainsKey(mapping.Owner.Id))
                .Select(mapping => (mapping.RuleId, appLabelIds[mapping.Owner.Id]))];
        }

        private async Task<List<RuleOwnerApp>> LoadMappings(List<long>? ruleIds)
        {
            short mappingSourceId = (short)globalConfig.OwnerSoruceMappingID;
            if (ruleIds == null)
            {
                return await apiConnection.SendQueryAsync<List<RuleOwnerApp>>(LabellingQueries.getAllActiveRuleOwnerApps, new { mappingSourceId });
            }
            return await apiConnection.SendQueryAsync<List<RuleOwnerApp>>(LabellingQueries.getActiveRuleOwnerApps,
                new { ruleIds, mappingSourceId }, chunkingOptions: QueryChunks("ruleIds"));
        }

        /// <summary>
        /// The active labels of the rules as a whole written by this sync, of any key: the labels of another key than the
        /// configured one are App labels of a former App key, which are closed. Labels of other writers are not read.
        /// </summary>
        private async Task<List<RuleLabelRow>> LoadActiveAppLabels(List<long>? ruleIds)
        {
            if (ruleIds == null)
            {
                return await apiConnection.SendQueryAsync<List<RuleLabelRow>>(LabellingQueries.getAllActiveRuleAppLabels,
                    new { labelSource = RuleLabelSource.kAppSync });
            }
            return await apiConnection.SendQueryAsync<List<RuleLabelRow>>(LabellingQueries.getActiveRuleAppLabels,
                new { ruleIds, labelSource = RuleLabelSource.kAppSync }, chunkingOptions: QueryChunks("ruleIds"));
        }

        private async Task Close(List<(long ruleId, long labelId)> assignments, Dictionary<long, long> stamps)
        {
            foreach (IGrouping<long, (long ruleId, long labelId)> stampedAssignments in assignments.GroupBy(assignment => stamps[assignment.ruleId]))
            {
                long removedEventId = await GetChangeEventId(stampedAssignments.Key);
                var conditions = stampedAssignments.Select(assignment => new
                {
                    rule_id = new { _eq = assignment.ruleId },
                    label_id = new { _eq = assignment.labelId }
                }).ToList();
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.removeRuleLabelAssignments,
                    new { assignments = conditions, removedEventId, labelSource = RuleLabelSource.kAppSync },
                    chunkingOptions: MutationChunks("assignments"));
            }
        }

        private async Task Add(List<(long ruleId, long labelId)> assignments, Dictionary<long, long> stamps)
        {
            foreach (IGrouping<long, (long ruleId, long labelId)> stampedAssignments in assignments.GroupBy(assignment => stamps[assignment.ruleId]))
            {
                long createdEventId = await GetChangeEventId(stampedAssignments.Key);
                var labels = stampedAssignments.Select(assignment => new
                {
                    rule_id = assignment.ruleId,
                    label_id = assignment.labelId,
                    created_event_id = createdEventId,
                    label_source = RuleLabelSource.kAppSync
                }).ToList();
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.addRuleLabelAssignments, new { labels },
                    chunkingOptions: MutationChunks("labels"));
            }
        }

        /// <summary>Returns the change event of an import control, created once per sync.</summary>
        private async Task<long> GetChangeEventId(long importControlId)
        {
            if (!changeEventIds.TryGetValue(importControlId, out long eventId))
            {
                eventId = await appLabels.CreateChangeEvent(importControlId, false);
                changeEventIds[importControlId] = eventId;
            }
            return eventId;
        }

        private static QueryChunkingOptions QueryChunks(string variableName)
        {
            return new() { Enabled = true, ChunkVariableName = variableName, ChunkSize = kChunkSize, MergeMode = ChunkMergeMode.TopLevelArrayConcat };
        }

        private static QueryChunkingOptions MutationChunks(string variableName)
        {
            return new() { Enabled = true, ChunkVariableName = variableName, ChunkSize = kChunkSize, MergeMode = ChunkMergeMode.MutationAffectedRowsOnly };
        }
    }
}
