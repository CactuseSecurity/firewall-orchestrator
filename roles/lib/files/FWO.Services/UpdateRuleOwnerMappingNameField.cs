using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Services.EventMediator.Events;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FWO.Services
{
    public class UpdateRuleOwnerMappingNameField : UpdateRuleOwnerMappingBase
    {
        private static readonly ConcurrentDictionary<string, Regex> NameFieldRegexCache = new();

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

        /// <summary>
        /// Delegates incremental processing of pending imports to the shared base implementation for NameField mapping.
        /// </summary>
        private async Task<bool> RunIncremental() => await RunIncremental(ProcessIncrementalImportNameField, RunFullReinitialize);

        /// <summary>
        /// Delegates one incremental import to the shared base implementation using NameField-specific loaders and mapper.
        /// </summary>
        private async Task ProcessIncrementalImportNameField(ImportControl import) =>
            await ProcessIncrementalImport(import, HandleRuleImportNameField, HandleOwnerImportNameField, BuildNewRuleOwnersNameField);

        /// <summary>
        /// Delegates loading of changed rules, modelling connections, and removable mappings for a NameField rule import.
        /// </summary>
        private async Task<(List<Rule> rulesToMap, List<ModellingConnection> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportNameField(ImportControl import) =>
            await HandleRuleImport(import, RuleQueries.getChangedRulesForRuleOwnerMappingNameField, () => apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getOwnersForRuleOwnerNameField));



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

        /// <summary>
        /// Delegates the full reinitialize flow to the shared base implementation using NameField rules and modelling connections.
        /// </summary>
        private async Task<bool> RunFullReinitialize() =>
            await RunFullReinitialize(RuleQueries.getRulesForOwnerMappingNameField, () => apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getOwnersForRuleOwnerNameField), BuildNewRuleOwnersNameField);

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

        public static int? ExtractNameFieldValue(Rule rule, string modelledMarker, out string? errorMessage)
        {
            errorMessage = null;

            if (rule == null)
            {
                errorMessage = "Rule is null";
                return null;
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                errorMessage = $"rule.Name is null or empty for rule id {rule.Id}";
                return null;
            }

            if (string.IsNullOrWhiteSpace(modelledMarker))
            {
                errorMessage = $"modelledMarker is null or empty for rule id {rule.Id}";
                return null;
            }


            try
            {
                var regex = GetNameFieldRegex(modelledMarker);
                var match = regex.Match(rule.Name);

                if (!match.Success)
                {
                    errorMessage = $"No match for marker '{modelledMarker}' in '{rule.Name}'";
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

        /// <summary>
        /// Returns a compiled regex for the configured marker and reuses it across rule evaluations.
        /// </summary>
        private static Regex GetNameFieldRegex(string modelledMarker)
        {
            return NameFieldRegexCache.GetOrAdd(modelledMarker, static value => new Regex($@"{Regex.Escape(value)}(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(50)));
        }
    }
}
