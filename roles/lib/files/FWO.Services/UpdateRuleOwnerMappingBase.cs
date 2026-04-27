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
        protected const string LogMessageTitle = "Update rule_owner Notifier";
        protected readonly ApiConnection apiConnection;
        protected readonly GlobalConfig globalConfig;

        protected UpdateRuleOwnerMappingBase(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        public abstract OwnerMappingSourceStm Source { get; }

        public abstract Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null);

        /// <summary>
        /// Chooses between full reinitialize and incremental processing based on the event arguments.
        /// </summary>
        protected static async Task<bool> UpdateRuleOwners(Func<Task<bool>> fullReinitFunc, Func<Task<bool>> incrementalFunc, bool isFullReInitialize)
        {
            return isFullReInitialize ? await fullReinitFunc() : await incrementalFunc();
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
        /// </summary>
        protected async Task<bool> FinalizeFullReinitialize(List<RuleOwner> newRuleOwners)
        {
            if (!newRuleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert. Aborting import.");
                return false;
            }

            long importControlId = await CreateImportControl();

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = importControlId;
            }

            await SetAllActiveRuleOwnersRemoved(importControlId);
            await InsertNewRuleOwners(newRuleOwners);
            await CompleteImportControlFullReInit(importControlId);

            Log.WriteInfo(LogMessageTitle, "FULL rule_owner reinitialize completed.");
            return true;
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
                return await fullReinitFunc();
            }

            foreach (var import in pendingImports.OrderBy(i => i.ControlId))
            {
                try
                {
                    await processIncrementalImportFunc(import);
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while processing import_control {import.ControlId}. ", ex);
                    break;
                }
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
            await InsertNewRuleOwners(newRuleOwners);
            await CompleteImportControl(importControlId);
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

        protected async Task SetAllActiveRuleOwnersRemoved(long controlId)
        {
            try
            {
                await apiConnection.SendQueryAsync<RuleOwnerMutationWrapper>(OwnerQueries.setAllActiveRuleOwnersRemoved, new { controlId });
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
                await apiConnection.SendQueryAsync<RuleOwnerMutationWrapper>(OwnerQueries.insertRuleOwners, new { objects = ruleOwners });
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
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
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
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
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


        protected static bool ProcessOwnerChanges(List<OwnerChange> changelogOwners, List<FwoOwner> ownersToAdd, List<FwoOwner> ownersToRemove)
        {
            if (changelogOwners == null || !changelogOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed owners found for rule-owner mapping. Aborting incremental import.");
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
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
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
