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

        protected static async Task<bool> UpdateRuleOwners(Func<Task<bool>> fullReinitFunc, Func<Task<bool>> incrementalFunc, bool isFullReInitialize)
        {
            return isFullReInitialize ? await fullReinitFunc() : await incrementalFunc();
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
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
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
