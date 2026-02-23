using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;
using FWO.Services.EventMediator.Interfaces;
using Org.BouncyCastle.Asn1.Crmf;
using System.Security.AccessControl;
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

        protected override async Task<bool> Execute(UpdateRuleOwnerMappingEventArgs? eventArgs = null)  // FullReinitialize like ErrorModel AppServer for overview Data Sucessfull or Error
        {
            await Task.Delay(1000);


            switch (globalConfig.OwnerSoruceMappingID)
            {
                case OwnerMappingSourceStm.IP_BASED:
                    return false;
                case OwnerMappingSourceStm.CUSTOM_FIELD:
                    return await UpdateRuleOwners(eventArgs);
                case OwnerMappingSourceStm.NAME_FIELD:
                    return false;
                case OwnerMappingSourceStm.MANUEL:
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
            var lastImportControl = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getLastImportControl);
            var lastControl = lastImportControl.FirstOrDefault() ?? throw new InvalidOperationException("No import_control found.");

            long newControlId = lastControl.ControlId + 1;

            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForOwnerMapping);
            var ownersTask = apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwner);
            await Task.WhenAll(rulesTask, ownersTask);
            var rules = rulesTask.Result;
            var owners = ownersTask.Result;

            var newRuleOwners = BuildNewRuleOwnersCustomField(rules, owners, newControlId);

            if (!newRuleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert. Aborting import.");
                return false;
            }

            long importControlId = await CreateImportControl(newControlId);

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
                Log.WriteInfo(LogMessageTitle, "No pending import_controls found.");
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
            List<Rule> rulesToInsert = new List<Rule>();
            List<FwoOwner> owners = new List<FwoOwner>();
            List<RuleOwner> ruleOwnersToRemove = new List<RuleOwner>();

            switch (import.ImportTypeId)
            {
                case ImportType.RULE:
                    {
                        var changelogRules = await apiConnection.SendQueryAsync<List<RuleChange>>(RuleQueries.getChangedRulesForRuleOwnerMapping, new { controlId = import.ControlId });
                        if (changelogRules == null || !changelogRules.Any())
                        {
                            Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                            break;
                        }

                        var filteredChanges = changelogRules.Where(rc =>
                        {
                            var oldRaw = rc.OldRule?.CustomFields;
                            var newRaw = rc.NewRule?.CustomFields;

                            var oldFields = !string.IsNullOrWhiteSpace(oldRaw)
                                ? JsonSerializer.Deserialize<Dictionary<string, string>>(oldRaw.Replace("'", "\""))
                                : new Dictionary<string, string>();

                            var newFields = !string.IsNullOrWhiteSpace(newRaw)
                                ? JsonSerializer.Deserialize<Dictionary<string, string>>(newRaw.Replace("'", "\""))
                                : new Dictionary<string, string>();

                            oldFields ??= new Dictionary<string, string>();
                            newFields ??= new Dictionary<string, string>();

                            // get keys for check
                            oldFields.TryGetValue(globalConfig.OwnerSourceCustomFieldKey, out var oldValue);
                            newFields.TryGetValue(globalConfig.OwnerSourceCustomFieldKey, out var newValue);

                            return oldValue != newValue;
                        })
                        .ToList();

                        rulesToInsert = filteredChanges!.Select(c => c.NewRule).Where(r => r != null).ToList();
                        var rulesToRemove = filteredChanges!.Select(c => c.OldRule).Where(r => r != null).ToList();

                        owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwner);

                        ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemove, new { ruleIds = rulesToRemove.Select(r => r.Id).ToList() });
                        break;
                    }

                case ImportType.OWNER:
                    {
                        await RunFullReinitialize();
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException($"ImportType '{import.ImportTypeId}' is not supported in LoadRulesAndOwnersAsync.");
                    }
            }

            var newRuleOwners = BuildNewRuleOwnersCustomField(rulesToInsert, owners, import.ControlId);

            await SetAffectedRuleOwnersRemoved(ruleOwnersToRemove, import.ControlId);

            await InsertNewRuleOwners(newRuleOwners);

            await CompleteImportControl(import.ControlId);
        }

        private async Task<long> CreateImportControl(long importControlId)
        {
            try
            {
                var result = await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.addImportFoRuleOwner, new { controlId = importControlId, import_type_id = ImportType.ADMIN_VIA_REINITIALIZE_BTN });
                Log.WriteInfo(LogMessageTitle, $"Created new import control with ID {result.ControlId}.");
                return result.ControlId;
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

        private async Task SetAffectedRuleOwnersRemoved(List<RuleOwner> newRuleOwners, long importControlId)
        {
            try
            {
                if (!newRuleOwners.Any()) return;


                var listRuleOwnersToRemove = newRuleOwners
                .Select(r => new
                {
                    rule_id = new { _eq = r.rule_id },
                    owner_id = new { _eq = r.owner_id },
                    created = new { _eq = r.created }
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

        private List<RuleOwner> BuildNewRuleOwnersCustomField(List<Rule> rulesToMap, List<FwoOwner> ownersToMap, long importControlId)
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
                            rule_id = rule.Id,
                            owner_id = ownerId,
                            rule_metadata_id = rule.Metadata.Id,
                            owner_mapping_source_id = OwnerMappingSourceStm.CUSTOM_FIELD,
                            created = importControlId
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
                await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerInc, //Zwei verschiedene machen 
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
