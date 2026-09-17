using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;


namespace FWO.Services
{
    public abstract class UpdateRuleOwnerMappingBase : IUpdateRuleOwnerMapping
    {
        protected const int MaxPendingImportsBeforeFullReinit = 3;
        private const int kAlertSeverity = 1;

        private bool triggeredByChange;
        private List<RuleOwnerMappingChange> appliedChanges = [];
        protected const int RuleOwnerRemovalBatchSize = 500;
        protected const int RuleOwnerInsertBatchSize = 500;

        protected const string LogMessageTitle = "Update rule_owner Notifier";
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
        /// Loads all rules and mapping owners for a full reinitialize and delegates persistence of the rebuilt mapping set.
        /// </summary>
        protected async Task<bool> RunFullReinitialize<TMappingOwner>(string rulesQuery, Func<Task<List<TMappingOwner>>> loadOwnersFunc, Func<List<Rule>, List<TMappingOwner>, List<RuleOwner>> buildNewRuleOwnersFunc)
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(rulesQuery);
            var ownersTask = loadOwnersFunc();
            await Task.WhenAll(rulesTask, ownersTask);

            var newRuleOwners = buildNewRuleOwnersFunc(rulesTask.Result, ownersTask.Result);
            return await FinalizeFullReinitialize(newRuleOwners);
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
            await new RuleOwnerMappingRunHistory(apiConnection).Store(run);

            Log.WriteInfo(LogMessageTitle, $"Full reinitialize {importControlId}: {run.AddedCount} mappings added, {run.RemovedCount} removed, " +
                $"{run.PendingImportsBefore.Count} imports were still pending.");

            if (IndicatesDrift(run))
            {
                await AlertMappingDrift(run);
            }
        }

        /// <summary>
        /// Decides whether a run result means the incremental mapping missed something. A deliberate change
        /// rebuilds a different state on purpose, a pending backlog explains the difference on its own, and an
        /// empty result has its own more precise alert - none of those is drift.
        /// </summary>
        /// <param name="run">Result of the full reinitialize.</param>
        /// <returns>True if the difference points at the incremental mapping.</returns>
        private static bool IndicatesDrift(RuleOwnerMappingRun run)
        {
            return run.DiffMeaningful && !run.TriggeredByChange && run.MappingCount > 0 && run.AddedCount + run.RemovedCount > 0;
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
                await AlertFullReinitFallback(pendingImports.Count);
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

                await CompleteOlderPendingImports(importControlId);
            }
            catch (Exception ex)
            {
                // same reasoning as in CompleteImportControl: a rebuild whose import control was not
                // completed must not be reported as a successful run
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
                throw;
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
            bool failedBefore = await RaiseAlert($"Rule owner mapping failed for import_control {failedIds}. See the middleware log for details.");

            if (!failedBefore)
            {
                return false;
            }

            // the alert of the previous run is still open, so this is at least the second attempt
            Log.WriteWarning(LogMessageTitle, $"import_control {failedIds} failed again. Falling back to full rule_owner reinitialize.");
            return await fullReinitFunc();
        }

        /// <summary>
        /// Raises an alert when the pending import backlog forces a full reinitialize, because that hides
        /// whatever stopped the incremental processing from keeping up.
        /// </summary>
        /// <param name="pendingImportCount">Number of imports waiting to be mapped.</param>
        private async Task AlertFullReinitFallback(int pendingImportCount)
        {
            await RaiseAlert($"Rule owner mapping fell back to a full reinitialize because {pendingImportCount} imports were pending.");
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
        /// incremental path missed a change.
        /// </summary>
        /// <param name="run">The recorded run holding the difference.</param>
        private async Task AlertMappingDrift(RuleOwnerMappingRun run)
        {
            await RaiseAlert($"Full rule_owner reinitialize {run.ControlId} added {run.AddedCount} and removed {run.RemovedCount} mappings " +
                $"although no import was pending. The incremental mapping missed these changes. See config key '{RuleOwnerMappingRunHistory.kConfigKey}' for the affected rules and owners.");
        }

        /// <summary>
        /// Writes a log entry and an alert unless the same alert is already open, so a job repeating every
        /// few seconds does not flood the alert list with identical entries. The already open alert also
        /// tells a repeated problem from a first occurrence, see <see cref="HandleFailedImports"/>.
        /// </summary>
        /// <param name="description">Description shown in the alert and the log entry.</param>
        /// <returns>True if an alert with the same description was already open and nothing was written.</returns>
        private async Task<bool> RaiseAlert(string description)
        {
            try
            {
                if (await SameAlertAlreadyOpen(description))
                {
                    return true;
                }

                await AlertHelper.AddLogEntry(apiConnection, kAlertSeverity, LogMessageTitle, description, GlobalConst.kRuleOwnerMapping);
                await AlertHelper.SetAlert(apiConnection, LogMessageTitle, description, GlobalConst.kRuleOwnerMapping, AlertCode.RuleOwnerMapping,
                    new AlertHelper.AdditionalAlertData { CompareDesc = true });
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while raising a rule_owner mapping alert.", ex);
            }
            return false;
        }

        /// <summary>
        /// Checks whether an unacknowledged rule_owner mapping alert with the same description exists.
        /// </summary>
        /// <param name="description">Description to look for.</param>
        /// <returns>True if such an alert is still open.</returns>
        private async Task<bool> SameAlertAlreadyOpen(string description)
        {
            List<Alert>? openAlerts = await apiConnection.SendQueryAsync<List<Alert>>(MonitorQueries.getOpenAlerts);
            return openAlerts?.Any(alert => alert.AlertCode == AlertCode.RuleOwnerMapping && alert.Description == description) == true;
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
