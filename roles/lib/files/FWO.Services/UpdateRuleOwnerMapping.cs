using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;
using System.Text.Json;

namespace FWO.Services
{
    public class UpdateRuleOwnerMapping : FWImportChangesNotifierBase<UpdateRuleOwnerMappingEventArgs>
    {
        private const string LogMessageTitle = "Update rule_owner Notifier";
        protected readonly ApiConnection apiConnection;
        protected GlobalConfig globalConfig;

        public UpdateRuleOwnerMapping(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        protected override async Task<bool> Execute(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            await Task.Delay(1000);

            switch ((OwnerMappingSourceStm)globalConfig.OwnerSoruceMappingID)
            {
                case OwnerMappingSourceStm.IpBased:
                    return false;
                case OwnerMappingSourceStm.CustomField:
                    return await UpdateRuleOwners(eventArgs);
                case OwnerMappingSourceStm.NameField:
                    return false;
                case OwnerMappingSourceStm.Manual:
                    return false;
                default:
                    return false;
            }
        }

        public async Task HandleEvent(UpdateRuleOwnerMappingEvent evt)
        {
            try
            {
                bool success = await Run(evt.EventArgs);
                evt.EventArgs.Completion?.SetResult(success);
            }
            catch (Exception ex)
            {
                Log.WriteError("UpdateOwnerRuleMappings failed", ex.ToString());
                evt.EventArgs.Completion?.SetException(ex);
            }
        }

        public async Task<bool> UpdateRuleOwners(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            bool isFullReInitialize = eventArgs?.isFullReInitialize ?? false;

            if (isFullReInitialize)
            {
                return await RunFullReinitialize();
            }
            else
            {
                return await RunIncremental();
            }
        }

        private async Task<bool> RunFullReinitialize()
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForOwnerMapping);
            var ownersTask = apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwner);
            await Task.WhenAll(rulesTask, ownersTask);
            var rules = rulesTask.Result;
            var owners = ownersTask.Result;

            var newRuleOwners = BuildNewRuleOwnersCustomField(rules, owners);

            if (!newRuleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert. Aborting import.");
                return false;
            }

            long importControlId = await CreateImportControl();

            foreach( RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = importControlId;
            }

            await SetAllActiveRuleOwnersRemoved(importControlId);
            await InsertNewRuleOwners(newRuleOwners);
            await CompleteImportControlFullReInit(importControlId);

