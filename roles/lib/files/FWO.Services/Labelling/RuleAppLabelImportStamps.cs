using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data.Labelling;
using FWO.Data.Report;
using FWO.Logging;
using System.Globalization;

namespace FWO.Services.Labelling
{
    /// <summary>
    /// Finds the import control id the App label changes of a rule are stamped with.
    /// </summary>
    /// <remarks>
    /// Rule reports show the labels active at the relevant import of a management: its last successful import with
    /// changes (or that of its super-manager, if newer). The import control of an owner mapping run comes from the global
    /// sequence and is usually newer than that, so a change stamped with it would only show after the next import with
    /// changes. A change is therefore stamped with the current relevant import of the management of the rule, and only
    /// with the mapping run while the management has no such import.
    /// </remarks>
    /// <param name="apiConnection">Api connection with read access to rules, managements and import controls.</param>
    public class RuleAppLabelImportStamps(ApiConnection apiConnection)
    {
        private const string LogMessageTitle = "Rule App labels";
        private const string kTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const int kChunkSize = 500;

        /// <summary>
        /// Returns the import control id per rule to stamp its App label changes with.
        /// </summary>
        /// <param name="ruleIds">Rules whose App labels change.</param>
        /// <param name="fallbackImportControlId">Import control of the mapping run, for rules of managements without import.</param>
        /// <returns>Import control id by rule id; every given rule is contained.</returns>
        public async Task<Dictionary<long, long>> Load(IEnumerable<long> ruleIds, long fallbackImportControlId)
        {
            List<long> distinctRuleIds = [.. ruleIds.Distinct()];
            Dictionary<long, long> stamps = distinctRuleIds.ToDictionary(ruleId => ruleId, _ => fallbackImportControlId);
            if (distinctRuleIds.Count == 0)
            {
                return stamps;
            }
            try
            {
                List<RuleManagement> rules = await apiConnection.SendQueryAsync<List<RuleManagement>>(LabellingQueries.getRuleManagements,
                    new { ruleIds = distinctRuleIds }, chunkingOptions: RuleIdChunks());
                Dictionary<int, long> importIds = await LoadRelevantImportIds([.. rules.Select(rule => rule.ManagementId).Distinct()]);
                foreach (RuleManagement rule in rules)
                {
                    if (importIds.TryGetValue(rule.ManagementId, out long importId))
                    {
                        stamps[rule.RuleId] = importId;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteWarning(LogMessageTitle, $"The relevant imports of the rules could not be loaded, App label changes are stamped " +
                    $"with import control {fallbackImportControlId}: {exception.Message}");
            }
            return stamps;
        }

        /// <summary>
        /// Returns the current relevant import id per management, as the rule reports determine it.
        /// </summary>
        private async Task<Dictionary<int, long>> LoadRelevantImportIds(List<int> managementIds)
        {
            if (managementIds.Count == 0)
            {
                return [];
            }
            var variables = new { time = DateTime.Now.ToString(kTimeFormat, CultureInfo.InvariantCulture), mgmIds = managementIds };
            List<ManagementReport> managements = await apiConnection.SendQueryAsync<List<ManagementReport>>(ReportQueries.getRelevantImportIdsAtTime, variables);
            Dictionary<int, long> ownImportIds = managements
                .Where(management => management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                .ToDictionary(management => management.Id, management => management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!.Value);
            Dictionary<int, long> relevantImportIds = [];
            foreach (ManagementReport management in managements.Where(management => managementIds.Contains(management.Id)))
            {
                long? superManagerImportId = management.SuperManagerId is int superManagerId && ownImportIds.TryGetValue(superManagerId, out long superImportId)
                    ? superImportId : null;
                long? ownImportId = ownImportIds.TryGetValue(management.Id, out long importId) ? importId : null;
                long? relevantImportId = ownImportId is long own && superManagerImportId is long super ? Math.Max(own, super) : ownImportId ?? superManagerImportId;
                if (relevantImportId is long relevant)
                {
                    relevantImportIds[management.Id] = relevant;
                }
            }
            return relevantImportIds;
        }

        private static QueryChunkingOptions RuleIdChunks()
        {
            return new() { Enabled = true, ChunkVariableName = "ruleIds", ChunkSize = kChunkSize, MergeMode = ChunkMergeMode.TopLevelArrayConcat };
        }
    }
}
