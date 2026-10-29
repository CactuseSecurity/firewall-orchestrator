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
    /// Runs after the rule owner mapping persisted a run. Missing assignments are added and obsolete ones closed, both
    /// with one label change event carrying the import control id of the mapping run, which the reports need to place
    /// the change in time. A full sync compares all rules; it runs after a full reinitialize, on the first run of the
    /// process (which fills the App labels of existing mappings after the upgrade) and after a failed sync.
    /// Errors are logged instead of failing the mapping run.
    /// </remarks>
    /// <param name="apiConnection">Api connection with write access to the labelling tables.</param>
    /// <param name="globalConfig">Global config with the App key and the configured mapping source.</param>
    public class RuleAppLabelSync(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        private const string LogMessageTitle = "Rule App labels";
        private const int kChunkSize = 500;
        private static bool fullSyncDone;

        private readonly AppLabelHandler appLabels = new(apiConnection, globalConfig.AppLabelKey);
        private long? changeEventId;

        /// <summary>
        /// Forces the next sync to compare all rules, e.g. in tests.
        /// </summary>
        public static void RequestFullSync()
        {
            fullSyncDone = false;
        }

        private static void MarkFullSyncDone()
        {
            fullSyncDone = true;
        }

        /// <summary>
        /// Syncs the App labels of the rules whose mappings changed, or of all rules if no full sync ran yet.
        /// </summary>
        /// <param name="affectedRuleIds">Rules whose rule owner mappings were added or removed.</param>
        /// <param name="importControlId">Import control of the mapping run.</param>
        public async Task Sync(IEnumerable<long> affectedRuleIds, long importControlId)
        {
            if (!fullSyncDone)
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
        /// <param name="importControlId">Import control of the mapping run.</param>
        public async Task SyncAll(long importControlId)
        {
            if (await Run(null, importControlId))
            {
                MarkFullSyncDone();
            }
        }

        private async Task<bool> Run(List<long>? ruleIds, long importControlId)
        {
            if (!appLabels.Enabled)
            {
                return true;
            }
            try
            {
                List<RuleOwnerApp> mappings = await LoadMappings(ruleIds);
                List<RuleLabelRow> activeLabels = await LoadActiveAppLabels(ruleIds);
                Dictionary<string, long> appLabelIds = await appLabels.GetAppLabelIds(mappings.Select(mapping => mapping.Owner.AppIdExternal));
                HashSet<(long ruleId, long labelId)> wanted = [.. mappings
                    .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Owner.AppIdExternal))
                    .Select(mapping => (mapping.RuleId, appLabelIds[mapping.Owner.AppIdExternal!.Trim()]))];
                HashSet<(long ruleId, long labelId)> active = [.. activeLabels.Select(label => (label.RuleId, label.LabelId))];
                List<(long ruleId, long labelId)> toClose = [.. active.Except(wanted)];
                List<(long ruleId, long labelId)> toAdd = [.. wanted.Except(active)];
                await Close(toClose, importControlId);
                await Add(toAdd, importControlId);
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

        private async Task<List<RuleLabelRow>> LoadActiveAppLabels(List<long>? ruleIds)
        {
            if (ruleIds == null)
            {
                return await apiConnection.SendQueryAsync<List<RuleLabelRow>>(LabellingQueries.getAllActiveRuleAppLabels, new { keyName = appLabels.KeyName });
            }
            return await apiConnection.SendQueryAsync<List<RuleLabelRow>>(LabellingQueries.getActiveRuleAppLabels,
                new { keyName = appLabels.KeyName, ruleIds }, chunkingOptions: QueryChunks("ruleIds"));
        }

        private async Task Close(List<(long ruleId, long labelId)> assignments, long importControlId)
        {
            if (assignments.Count == 0)
            {
                return;
            }
            long removedEventId = await GetChangeEventId(importControlId);
            var conditions = assignments.Select(assignment => new
            {
                rule_id = new { _eq = assignment.ruleId },
                label_id = new { _eq = assignment.labelId }
            }).ToList();
            await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.removeRuleLabelAssignments,
                new { assignments = conditions, removedEventId }, chunkingOptions: MutationChunks("assignments"));
        }

        private async Task Add(List<(long ruleId, long labelId)> assignments, long importControlId)
        {
            if (assignments.Count == 0)
            {
                return;
            }
            long createdEventId = await GetChangeEventId(importControlId);
            var labels = assignments.Select(assignment => new
            {
                rule_id = assignment.ruleId,
                label_id = assignment.labelId,
                created_event_id = createdEventId
            }).ToList();
            await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.addRuleLabelAssignments, new { labels },
                chunkingOptions: MutationChunks("labels"));
        }

        private async Task<long> GetChangeEventId(long importControlId)
        {
            changeEventId ??= await appLabels.CreateChangeEvent(importControlId, false);
            return changeEventId.Value;
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
