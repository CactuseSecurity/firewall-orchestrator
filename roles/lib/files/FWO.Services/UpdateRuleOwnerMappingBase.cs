using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Logging;
using FWO.Services.EventMediator.Events;


namespace FWO.Services
{
    public abstract class UpdateRuleOwnerMappingBase : IUpdateRuleOwnerMapping
    {
        protected const int MaxPendingImportsBeforeFullReinit = 3;
        private const int kAlertSeverity = 1;

        /// <summary>
        /// The empty mapping result, shared because it is only ever read - a run recording no mapping at all
        /// either found nothing to map or removed everything on purpose.
        /// </summary>
        protected static readonly List<RuleOwner> NoRuleOwners = [];

        private bool triggeredByChange;
        private List<RuleOwnerMappingChange> appliedChanges = [];
        protected const int RuleOwnerRemovalBatchSize = 500;
        protected const int RuleOwnerInsertBatchSize = 500;

        protected const string LogMessageTitle = "Update rule_owner Notifier";

        /// <summary>
        /// Where the run history actually lives. An alert that asks for the entry to be reset or for its
        /// write access to be checked is only actionable if it says where to go, and there is no UI action
        /// for either - so the alert has to carry the location itself.
        /// </summary>
        private const string kHistoryEntryLocation = "the row with config_user = 0 in table config.";

        /// <summary>
        /// Where the recorded runs can be read. The alert about a difference points here rather than at the
        /// config entry that holds them: the page lists the affected rules and owners and names the cause of
        /// each run, which is what the alert asks the admin to look at - the entry itself is a JSON value in
        /// a database row. The entry is named only where it has to be repaired by hand.
        /// </summary>
        private const string kMonitoringPageLocation = "Monitoring - Rule owner mapping runs (monitoring/rule_owner_mapping)";

        protected readonly ApiConnection apiConnection;
        protected readonly GlobalConfig globalConfig;

        /// <summary>
        /// Writes the per-rule and per-object messages, filtered by the configured level. Import failures,
        /// alerts and the per-run summary bypass this and are always logged.
        /// </summary>
        protected RuleOwnerMappingLogger MappingLog { get; }

        protected UpdateRuleOwnerMappingBase(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            MappingLog = new RuleOwnerMappingLogger(globalConfig.RuleOwnerMappingLogLevel);
        }

        public abstract OwnerMappingSourceStm Source { get; }

        public abstract Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null);

        /// <summary>
        /// Chooses between full reinitialize and incremental processing based on the event arguments and
        /// remembers whether the run follows a configuration change, which decides how its result is read.
        /// </summary>
        /// <param name="fullReinitFunc">Rebuilds every mapping.</param>
        /// <param name="incrementalFunc">Processes the pending imports.</param>
        /// <param name="eventArgs">Arguments of the triggering event.</param>
        /// <returns>True if the run succeeded.</returns>
        protected async Task<bool> UpdateRuleOwners(Func<Task<bool>> fullReinitFunc, Func<Task<bool>> incrementalFunc, UpdateRuleOwnerMappingEventArgs? eventArgs)
        {
            TakeOverEventArgs(eventArgs);
            return (eventArgs?.isFullReInitialize ?? false) ? await fullReinitFunc() : await incrementalFunc();
        }

        /// <summary>
        /// Remembers what the triggering event said about the run, which decides how its result is read.
        /// Sources that do not go through <see cref="UpdateRuleOwners"/> have to call this themselves.
        /// </summary>
        /// <param name="eventArgs">Arguments of the triggering event.</param>
        protected void TakeOverEventArgs(UpdateRuleOwnerMappingEventArgs? eventArgs)
        {
            triggeredByChange = eventArgs?.TriggeredByChange ?? false;
            appliedChanges = eventArgs?.Changes ?? [];
        }

        /// <summary>
        /// Loads all rules and mapping owners for a full reinitialize and delegates persistence of the rebuilt
        /// mapping set. An empty owner set is a legitimate configuration state - no owner matches, so nothing
        /// maps and the obsolete mappings have to go - and so is an empty rule result, as long as a rule base
        /// exists at all. Without one there is nothing to judge: see
        /// <see cref="CompleteFullReinitializeWithoutRules"/>.
        /// <para>
        /// <paramref name="rulesQuery"/> may be narrowed to the rules its source can map at all, so its empty
        /// result cannot tell the two apart by itself. <see cref="ActiveRuleBaseExists"/> answers that
        /// separately, and only on the path where there is nothing to map anyway.
        /// </para>
        /// </summary>
        protected async Task<bool> RunFullReinitialize<TMappingOwner>(string rulesQuery, Func<Task<List<TMappingOwner>>> loadOwnersFunc, Func<List<Rule>, List<TMappingOwner>, List<RuleOwner>> buildNewRuleOwnersFunc)
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(rulesQuery);
            var ownersTask = loadOwnersFunc();
            await Task.WhenAll(rulesTask, ownersTask);

