using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;
using System;
using System.Collections.Generic;
using System.Text;

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
            throw new NotImplementedException();
        }

        private async Task<bool> RunFullReinitialize()
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForOwnerMappingNameField);
            var ownersTask = apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerCustomField);// same should be Okay - app_id_external?
            await Task.WhenAll(rulesTask, ownersTask);
            var rules = rulesTask.Result;
            var owners = ownersTask.Result;

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

        private List<RuleOwner> BuildNewRuleOwnersNameField(List<Rule> rulesToMap, List<FwoOwner> ownersToMap)
        {
            // create a dictionary for owner name to id mapping for faster lookup
            var ownerNameToIdMap = ownersToMap.Where(o => !string.IsNullOrWhiteSpace(o.ExtAppId))   // ExtAppId is match FWOC{ID}?
                                              .ToDictionary(o => o.ExtAppId!, o => o.Id);
            var newRuleOwners = new List<RuleOwner>();

            // iterate through rules and create new mappings based on CustomFields
            foreach (Rule rule in rulesToMap)
            {
                try
                {
                    var NameFieldValue = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, globalConfig.CustomFieldOwnerKey, out _);

                    if (!string.IsNullOrWhiteSpace(NameFieldValue) && ownerNameToIdMap.TryGetValue(NameFieldValue, out var ownerId))
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


        private void ExtractNameFieldValue(Rule rule, out string? nameFieldValue)
        {
            try
            {
                nameFieldValue = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, globalConfig.CustomFieldOwnerKey, out _);
            }
            catch (Exception ex)
            {
                Log.WriteWarning(LogMessageTitle, $"Rule {rule.Id} has invalid CustomFields: {ex.Message}");
                nameFieldValue = null;
            }
        }
    }
}
