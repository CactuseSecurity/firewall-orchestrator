using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;

namespace FWO.Services
{
    public class UpdateRuleOwnerMappingCustomField : UpdateRuleOwnerMappingBase
    {
        public override OwnerMappingSourceStm Source => OwnerMappingSourceStm.CustomField;

        public UpdateRuleOwnerMappingCustomField(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig)
        {
        }

        public override async Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            bool isFullReInitialize = eventArgs?.isFullReInitialize ?? false;
            return await UpdateRuleOwners(RunFullReinitialize, RunIncremental, isFullReInitialize);
        }

        /// <summary>
        /// Delegates the full reinitialize flow to the shared base implementation using CustomField rule and owner sources.
        /// </summary>
        private async Task<bool> RunFullReinitialize() =>
            await RunFullReinitialize(RuleQueries.getRulesForOwnerMappingCustomField, () => apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerCustomField),
                BuildNewRuleOwnersCustomField);

        /// <summary>
        /// Delegates incremental processing of pending imports to the shared base implementation for CustomField mapping.
        /// </summary>
        private async Task<bool> RunIncremental() => await RunIncremental(ProcessIncrementalImportCustomField, RunFullReinitialize);

        /// <summary>
        /// Delegates one incremental import to the shared base implementation using CustomField-specific loaders and mapper.
        /// </summary>
        private async Task ProcessIncrementalImportCustomField(ImportControl import) =>
            await ProcessIncrementalImport(import, HandleRuleImportCustomField, HandleOwnerImportCustomField, BuildNewRuleOwnersCustomField);

        /// <summary>
        /// Delegates loading of changed rules, mapping owners, and removable mappings for a CustomField rule import.
        /// </summary>
        private async Task<(List<Rule> rulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportCustomField(ImportControl import) =>
            await HandleRuleImport(import, RuleQueries.getChangedRulesForRuleOwnerMappingCustomField, () => apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerCustomField));

        private async Task<(List<Rule> RulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleOwnerImportCustomField(ImportControl import)
        {
            var changelogOwners = await apiConnection.SendQueryAsync<List<OwnerChange>>(OwnerQueries.getChangedOwnersForRuleOwnerMappingCustomField, new { controlId = import.ControlId });
            var ownersToAdd = new List<FwoOwner>();
            var ownersToRemove = new List<FwoOwner>();
            var rulesToMap = new List<Rule>();
            var ruleOwnersToRemove = new List<RuleOwner>();

            if (!ProcessOwnerChanges(changelogOwners, ownersToAdd, ownersToRemove))
            {
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            if (ownersToAdd.Any())
            {
                rulesToMap = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwnerCustomField);
            }

            if (ownersToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByOwner, new { ownerIds = ownersToRemove.Select(o => o.Id).ToList() });
            }
            return (rulesToMap, ownersToAdd, ruleOwnersToRemove);
        }

        public List<RuleOwner> BuildNewRuleOwnersCustomField(List<Rule> rulesToMap, List<FwoOwner> ownersToMap)
        {
            // create a dictionary for owner name to id mapping for faster lookup
            var ownerNameToIdMap = ownersToMap.Where(o => !string.IsNullOrWhiteSpace(o.ExtAppId))
                                              .ToDictionary(o => o.ExtAppId!, o => o.Id);
            var newRuleOwners = new List<RuleOwner>();

            // iterate through rules and create new mappings based on CustomFields
            foreach (Rule rule in rulesToMap)
            {
                try
                {
                    var customFieldValue = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, globalConfig.CustomFieldOwnerKey, out _);

                    if (!string.IsNullOrWhiteSpace(customFieldValue) && ownerNameToIdMap.TryGetValue(customFieldValue, out var ownerId))
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

    }
}