            List<Rule> rulesToMap = rulesTask.Result ?? [];
            if (rulesToMap.Count == 0)
            {
                // the query matched nothing, which is a valid result while rules exist to match against
                bool ruleBaseExists = await ActiveRuleBaseExists();
                return ruleBaseExists
                    ? await FinalizeFullReinitialize(NoRuleOwners)
                    : await CompleteFullReinitializeWithoutRules();
            }

            List<TMappingOwner> ownersToMap = ownersTask.Result ?? [];
            var newRuleOwners = buildNewRuleOwnersFunc(rulesToMap, ownersToMap);
            return await FinalizeFullReinitialize(newRuleOwners);
        }

        /// <summary>
        /// Completes a full reinitialize that ran against an empty rule base. That is not the same as "the
        /// configured source matched no rule": without a rule there is nothing to map and nothing to judge,
        /// so replacing the stored state would remove every mapping on the strength of an input the run never
        /// had - on a fresh installation without rules, or if the rule query came back empty, on exactly the
        /// path taken when something is already wrong.
        /// <para>
        /// The stored mappings are therefore kept, no run is recorded - the run proved nothing and must not
        /// refresh "last verified without deviation" - and no empty-result alert is raised. The import control
        /// is still created and completed together with the older pending ones, so the backlog drains and the
        /// next run does not fall back to a full reinitialize again.
        /// </para>
        /// </summary>
        /// <returns>True, because there was nothing to do rather than something that failed.</returns>
        private async Task<bool> CompleteFullReinitializeWithoutRules()
        {
            Log.WriteWarning(LogMessageTitle, "No rule could be loaded for the full rule_owner reinitialize. " +
                "The existing mappings are kept, because an empty input says nothing about them.");

            long importControlId = await CreateImportControl();
            await CompleteImportControlFullReInit(importControlId);
            await DropPendingChangeNote();
            return true;
        }

        /// <summary>
        /// Drops the note of a saved configuration change for a rebuild that completed without recording a
        /// run. <see cref="RuleOwnerMappingRunHistory.Store"/> drops it for every rebuild that records one,
        /// and the note is about the rebuild having completed rather than about it having found anything -
        /// so a rebuild that completes and records nothing has to drop it here, or it is left for the next
        /// unrelated rebuild to be read as the intended change.
        /// </summary>
        protected async Task DropPendingChangeNote()
        {
            await new RuleOwnerMappingRunHistory(apiConnection).ClearPendingChanges();
        }

        /// <summary>
        /// Checks whether the managed rule base holds any rule a mapping source could match. Asked only when a
        /// source specific rule query came back empty, to tell a source that stopped matching from an
        /// installation that has no rule to match against.
        /// </summary>
        /// <returns>True if at least one active access rule exists.</returns>
        private async Task<bool> ActiveRuleBaseExists()
        {
            AggregateCount? activeRules = await apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countActiveRulesForOwnerMapping);
            return activeRules?.Aggregate?.Count > 0;
        }

        /// <summary>
        /// Persists a full reinitialize by replacing all active rule-owner mappings with the provided set.
        /// An empty set is a valid result and still replaces the previous state: the configured mapping
        /// source can legitimately stop matching any rule, and the obsolete mappings have to go. Because
        /// that is almost always a configuration problem, it raises an alert instead of failing silently.
        /// </summary>
        protected async Task<bool> FinalizeFullReinitialize(List<RuleOwner> newRuleOwners)
        {
            List<long> pendingImportsBefore = await LoadPendingImportControlIds();
            long importControlId = await CreateImportControl();

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = importControlId;
            }

            List<RuleOwner> previousRuleOwners = await SetAllActiveRuleOwnersRemoved(importControlId);
            await InsertNewRuleOwners(newRuleOwners);

            // recorded before the completion so a failure there does not lose the result of the run
            await RecordRun(importControlId, previousRuleOwners, newRuleOwners, pendingImportsBefore);
            await CompleteImportControlFullReInit(importControlId);

            if (newRuleOwners.Count == 0)
            {
                await AlertEmptyMappingResult();
            }

            Log.WriteInfo(LogMessageTitle, $"FULL rule_owner reinitialize completed with {newRuleOwners.Count} mappings.");
            return true;
        }

        /// <summary>
        /// Stores the result of a full reinitialize and alerts when it changed anything although the
        /// incremental mapping was up to date - that difference means the incremental path missed something.
        /// </summary>
        /// <param name="importControlId">Import control of the full reinitialize.</param>
        /// <param name="previousRuleOwners">Mappings that were active before the run.</param>
        /// <param name="newRuleOwners">Mappings the run rebuilt.</param>
        /// <param name="pendingImportsBefore">Imports still waiting to be mapped when the run started.</param>
        protected async Task RecordRun(long importControlId, List<RuleOwner> previousRuleOwners, List<RuleOwner> newRuleOwners, List<long> pendingImportsBefore)
        {
            RuleOwnerMappingRun run = RuleOwnerMappingRunHistory.BuildRun(importControlId, Source, previousRuleOwners, newRuleOwners,
                pendingImportsBefore, triggeredByChange, appliedChanges);

            // Store takes over a change that was saved but whose rebuild never completed, so the drift
            // decision below has to be made on what was stored, not on what this run knew by itself
            RuleOwnerMappingStoreResult stored = await new RuleOwnerMappingRunHistory(apiConnection).Store(run);
            run = stored.Run;

            Log.WriteInfo(LogMessageTitle, $"Full reinitialize {importControlId}: {run.AddedCount} mappings added, {run.RemovedCount} removed, " +
                $"{run.PendingImportsBefore.Count} imports were still pending.");

            if (stored.WriteFailed)
            {
                await ReportUnrecordedRun(importControlId);
            }

            // reported even when the run itself was not recorded: the difference is real either way, and
            // the alert above says why it will not show up on the monitoring page
            if (IndicatesDrift(run))
            {
                await AlertMappingDrift(run);
            }
        }

        /// <summary>
        /// Reports a full reinitialize whose result could not be written to the run history. The stored entry
        /// stays readable and keeps its earlier runs, so nothing in it marks the gap - the monitoring page
        /// shows the last run that was written as if it were the current state, and an old timestamp there is
        /// indistinguishable from an installation that legitimately has not rebuilt since. The alert is the
        /// only thing that separates the two.
        /// <para>
        /// The description names neither the import nor the write error, for the reason given on
        /// <see cref="ReportUnrepairableImports"/>: <see cref="RaiseAlert"/> recognizes a repeat by the exact
        /// text, and a write that fails for a standing reason fails on every run. The details go into the log.
        /// </para>
        /// </summary>
        /// <param name="importControlId">Import control of the run that was not recorded, for the log.</param>
        private async Task ReportUnrecordedRun(long importControlId)
        {
            Log.WriteError(LogMessageTitle, $"Full reinitialize {importControlId} is not recorded: the run history in config entry " +
                $"'{RuleOwnerMappingRunHistory.kConfigKey}' could not be written. See the error logged above for what the write " +
                "failed on.");
            await RaiseAlert("Rule owner mapping cannot record its runs: the run history in config entry " +
                $"'{RuleOwnerMappingRunHistory.kConfigKey}' could not be written, so the monitoring page keeps showing the last run " +
                "that was recorded and cannot say whether the running update is deviating now. Check the middleware's write access to " +
                kHistoryEntryLocation + " The affected runs are named in the middleware log.");
        }

        /// <summary>
        /// Decides whether a run result means the incremental mapping missed something. A deliberate change
        /// rebuilds a different state on purpose, a pending backlog explains the difference on its own, and an
        /// empty result has its own more precise alert - none of those is drift.
        /// <para>
        /// Read off <see cref="RuleOwnerMappingRun.State"/> rather than decided here, so this alert and the
        /// monitoring page cannot disagree about the same run. They did while both spelled the rule out.
        /// </para>
        /// </summary>
        /// <param name="run">Result of the full reinitialize.</param>
        /// <returns>True if the difference points at the incremental mapping.</returns>
        private static bool IndicatesDrift(RuleOwnerMappingRun run)
        {
            return run.State == RuleOwnerMappingRunState.Drift;
        }

        /// <summary>
        /// Reads the control ids of the imports that are still waiting to be mapped.
        /// </summary>
        /// <returns>The pending control ids, empty when the backlog is clear.</returns>
        protected async Task<List<long>> LoadPendingImportControlIds()
        {
            List<ImportControl>? pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);
            return pendingImports?.Select(import => import.ControlId).ToList() ?? [];
        }

        /// <summary>
        /// Processes all pending incremental imports in control-id order and falls back to full reinitialize when too many imports are queued.
        /// </summary>
        protected async Task<bool> RunIncremental(Func<ImportControl, Task> processIncrementalImportFunc, Func<Task<bool>> fullReinitFunc)
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);

            if (pendingImports == null || !pendingImports.Any())
            {
                return false;
            }

            if (pendingImports.Count > MaxPendingImportsBeforeFullReinit)
            {
                Log.WriteWarning(LogMessageTitle, $"Found {pendingImports.Count} pending imports. Falling back to full rule_owner reinitialize.");
                await AlertFullReinitFallback();
                return await fullReinitFunc();
            }

            List<long> failedImportControlIds = [];

            foreach (var import in pendingImports.OrderBy(i => i.ControlId))
            {
                try
                {
                    await processIncrementalImportFunc(import);
                }
                catch (Exception ex)
                {
                    // one broken import must not block the pending imports behind it, so the loop
                    // continues and the failures are reported instead of being swallowed
                    failedImportControlIds.Add(import.ControlId);
                    Log.WriteError(LogMessageTitle, $"Error while processing import_control {import.ControlId}. ", ex);
                }
            }

            if (failedImportControlIds.Count > 0)
            {
                return await HandleFailedImports(failedImportControlIds, fullReinitFunc);
            }

            // every pending import was processed, so an import failing again later starts counting from zero
            await new RuleOwnerMappingRunHistory(apiConnection).ClearFailedImports();
            return true;
        }

        /// <summary>
        /// Loads rule-owner mapping input for one incremental import, builds new mappings, and persists the delta.
        /// </summary>
        protected async Task ProcessIncrementalImport<TMappingOwner>(ImportControl import, Func<ImportControl, Task<(List<Rule> rulesToMap, List<TMappingOwner> owners, List<RuleOwner> ruleOwnersToRemove)>> handleRuleImportFunc,
            Func<ImportControl, Task<(List<Rule> rulesToMap, List<TMappingOwner> owners, List<RuleOwner> ruleOwnersToRemove)>> handleOwnerImportFunc, Func<List<Rule>, List<TMappingOwner>, List<RuleOwner>> buildNewRuleOwnersFunc)
        {
            List<Rule> rulesToMap;
            List<TMappingOwner> owners;
            List<RuleOwner> ruleOwnersToRemove;

            switch (import.ImportTypeId)
            {
                case ImportType.RULE:
                    (rulesToMap, owners, ruleOwnersToRemove) = await handleRuleImportFunc(import);
                    break;

                case ImportType.OWNER:
                    (rulesToMap, owners, ruleOwnersToRemove) = await handleOwnerImportFunc(import);
                    break;

                default:
                    throw new NotSupportedException($"ImportType '{import.ImportTypeId}' is not supported in LoadRulesAndOwnersAsync.");
            }

            var newRuleOwners = buildNewRuleOwnersFunc(rulesToMap, owners);
            await FinalizeIncrementalImport(newRuleOwners, ruleOwnersToRemove, import.ControlId);
        }

        /// <summary>
        /// Persists one incremental import by marking obsolete mappings removed and inserting rebuilt mappings for the same control id.
        /// </summary>
        protected async Task FinalizeIncrementalImport(List<RuleOwner> newRuleOwners, List<RuleOwner> ruleOwnersToRemove, long importControlId)
        {
            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = importControlId;
            }

            await SetAffectedRuleOwnersRemoved(ruleOwnersToRemove, importControlId);
            await InsertNewRuleOwners(await DropStillActiveMappings(newRuleOwners));
            await CompleteImportControl(importControlId);
        }

        /// <summary>
        /// Drops mappings that are still active after the removal step so the insert cannot collide with the
        /// partial unique index on (rule_id, owner_id) where removed is null. An owner insert or reactivation
        /// rebuilds the mapping for every rule without removing anything first, and the on_conflict clause on
        /// pk_rule_owner (rule_id, owner_id, created) does not catch that because the created value differs.
        /// </summary>
        /// <param name="newRuleOwners">Mappings that were just rebuilt for this import.</param>
        /// <returns>The mappings that are safe to insert.</returns>
        protected async Task<List<RuleOwner>> DropStillActiveMappings(List<RuleOwner> newRuleOwners)
        {
            if (newRuleOwners.Count == 0)
            {
                return newRuleOwners;
            }

            List<RuleOwner> activeRuleOwners = await LoadActiveMappingsForSmallerKeySet(newRuleOwners);
            HashSet<(long RuleId, int OwnerId)> activePairs = activeRuleOwners.Select(ruleOwner => (ruleOwner.RuleId, ruleOwner.OwnerId)).ToHashSet();
            List<RuleOwner> insertableRuleOwners = newRuleOwners.Where(ruleOwner => !activePairs.Contains((ruleOwner.RuleId, ruleOwner.OwnerId))).ToList();

            int skippedCount = newRuleOwners.Count - insertableRuleOwners.Count;
            if (skippedCount > 0)
            {
                Log.WriteInfo(LogMessageTitle, $"Skipped {skippedCount} rule_owner mappings that are already active.");
            }

            return insertableRuleOwners;
        }

        /// <summary>
        /// Loads the currently active mappings, filtering by whichever key set is smaller: an owner import
        /// rebuilds few owners across all rules, a rule import few rules across all owners.
        /// </summary>
        /// <param name="newRuleOwners">Mappings that were just rebuilt for this import.</param>
        /// <returns>The active mappings covering the rebuilt set.</returns>
        private async Task<List<RuleOwner>> LoadActiveMappingsForSmallerKeySet(List<RuleOwner> newRuleOwners)
        {
            List<long> ruleIds = newRuleOwners.Select(ruleOwner => ruleOwner.RuleId).Distinct().ToList();
            List<int> ownerIds = newRuleOwners.Select(ruleOwner => ruleOwner.OwnerId).Distinct().ToList();

            List<RuleOwner>? activeRuleOwners = ruleIds.Count <= ownerIds.Count
                ? await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByRule, new { ruleIds })
                : await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByOwner, new { ownerIds });

            return activeRuleOwners ?? [];
        }

        /// <summary>
        /// Loads changed rules for one incremental rule import and fetches affected owners plus removable mappings.
        /// </summary>
        protected async Task<(List<Rule> rulesToMap, List<TMappingOwner> owners, List<RuleOwner> ruleOwnersToRemove)> HandleRuleImport<TMappingOwner>(ImportControl import, string changedRulesQuery, Func<Task<List<TMappingOwner>>> loadOwnersFunc)
        {
            var changelogRules = await apiConnection.SendQueryAsync<List<RuleChange>>(changedRulesQuery, new { controlId = import.ControlId });
            var rulesToMap = new List<Rule>();
            var rulesToRemove = new List<Rule>();
            var owners = new List<TMappingOwner>();
            var ruleOwnersToRemove = new List<RuleOwner>();

            if (!ProcessRuleChanges(changelogRules, rulesToMap, rulesToRemove))
            {
                return (new List<Rule>(), new List<TMappingOwner>(), new List<RuleOwner>());
            }

            if (rulesToMap.Any())
            {
                owners = await loadOwnersFunc();
            }

            if (rulesToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByRule, new { ruleIds = rulesToRemove.Select(r => r.Id).ToList() });
            }

            return (rulesToMap, owners, ruleOwnersToRemove);
        }

        protected async Task<long> CreateImportControl()
        {
            try
            {
                var result = await apiConnection.SendQueryAsync<InsertImportControl>(ImportQueries.addImportForRuleOwner, new { importTypeId = ImportType.ADMIN_VIA_REINITIALIZE_BTN });

                var firstControl = result.Returning.FirstOrDefault();

                if (firstControl == null)
                {
                    Log.WriteError(LogMessageTitle, "No ImportControl returned. Mutation may have failed.");
                    throw new InvalidOperationException("Failed to create ImportControl. Returning list empty.");
                }

                Log.WriteInfo(LogMessageTitle, $"Created new import control with ID {firstControl.ControlId}.");
                return firstControl.ControlId;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, "Error while creating a new import control.", exception);
                throw;
            }
        }

        /// <summary>
        /// Marks every active mapping as removed and returns the state that was just replaced, which the
        /// run history diffs against the rebuilt mappings.
        /// <para>
        /// This materializes every active mapping in memory, and the caller holds the rebuilt set alongside
        /// it while the diff is computed - so the peak is a small multiple of the active rule_owner rows.
        /// That is accepted deliberately: rule_owner holds one row per mapped rule and owner, which stays in
        /// the same order of magnitude as the rule base. Should an installation grow far beyond that, fetch
        /// the counts by aggregate and materialize only the pairs the history actually lists
        /// (kMaxListedPairs) instead of returning every row here.
        /// </para>
        /// </summary>
        /// <param name="controlId">Import control the removal is recorded under.</param>
        /// <returns>The mappings that were active before the removal.</returns>
        protected async Task<List<RuleOwner>> SetAllActiveRuleOwnersRemoved(long controlId)
        {
            try
            {
                UpdateRuleOwner? result = await apiConnection.SendQueryAsync<UpdateRuleOwner>(OwnerQueries.setAllActiveRuleOwnersRemoved, new { controlId });
                return result?.Returning ?? [];
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while marking all active rule owners as removed.", ex);
                throw;
            }
        }

        protected async Task SetAffectedRuleOwnersRemoved(List<RuleOwner> ruleOwnersToSetRemoved, long importControlId)
        {
            try
            {
                if (!ruleOwnersToSetRemoved.Any()) return;

                var listRuleOwnersToRemove = ruleOwnersToSetRemoved
                .Select(r => new
                {
                    rule_id = new { _eq = r.RuleId },
                    owner_id = new { _eq = r.OwnerId },
                    created = new { _eq = r.Created }
                })
                .ToList();

                await apiConnection.SendQueryAsync<RuleOwnerMutationWrapper>(OwnerQueries.setAffectedRuleOwnersRemoved,
                    new
                    {
                        objects = listRuleOwnersToRemove,
                        removed = importControlId
                    },
                    chunkingOptions: new QueryChunkingOptions
                    {
                        Enabled = true,
                        ChunkVariableName = "objects",
                        ChunkSize = RuleOwnerRemovalBatchSize,
                        MergeMode = ChunkMergeMode.MutationAffectedRowsOnly
                    });

            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle,
                    "Error while marking affected rule owners as removed.", ex);
                throw;
            }
        }

        protected async Task InsertNewRuleOwners(List<RuleOwner> ruleOwners)
        {
            if (!ruleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert.");
                return;
            }

            try
            {

                await apiConnection.SendQueryAsync<RuleOwnerMutationWrapper>(OwnerQueries.insertRuleOwners, new { objects = ruleOwners.ToList() },
                chunkingOptions: new QueryChunkingOptions
                {
                    Enabled = true,
                    ChunkVariableName = "objects",
                    ChunkSize = RuleOwnerInsertBatchSize,
                    MergeMode = ChunkMergeMode.MutationAffectedRowsAndReturning
                });

                Log.WriteInfo(LogMessageTitle, $"{ruleOwners.Count} rule owners inserted successfully.");
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while inserting new rule owners.", ex);
                throw;
            }
        }

        protected async Task CompleteImportControl(long importControlId)
        {
            try
            {
                await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerInc,
                new
                {
                    controlId = importControlId,
                    rule_owner_mapping_done = true
                }
            );
                Log.WriteInfo(LogMessageTitle, $"Import control {importControlId} completed successfully.");
            }
            catch (Exception ex)
            {
                // an import that was not marked done stays pending and would be processed again, so the
                // failure has to reach RunIncremental instead of leaving the run looking successful
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
                throw;
            }
        }

        protected async Task CompleteImportControlFullReInit(long importControlId)
        {
            try
            {
                await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerFull,
                new
                {
                    controlId = importControlId,
                    stopTime = DateTime.UtcNow,
                    successful = true,
                    rule_owner_mapping_done = true
                });

                Log.WriteInfo(LogMessageTitle, $"Import control {importControlId} completed successfully.");
            }
            catch (Exception ex)
            {
                // same reasoning as in CompleteImportControl: a rebuild whose import control was not
                // completed must not be reported as a successful run
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
                throw;
            }

            await DrainOlderPendingImports(importControlId);
        }

        /// <summary>
        /// Marks the imports the completed rebuild has already covered as done, and keeps a failure there to
        /// itself. The rebuild is written and its own import control is complete by now, so reporting the run
        /// as failed would send the caller after a state that is already correct - the UI would show the
        /// rebuild as failed and keep the configuration change outstanding. The backlog is drained again by
        /// the next run, or forces another full reinitialize, which is the same repair either way.
        /// </summary>
        /// <param name="importControlId">Import control of the completed rebuild.</param>
        private async Task DrainOlderPendingImports(long importControlId)
        {
            try
            {
                await CompleteOlderPendingImports(importControlId);
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while completing the imports the full reinitialize already covered. " +
                    "The rebuild itself is complete, the remaining imports are processed by the next run.", ex);
            }
        }

        protected async Task CompleteOlderPendingImports(long referenceControlId)
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);

            if (pendingImports == null || !pendingImports.Any())
            {
                return;
            }

            var olderImports = pendingImports.Where(i => i.ControlId < referenceControlId);

            foreach (var import in olderImports)
            {
                try
                {
                    await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerInc,
                        new
                        {
                            controlId = import.ControlId,
                            rule_owner_mapping_done = true
                        });

                    Log.WriteInfo(LogMessageTitle, $"Older import control {import.ControlId} marked as rule_owner_mapping_done.");
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while updating older import_control {import.ControlId}.", ex);
                }
            }
        }


        /// <summary>
        /// Reports imports that could not be processed and repairs the state when the same ones already
        /// failed on the previous run. A single failure is usually transient and is retried by the next run,
        /// so processing simply continues. One that persists would leave its changes unapplied indefinitely,
        /// because the healthy imports drain and the pending backlog never reaches the fallback threshold -
        /// the full reinitialize takes over instead, recomputing everything and completing the stuck import.
        /// </summary>
        /// <param name="failedImportControlIds">Control ids of the imports that failed.</param>
        /// <param name="fullReinitFunc">Rebuilds every mapping.</param>
        /// <returns>False after the first failure, the result of the rebuild after a repeated one.</returns>
        private async Task<bool> HandleFailedImports(List<long> failedImportControlIds, Func<Task<bool>> fullReinitFunc)
        {
            string failedIds = string.Join(", ", failedImportControlIds);
            RuleOwnerMappingRunHistory history = new(apiConnection);

            // the run history, not the alert, decides whether this is a repeat: an alert stops being open as
            // soon as somebody acknowledges it, which would leave a permanently failing import unrepaired
            RuleOwnerMappingFailedImportsResult failures = await history.RecordFailedImports(failedImportControlIds);

            if (failures.RepairBlockedUntilReset)
            {
                return await ReportUnrepairableImports(failedIds);
            }

            if (failures.WriteFailed && !failures.FailedBefore)
            {
                // nothing about this failure reaches the next run, so the repeat below is never established.
                // Checked after FailedBefore is known, because a repeat the entry already held survives a
                // failing save and is repaired right here rather than reported as unrepairable
                return await ReportUnrecordableImports(failedIds);
            }

            if (!failures.FailedBefore)
            {
                // raised on the state change only. This is the one condition that can repeat on every run, so
                // alerting per run would leave one acknowledged alert per run behind - see RaiseAlert
                await RaiseAlert($"Rule owner mapping failed for import_control {failedIds}. See the middleware log for details.");
                return false;
            }

            // the same import failing again is logged every run and repaired below; the alert raised on the
            // first failure stands for the condition and is not replaced by an identical one
            Log.WriteError(LogMessageTitle, $"Rule owner mapping failed again for import_control {failedIds}. " +
                "Falling back to full rule_owner reinitialize.");
            bool repaired = await fullReinitFunc();
            if (repaired)
            {
                // the rebuild recomputed everything and completed the stuck imports, so the failures are settled
                await history.ClearFailedImports();
            }
            return repaired;
        }

        /// <summary>
        /// Reports a failing import that cannot be repaired because the stored run history cannot be decoded.
        /// Nothing saves over such an entry, so the repeat the repair waits for is never recognized and the
        /// rebuild below it is never reached. Rebuilding on the first failure instead would recompute every
        /// mapping on every run for as long as both conditions last, which costs more than the stuck import,
        /// so the condition is reported and left to the admin - resetting the entry restores the repair.
        /// <para>
        /// The description names neither the imports nor a count, because this repeats on every run and
        /// <see cref="RaiseAlert"/> recognizes a repeat by the exact text: anything varying from run to run
        /// would leave one acknowledged alert per run behind. The ids go into the log instead.
        /// </para>
        /// </summary>
        /// <param name="failedIds">Control ids of the failing imports, for the log.</param>
        /// <returns>Always false - nothing was repaired.</returns>
        private async Task<bool> ReportUnrepairableImports(string failedIds)
        {
            Log.WriteError(LogMessageTitle, $"Rule owner mapping failed for import_control {failedIds} and cannot be repaired: " +
                $"the run history in config entry '{RuleOwnerMappingRunHistory.kConfigKey}' cannot be decoded, so a repeated " +
                "failure is never recognized. Resetting the entry restores the repair: clear its config_value in " +
                kHistoryEntryLocation);
            await RaiseAlert("Rule owner mapping cannot repair a failing import: the run history in config entry " +
                $"'{RuleOwnerMappingRunHistory.kConfigKey}' cannot be decoded and has to be reset - clear its config_value in " +
                kHistoryEntryLocation + " The affected imports are named in the middleware log.");
            return false;
        }

        /// <summary>
        /// Reports a failing import whose repeat can never be established, because this run's ids could not
        /// be written to the run history. The next run reads whatever the entry held before, which says
        /// nothing about this failure, so the repair keyed off a repeat is never reached - the same outage as
        /// an undecodable entry, with the write side as its cause. Rebuilding on the first failure instead
        /// would recompute every mapping on every run for as long as the writes keep failing, so the
        /// condition is reported and left to the admin.
        /// <para>
        /// A repeat the entry already held is not this case: it survives the failing save, and
        /// <see cref="HandleFailedImports"/> repairs on it before reaching here.
        /// </para>
        /// <para>
        /// The description names neither the imports nor the write error, for the reason given on
        /// <see cref="ReportUnrepairableImports"/>: <see cref="RaiseAlert"/> recognizes a repeat by the exact
        /// text. The details go into the log instead.
        /// </para>
        /// </summary>
        /// <param name="failedIds">Control ids of the failing imports, for the log.</param>
        /// <returns>Always false - nothing was repaired.</returns>
        private async Task<bool> ReportUnrecordableImports(string failedIds)
        {
            Log.WriteError(LogMessageTitle, $"Rule owner mapping failed for import_control {failedIds} and cannot be repaired: " +
                $"the run history in config entry '{RuleOwnerMappingRunHistory.kConfigKey}' could not be written, so this failure " +
                "is not remembered and a repeat is never recognized. See the error logged above for what the write failed on.");
            await RaiseAlert("Rule owner mapping cannot repair a failing import: the run history in config entry " +
                $"'{RuleOwnerMappingRunHistory.kConfigKey}' could not be written, so the failure is not remembered. " +
                "Check the middleware's write access to " + kHistoryEntryLocation +
                " The affected imports are named in the middleware log.");
            return false;
        }

        /// <summary>
        /// Raises an alert when the pending import backlog forces a full reinitialize, because that hides
        /// whatever stopped the incremental processing from keeping up.
        /// <para>
        /// The description names the threshold, not the actual backlog: <see cref="RaiseAlert"/> recognizes a
        /// repeat by the exact text, so a number differing from run to run would leave one open alert behind
        /// per run - and this condition repeats on every run for as long as the rebuild it falls back to keeps
        /// failing. The actual count is logged by the caller.
        /// </para>
        /// </summary>
        private async Task AlertFullReinitFallback()
        {
            await RaiseAlert($"Rule owner mapping fell back to a full reinitialize because more than {MaxPendingImportsBeforeFullReinit} " +
                "imports were pending. See the middleware log for the number.");
        }

        /// <summary>
        /// Raises an alert when a full reinitialize produced no mapping at all, which removes every existing
        /// mapping and almost always points at a misconfigured mapping source.
        /// </summary>
        private async Task AlertEmptyMappingResult()
        {
            await RaiseAlert($"Rule owner mapping source '{Source}' matched no rule. All existing rule_owner mappings were removed.");
        }

        /// <summary>
        /// Raises an alert when a full reinitialize changed mappings although no import was pending. With a
        /// correct incremental mapping the rebuilt state matches the stored one, so any difference means the
        /// incremental path missed a change - unless the run dropped an expired change note, in which case
        /// the difference is just as likely the effect of that never applied setting and is named as such.
        /// </summary>
        /// <param name="run">The recorded run holding the difference.</param>
        private async Task AlertMappingDrift(RuleOwnerMappingRun run)
        {
            await RaiseAlert($"Full rule_owner reinitialize {run.ControlId} added {run.AddedCount} and removed {run.RemovedCount} mappings " +
                $"although no import was pending. {DescribeDriftCause(run)} " +
                $"See {kMonitoringPageLocation} for the affected rules and owners.");
        }

        /// <summary>
        /// Names what the difference of a run most likely comes from. Asserting the incremental mapping is
        /// only justified while nothing else explains the difference; a change note the run has just dropped
        /// as expired does explain it, and the run cannot tell the two apart.
        /// </summary>
        /// <param name="run">The recorded run holding the difference.</param>
        /// <returns>The sentence naming the cause.</returns>
        private static string DescribeDriftCause(RuleOwnerMappingRun run)
        {
            return run.DroppedChangeRecordedAt == null
                ? "The incremental mapping missed these changes."
                : $"A mapping setting saved on {run.DroppedChangeRecordedAt:u} was never applied by a rebuild and is no longer " +
                  "taken into account, so this difference may be its intended effect rather than one the incremental mapping missed.";
        }

        /// <summary>
        /// Writes a log entry and an alert, following the platform convention of <see cref="AlertHelper.SetAlert"/>
        /// with <see cref="AlertHelper.AdditionalAlertData.CompareDesc"/>: the new alert is inserted and the
        /// older one for the same problem is acknowledged, so the open alert always carries the timestamp of
        /// the latest occurrence. Suppressing the insert instead would freeze that timestamp at the first
        /// occurrence, making "failed once last week" and "failing on every run since" indistinguishable.
        /// <para>
        /// A caller whose condition can persist across runs therefore has to raise only on a state change
        /// rather than once per run, or the alert list fills up with one acknowledged row per run of a job
        /// that repeats every few seconds. <see cref="HandleFailedImports"/> does that for an ordinary
        /// repeated failure, keyed off the run history.
        /// </para>
        /// <para>
        /// The callers that cannot are the ones whose condition is the run history itself being unusable -
        /// <see cref="ReportUnrepairableImports"/>, <see cref="ReportUnrecordableImports"/> and
        /// <see cref="ReportUnrecordedRun"/> - plus <see cref="AlertFullReinitFallback"/>, which has no state
        /// of its own to key off and repeats for as long as the rebuild it falls back to keeps failing. All
        /// four keep their description constant, which holds the open alert to one row - the acknowledged
        /// ones still accumulate.
        /// </para>
        /// </summary>
        /// <param name="description">Description shown in the alert and the log entry.</param>
        private async Task RaiseAlert(string description)
        {
            try
            {
                await AlertHelper.AddLogEntry(apiConnection, kAlertSeverity, LogMessageTitle, description, GlobalConst.kRuleOwnerMapping);
                await AlertHelper.SetAlert(apiConnection, LogMessageTitle, description, GlobalConst.kRuleOwnerMapping, AlertCode.RuleOwnerMapping,
                    new AlertHelper.AdditionalAlertData { CompareDesc = true });
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while raising a rule_owner mapping alert.", ex);
            }
        }

        protected static bool ProcessOwnerChanges(List<OwnerChange> changelogOwners, List<FwoOwner> ownersToAdd, List<FwoOwner> ownersToRemove)
        {
            if (changelogOwners == null || !changelogOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed owners found for rule-owner mapping. Nothing to map for this import.");
                return false;
            }
            foreach (var change in changelogOwners)
            {
                switch (change.ChangeAction)
                {
                    case ChangelogActionType.INSERT:
                    case ChangelogActionType.REACTIVATE:
                        ownersToAdd.Add(change.NewOwner);
                        break;

                    case ChangelogActionType.DELETE:
                    case ChangelogActionType.DEACTIVATE:
                        ownersToRemove.Add(change.OldOwner);
                        break;

                    case ChangelogActionType.CHANGE:
                        ownersToAdd.Add(change.NewOwner);
                        ownersToRemove.Add(change.OldOwner);
                        break;
                }
            }

            return true;
        }

        protected static bool ProcessRuleChanges(List<RuleChange> changelogRules, List<Rule> rulesToMap, List<Rule> rulesToRemove)
        {
            if (changelogRules == null || !changelogRules.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Nothing to map for this import.");
                return false;
            }

            foreach (var change in changelogRules)
            {
                switch (change.ChangeAction)
                {
                    case ChangelogActionType.INSERT:
                    case ChangelogActionType.REACTIVATE:
                        rulesToMap.Add(change.NewRule);
                        break;

                    case ChangelogActionType.DELETE:
                    case ChangelogActionType.DEACTIVATE:
                        rulesToRemove.Add(change.OldRule);
                        break;

                    case ChangelogActionType.CHANGE:
                        rulesToRemove.Add(change.OldRule);
                        rulesToMap.Add(change.NewRule);
                        break;
                }
            }

            return true;
        }
    }
}