            Log.WriteInfo(LogMessageTitle, "FULL rule_owner reinitialize completed.");
            return true;
        }

        private async Task<bool> RunIncremental()
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);

            if (pendingImports == null || !pendingImports.Any())
            {
                return false;
            }

            foreach (var import in pendingImports.OrderBy(i => i.ControlId))
            {
                try
                {
                    await ProcessIncrementalImport(import);
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while processing import_control {import.ControlId}. ", ex);
                    break;
                }
            }

            return true;
        }

        private async Task ProcessIncrementalImport(ImportControl import)
        {
            List<Rule> rulesToMap = new List<Rule>();
            List<FwoOwner> owners = new List<FwoOwner>();
            List<RuleOwner> ruleOwnersToRemove = new List<RuleOwner>();

            switch (import.ImportTypeId)
            {
                case ImportType.RULE:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleRuleImportCustomField(import);
                        break;
                    }

                case ImportType.OWNER:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleOwnerImportCustomField(import);
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException($"ImportType '{import.ImportTypeId}' is not supported in LoadRulesAndOwnersAsync.");
                    }
            }

            var newRuleOwners = BuildNewRuleOwnersCustomField(rulesToMap, owners);

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = import.ControlId;
            }

            await SetAffectedRuleOwnersRemoved(ruleOwnersToRemove, import.ControlId);

            await InsertNewRuleOwners(newRuleOwners);

            await CompleteImportControl(import.ControlId);
        }

        private async Task<long> CreateImportControl()
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

                Log.WriteInfo(LogMessageTitle, $"Created new import control with ID { firstControl.ControlId }.");
                return firstControl.ControlId;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, "Error while creating a new import control.", exception);
                throw;
            }
        }

        private async Task SetAllActiveRuleOwnersRemoved(long controlId)
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

        private async Task SetAffectedRuleOwnersRemoved(List<RuleOwner> ruleOwnersToSetRemoved, long importControlId)
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

        private List<RuleOwner> BuildNewRuleOwnersCustomField(List<Rule> rulesToMap, List<FwoOwner> ownersToMap)
        {
            // create a dictionary for owner name to id mapping for faster lookup
            var ownerNameToIdMap = ownersToMap.Where(o => !string.IsNullOrWhiteSpace(o.ExtAppId))
                                              .ToDictionary(o => o.ExtAppId!, o => o.Id);
            var newRuleOwners = new List<RuleOwner>();

            // iterate through rules and create new mappings based on CustomFields
            foreach (Rule rule in rulesToMap)
            {
                if (string.IsNullOrWhiteSpace(rule.CustomFields))
                {
                    Log.WriteWarning(LogMessageTitle, $"Rule {rule.Id} has no CustomFields and will be skipped.");
                    continue;
                }

                try
                {
                    var customFields = JsonSerializer.Deserialize<Dictionary<string, string>>(rule.CustomFields.Replace("'", "\""));

                    if (customFields == null || !customFields.TryGetValue(globalConfig.OwnerSourceCustomFieldKey, out var ownerName))
                    {
                        continue;
                    }

                    if (ownerNameToIdMap.TryGetValue(ownerName, out var ownerId))
                    {
                        newRuleOwners.Add(new RuleOwner
                        {
                            RuleId = rule.Id,
                            OwnerId = ownerId,
                            RuleMetadataId = rule.Metadata.Id,
                            OwnerMappingSourceId = (int)OwnerMappingSourceStm.CustomField
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning(LogMessageTitle, $"Rule {rule.Id} has invalid CustomFields: {ex.Message}");
                }
            }
            return newRuleOwners;
        }

        private async Task InsertNewRuleOwners(List<RuleOwner> ruleOwners)
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

        private async Task CompleteImportControl(long importControlId)
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

        private async Task<(List<Rule> RulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportCustomField(ImportControl import)
        {
            var changelogRules = await apiConnection.SendQueryAsync<List<RuleChange>>(RuleQueries.getChangedRulesForRuleOwnerMapping, new { controlId = import.ControlId });
            if (changelogRules == null || !changelogRules.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            var relevantChanges = changelogRules
                .Where(IsOwnerSourceFieldChanged)
                .ToList();

            var rulesToMapTmp = relevantChanges
                .Select(c => c.NewRule)
                .Where(r => r != null)
                .ToList();

            var rulesToRemove = relevantChanges
                .Select(c => c.OldRule)
                .Where(r => r != null)
                .ToList();

            var ownersTmp = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwner);

            var ruleOwnersToRemoveTmp = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByRule, new { ruleIds = rulesToRemove.Select(r => r.Id).ToList() });

            return (rulesToMapTmp, ownersTmp, ruleOwnersToRemoveTmp);
        }

        private bool IsOwnerSourceFieldChanged(RuleChange ruleChange)
        {
            var oldFields = DeserializeCustomFields(ruleChange.OldRule?.CustomFields);
            var newFields = DeserializeCustomFields(ruleChange.NewRule?.CustomFields);

            oldFields.TryGetValue(globalConfig.OwnerSourceCustomFieldKey, out var oldValue);
            newFields.TryGetValue(globalConfig.OwnerSourceCustomFieldKey, out var newValue);

            return !string.Equals(oldValue, newValue, StringComparison.Ordinal);
        }

        private static Dictionary<string, string> DeserializeCustomFields(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(raw.Replace("'", "\"")) ?? new();
            }
            catch (JsonException)
            {
                return new();
            }
        }

        private async Task<(List<Rule> RulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleOwnerImportCustomField(ImportControl import)
        {
            var changelogOwners = await apiConnection.SendQueryAsync<List<OwnerChange>>(OwnerQueries.getChangedOwnersForRuleOwnerMapping, new { controlId = import.ControlId });
            var ownersToAdd = new List<FwoOwner>();
            var ownersToRemove = new List<FwoOwner>();
            var ruleOwnersToRemoveTmp = new List<RuleOwner>();
            var rulesToMapTmp = new List<Rule>();
            var ownersToUpdate = new List<FwoOwner>();
            if (changelogOwners == null || !changelogOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            foreach (var change in changelogOwners)
            {
                switch (change.ChangeAction)
                {
                    case 'I':
                        ownersToAdd.Add(change.NewOwner);
                        break;

                    case 'D':
                        ownersToRemove.Add(change.OldOwner);
                        break;

                    case 'C':
                        ownersToUpdate.Add(change.NewOwner);
                        break;
                }
            }

            if (ownersToAdd.Any())
            {
                rulesToMapTmp = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwner);
            }
            else if (ownersToUpdate.Any())
            {
                rulesToMapTmp = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwnerByOwnerToUpdate, new { ownerIds = ownersToUpdate.Select(o => o.Id).ToList() });
            }
            if (ownersToRemove.Any())
            {
                ruleOwnersToRemoveTmp = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByOwner, new { ownerIds = ownersToRemove.Select(o => o.Id).ToList() });
            }
            return (rulesToMapTmp, ownersToAdd.Concat(ownersToUpdate).ToList(), ruleOwnersToRemoveTmp);
        }


        private async Task CompleteImportControlFullReInit(long importControlId)
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
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
            }
        }
    }
}
