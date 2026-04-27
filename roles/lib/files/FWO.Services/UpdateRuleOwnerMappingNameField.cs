using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Services.EventMediator.Events;
using System.Text.RegularExpressions;

namespace FWO.Services
{
    public class UpdateRuleOwnerMappingNameField : UpdateRuleOwnerMappingBase
    {
        public override OwnerMappingSourceStm Source => OwnerMappingSourceStm.NameField;

        public UpdateRuleOwnerMappingNameField(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig)
        {
        }

        public override async Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            bool isFullReInitialize = eventArgs?.isFullReInitialize ?? false;
            return await UpdateRuleOwners(RunFullReinitialize, RunIncremental, isFullReInitialize);
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
                    await ProcessIncrementalImportNameField(import);
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while processing import_control {import.ControlId}. ", ex);
                    break;
                }
            }

            return true;
        }

        private async Task ProcessIncrementalImportNameField(ImportControl import)
        {
            List<Rule> rulesToMap = new List<Rule>();
            List<ModellingConnection> connectionOwners = new List<ModellingConnection>();
            List<RuleOwner> ruleOwnersToRemove = new List<RuleOwner>();

            switch (import.ImportTypeId)
            {
                case ImportType.RULE:
                    {
                        (rulesToMap, connectionOwners, ruleOwnersToRemove) = await HandleRuleImportNameField(import);
                        break;
                    }
                case ImportType.OWNER:
                    {
                        (rulesToMap, connectionOwners, ruleOwnersToRemove) = await HandleOwnerImportNameField(import);
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException($"ImportType '{import.ImportTypeId}' is not supported in LoadRulesAndOwnersAsync.");
                    }
            }

            var newRuleOwners = BuildNewRuleOwnersNameField(rulesToMap, connectionOwners);

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = import.ControlId;
            }

            await SetAffectedRuleOwnersRemoved(ruleOwnersToRemove, import.ControlId);

            await InsertNewRuleOwners(newRuleOwners);

            await CompleteImportControl(import.ControlId);
        }

        private async Task<(List<Rule> rulesToMap, List<ModellingConnection> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportNameField(ImportControl import)
        {
            var changelogRules = await apiConnection.SendQueryAsync<List<RuleChange>>(RuleQueries.getChangedRulesForRuleOwnerMappingNameField, new { controlId = import.ControlId });
            var rulesToMap = new List<Rule>();
            var rulesToRemove = new List<Rule>();
            var connectionOwners = new List<ModellingConnection>();
            var ruleOwnersToRemove = new List<RuleOwner>();

            if (!ProcessRuleChanges(changelogRules, rulesToMap, rulesToRemove))
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                return (new List<Rule>(), new List<ModellingConnection>(), new List<RuleOwner>());
            }

            if (rulesToMap.Any())
            {
                connectionOwners = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getOwnersForRuleOwnerNameField);
            }

            if (rulesToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByRule, new { ruleIds = rulesToRemove.Select(r => r.Id).ToList() });
            }

            return (rulesToMap, connectionOwners, ruleOwnersToRemove);
        }



        private async Task<(List<Rule> RulesToMap, List<ModellingConnection> connectionOwners, List<RuleOwner> RuleOwnersToRemove)> HandleOwnerImportNameField(ImportControl import)
        {
            var changelogOwners = await apiConnection.SendQueryAsync<List<OwnerChange>>(OwnerQueries.getChangedOwnersForRuleOwnerMappingNameField, new { controlId = import.ControlId });
            var ownersToAdd = new List<FwoOwner>();
            var connectionOwnersToAdd = new List<ModellingConnection>();
            var ownersToRemove = new List<FwoOwner>();
            var rulesToMap = new List<Rule>();
            var ruleOwnersToRemove = new List<RuleOwner>();

            if (!ProcessOwnerChanges(changelogOwners, ownersToAdd, ownersToRemove))
            {
                return (new List<Rule>(), new List<ModellingConnection>(), new List<RuleOwner>());
            }

            if (ownersToAdd.Any())
            {
                rulesToMap = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwnerNameField);
                connectionOwnersToAdd = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getOwnersForRuleOwnerNameFieldFilteredByOwner, new { ownerIds = ownersToAdd.Select(o => o.Id).ToList() });
            }

            if (ownersToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByOwner, new { ownerIds = ownersToRemove.Select(o => o.Id).ToList() });
            }
            return (rulesToMap, connectionOwnersToAdd, ruleOwnersToRemove);
        }

        private async Task<bool> RunFullReinitialize()
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForOwnerMappingNameField);
            var connectionOwnersTask = apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getOwnersForRuleOwnerNameField);
            await Task.WhenAll(rulesTask, connectionOwnersTask);
            var rules = rulesTask.Result;
            var owners = connectionOwnersTask.Result;

            var newRuleOwners = BuildNewRuleOwnersNameField(rules, owners);

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

        private List<RuleOwner> BuildNewRuleOwnersNameField(List<Rule> rulesToMap, List<ModellingConnection> connectionOwnersToMap)
        {
            // create a dictionary for owner app_id to id mapping for faster lookup (id matching FWOC{ID} in NameField)
            var connectionsToOwnerMap = connectionOwnersToMap.Where(c => c.AppId.HasValue)
                                              .ToDictionary(c => c.Id, c => c.AppId!.Value);
            var newRuleOwners = new List<RuleOwner>();

            // iterate through rules and create new mappings based on NameField
            foreach (Rule rule in rulesToMap)
            {
                var nameFieldValue = ExtractNameFieldValue(rule, globalConfig.ModModelledMarker, out var errorMessage);

                if (nameFieldValue.HasValue && connectionsToOwnerMap.TryGetValue(nameFieldValue.Value, out var ownerId))
                {
                    newRuleOwners.Add(new RuleOwner
                    {
                        RuleId = rule.Id,
                        OwnerId = ownerId,
                        RuleMetadataId = rule.Metadata.Id,
                        OwnerMappingSourceId = (int)OwnerMappingSourceStm.NameField
                    });
                }
                else if (errorMessage != null)
                {
                    Log.WriteWarning(LogMessageTitle, $"Rule {rule.Id}: {errorMessage}");
                }
            }
            return newRuleOwners;
        }

        public static int? ExtractNameFieldValue(Rule rule, string nameFieldValue, out string? errorMessage)
        {
            errorMessage = null;

            if (rule == null || string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(nameFieldValue))
            {
                errorMessage = $"Rule is null or NameFieldValue is empty or rule.Name for {rule?.Id} is empty";
                return null;
            }

            try
            {
                var pattern = $@"{Regex.Escape(nameFieldValue)}(\d+)";
                var match = Regex.Match(rule.Name, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                if (!match.Success)
                {
                    errorMessage = $"No match for marker '{nameFieldValue}' in '{rule.Name}'";
                    return null;
                }

                if (!int.TryParse(match.Groups[1].Value, out var connectionId))
                {
                    errorMessage = $"Extracted value is not a valid int: '{match.Groups[1].Value}'";
                    return null;
                }
                return connectionId;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return null;
            }
        }
    }
}
