using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Services
{
    /// <summary>
    /// One rule-owner mapping pair as it is recorded in the run history.
    /// </summary>
    public class RuleOwnerPair
    {
        /// <summary>Rule the mapping belongs to.</summary>
        [JsonPropertyName("ruleId")]
        public long RuleId { get; set; }

        /// <summary>Owner the rule was mapped to.</summary>
        [JsonPropertyName("ownerId")]
        public int OwnerId { get; set; }

        /// <summary>
        /// Import the mapping was originally created by. Only set for a removed pair, where it says how long
        /// the obsolete mapping had been in place. For an added pair it is the run's own control id and is
        /// therefore not repeated here.
        /// </summary>
        [JsonPropertyName("created"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Created { get; set; }

        /// <summary>
        /// Rule metadata the mapping belongs to. Survives new rule versions, so the affected rule stays
        /// identifiable even after it was edited and got a new rule id.
        /// </summary>
        [JsonPropertyName("ruleMetadataId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? RuleMetadataId { get; set; }
    }

    /// <summary>
    /// Settings a recorded change can refer to.
    /// </summary>
    public static class RuleOwnerMappingChangeSetting
    {
        /// <summary>The mapping source itself was switched.</summary>
        public const string kSource = "source";

        /// <summary>The marker of the name field mapping was changed.</summary>
        public const string kMarker = "marker";

        /// <summary>The keys of the custom field mapping were changed.</summary>
        public const string kCustomFieldKeys = "customFieldKeys";

        /// <summary>Owner data the mapping is calculated from was edited, for instance the owner networks.</summary>
        public const string kOwnerData = "ownerData";
    }

    /// <summary>
    /// One deliberate change that made a full reinitialize necessary. Recorded so a run whose result
    /// differs on purpose can be told apart from one where the incremental mapping failed.
    /// </summary>
    public class RuleOwnerMappingChange
    {
        /// <summary>Setting that was changed, see <see cref="RuleOwnerMappingChangeSetting"/>.</summary>
        [JsonPropertyName("setting")]
        public string Setting { get; set; } = "";

        /// <summary>Value before the change, empty when it cannot be named.</summary>
        [JsonPropertyName("from")]
        public string From { get; set; } = "";

        /// <summary>Value after the change, empty when it cannot be named.</summary>
        [JsonPropertyName("to")]
        public string To { get; set; } = "";
    }

    /// <summary>
    /// Result of one full reinitialize, kept so drift of the incremental mapping becomes visible.
    /// </summary>
    public class RuleOwnerMappingRun
    {
        /// <summary>When the full reinitialize finished.</summary>
        [JsonPropertyName("runTime")]
        public DateTime RunTime { get; set; }

        /// <summary>
        /// Import control the full reinitialize was recorded under. Every pair in <see cref="Added"/> and
        /// <see cref="Removed"/> belongs to this id in the rule_owner table, so it is not repeated per entry.
        /// </summary>
        [JsonPropertyName("controlId")]
        public long ControlId { get; set; }

        /// <summary>Mapping source that produced the result.</summary>
        [JsonPropertyName("mappingSource")]
        public string MappingSource { get; set; } = "";

        /// <summary>Number of mappings after the run.</summary>
        [JsonPropertyName("mappingCount")]
        public int MappingCount { get; set; }

        /// <summary>Pairs that did not exist before the run.</summary>
        [JsonPropertyName("addedCount")]
        public int AddedCount { get; set; }

        /// <summary>Pairs that existed before the run and are gone afterwards.</summary>
        [JsonPropertyName("removedCount")]
        public int RemovedCount { get; set; }

        /// <summary>
        /// Pairs the full reinitialize established although they did not exist before, so the incremental
        /// mapping never created them. These are kept here because they cannot be recovered from rule_owner:
        /// the run stamps created = <see cref="ControlId"/> on every rebuilt mapping, not only on the new
        /// ones, so that column identifies the run rather than the difference.
        /// Capped, see <see cref="PairListsTruncated"/>; <see cref="AddedCount"/> always holds the full number.
        /// </summary>
        [JsonPropertyName("added")]
        public List<RuleOwnerPair> Added { get; set; } = [];

        /// <summary>
        /// Pairs that were active before and the full reinitialize does not produce any more, so the
        /// incremental mapping left them behind. Kept here for the same reason as <see cref="Added"/>: the
        /// run stamps removed = <see cref="ControlId"/> on every previously active mapping, not only on the
        /// obsolete ones. Capped, see <see cref="PairListsTruncated"/>;
        /// <see cref="RemovedCount"/> always holds the full number.
        /// </summary>
        [JsonPropertyName("removed")]
        public List<RuleOwnerPair> Removed { get; set; } = [];

        /// <summary>True when the pair lists were cut off and the counts are higher than the listed entries.</summary>
        [JsonPropertyName("pairListsTruncated")]
        public bool PairListsTruncated { get; set; }

        /// <summary>Imports that were still waiting to be mapped when the run started.</summary>
        [JsonPropertyName("pendingImportsBefore")]
        public List<long> PendingImportsBefore { get; set; } = [];

        /// <summary>
        /// True only when no import was pending. Otherwise the difference just reflects the unprocessed
        /// backlog instead of drift, and must not be read as a mapping problem.
        /// </summary>
        [JsonPropertyName("diffMeaningful")]
        public bool DiffMeaningful { get; set; }

        /// <summary>
        /// True when the run followed a deliberate change, for instance a different marker, another mapping
        /// source or edited owner networks. The rebuilt state then differs from the stored one by design, so
        /// the difference is expected and is not reported as drift.
        /// </summary>
        [JsonPropertyName("triggeredByChange")]
        public bool TriggeredByChange { get; set; }

        /// <summary>
        /// What was changed, so a run whose result differs on purpose can be understood later on.
        /// </summary>
        [JsonPropertyName("changes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<RuleOwnerMappingChange> Changes { get; set; } = [];
    }

    /// <summary>
    /// What is kept about the full reinitialize runs: the runs that found something, and separately the
    /// last one that found nothing. Splitting them keeps every slot of the limited history available for
    /// actual findings, while "last verified correct" stays visible and can never be pushed out.
    /// </summary>
    public class RuleOwnerMappingRunHistoryData
    {
        /// <summary>Most recent run that found no difference, or <see langword="null"/> if there was none yet.</summary>
        [JsonPropertyName("lastRunWithoutFindings")]
        public RuleOwnerMappingRun? LastRunWithoutFindings { get; set; }

        /// <summary>Runs that found a difference, newest first.</summary>
        [JsonPropertyName("runsWithFindings")]
        public List<RuleOwnerMappingRun> RunsWithFindings { get; set; } = [];

        /// <summary>
        /// Control ids of the imports that failed on the previous incremental run. An import failing again
        /// while it is listed here has failed twice in a row, which the run then repairs by a full
        /// reinitialize. Kept here rather than derived from an open alert: an alert stops being open as soon
        /// as somebody acknowledges it, which would silently disarm the repair.
        /// </summary>
        [JsonPropertyName("failedImports")]
        public List<long> FailedImports { get; set; } = [];
    }

    /// <summary>
    /// Keeps the results of the last full reinitialize runs in a config entry.
    /// A full reinitialize replaces every mapping, so the difference between the state before and the
    /// rebuilt state is what tells an actual change from a plain rebuild. With a healthy incremental
    /// mapping and an empty import backlog that difference is empty.
    /// </summary>
    public class RuleOwnerMappingRunHistory
    {
        /// <summary>
        /// Config key the history is stored under, written with config_user = 0.
        /// <para>
        /// That tier is FWO's public one: the anonymous role may select every config_user = 0 row, because
        /// the login page reads global config before anybody has signed in. Only the client certificate the
        /// GraphQL API requires keeps this out of reach - anyone holding one reads this entry without user
        /// credentials. Comparable operational data does not live here for that reason; import_control and
        /// alert have their own tables and are not readable by the anonymous role at all.
        /// </para>
        /// <para>
        /// Accepted for what is stored today: rule ids, owner ids and import control ids, no names and no
        /// rule content. Do not extend this entry with anything more revealing - move it to its own table
        /// with select limited to middleware-server, admin and auditor instead.
        /// </para>
        /// </summary>
        public const string kConfigKey = "ruleOwnerMappingRunHistory";

        /// <summary>How many runs with a finding are kept. Shown on the page, so it stays in one place.</summary>
        public const int kMaxRuns = 10;
        private const int kMaxListedPairs = 500;
        private const string kLogMessageTitle = "Update rule_owner Notifier";

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

        private readonly ApiConnection apiConnection;

        /// <summary>
        /// Creates the history writer.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        public RuleOwnerMappingRunHistory(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }

        /// <summary>
        /// Builds the result of one full reinitialize from the state before and the rebuilt mappings.
        /// </summary>
        /// <param name="controlId">Import control of the full reinitialize.</param>
        /// <param name="mappingSource">Mapping source that produced the result.</param>
        /// <param name="previousRuleOwners">Mappings that were active before the run.</param>
        /// <param name="newRuleOwners">Mappings the run rebuilt.</param>
        /// <param name="pendingImportsBefore">Imports still waiting to be mapped when the run started.</param>
        /// <param name="triggeredByChange">True when the run followed a deliberate change.</param>
        /// <param name="changes">What was changed, empty when nothing was recorded.</param>
        /// <returns>The recorded run.</returns>
        public static RuleOwnerMappingRun BuildRun(long controlId, OwnerMappingSourceStm mappingSource, List<RuleOwner> previousRuleOwners,
            List<RuleOwner> newRuleOwners, List<long> pendingImportsBefore, bool triggeredByChange = false,
            List<RuleOwnerMappingChange>? changes = null)
        {
            Dictionary<(long RuleId, int OwnerId), RuleOwner> previousByPair = ToPairMap(previousRuleOwners);
            Dictionary<(long RuleId, int OwnerId), RuleOwner> newByPair = ToPairMap(newRuleOwners);

            List<RuleOwnerPair> added = ToPairList(newByPair.Keys.Except(previousByPair.Keys), newByPair, withOrigin: false);
            List<RuleOwnerPair> removed = ToPairList(previousByPair.Keys.Except(newByPair.Keys), previousByPair, withOrigin: true);

            // switching the mapping source replaces every mapping by definition, so listing each pair would
            // only fill the config entry without telling anybody anything - the counts carry the information
            bool keepPairLists = !(changes ?? []).Any(change => change.Setting == RuleOwnerMappingChangeSetting.kSource);

            return new RuleOwnerMappingRun
            {
                RunTime = DateTime.UtcNow,
                ControlId = controlId,
                MappingSource = mappingSource.ToString(),
                MappingCount = newByPair.Count,
                AddedCount = added.Count,
                RemovedCount = removed.Count,
                Added = keepPairLists ? added.Take(kMaxListedPairs).ToList() : [],
                Removed = keepPairLists ? removed.Take(kMaxListedPairs).ToList() : [],
                PairListsTruncated = keepPairLists && (added.Count > kMaxListedPairs || removed.Count > kMaxListedPairs),
                PendingImportsBefore = pendingImportsBefore,
                DiffMeaningful = pendingImportsBefore.Count == 0,
                TriggeredByChange = triggeredByChange,
                Changes = changes ?? []
            };
        }

        /// <summary>
        /// Prepends the run to the stored history and drops everything beyond the newest entries.
        /// </summary>
        /// <param name="run">Run to store.</param>
        public async Task Store(RuleOwnerMappingRun run)
        {
            try
            {
                RuleOwnerMappingRunHistoryData history = await Load();

                // a run without findings updates "last verified correct", so a repeated rebuild cannot push
                // anything out of the limited history - but only when it could judge at all: with imports
                // still pending it proves nothing and must not claim a verification
                if (HasNoFindings(run) && run.DiffMeaningful)
                {
                    history.LastRunWithoutFindings = run;
                }

                // a deliberate change is kept even when it changed nothing: that it had no effect is exactly
                // what somebody who just edited the configuration needs to see
                if (!HasNoFindings(run) || run.TriggeredByChange)
                {
                    history.RunsWithFindings.Insert(0, run);
                    history.RunsWithFindings = history.RunsWithFindings.Take(kMaxRuns).ToList();
                }

                await Save(history);
            }
            catch (Exception ex)
            {
                // the history is a diagnostic aid, it must never break the mapping itself
                Log.WriteError(kLogMessageTitle, "Error while storing the rule_owner mapping run history.", ex);
            }
        }

        /// <summary>
        /// Remembers which imports failed and reports whether any of them had already failed on the previous
        /// run. The repair path keys off this instead of an open alert, so acknowledging the alert - the
        /// normal response to one - cannot disarm the repair of a persistently failing import.
        /// </summary>
        /// <param name="failedImportControlIds">Control ids of the imports that failed on this run.</param>
        /// <returns>True if at least one of them had already failed on the previous run.</returns>
        public async Task<bool> RecordFailedImports(List<long> failedImportControlIds)
        {
            try
            {
                RuleOwnerMappingRunHistoryData history = await Load();
                bool failedBefore = failedImportControlIds.Exists(history.FailedImports.Contains);

                history.FailedImports = failedImportControlIds;
                await Save(history);
                return failedBefore;
            }
            catch (Exception ex)
            {
                // without the stored state the run cannot tell a repeated failure from a first one. Reporting
                // "not seen before" only delays the repair by one run, while the opposite would rebuild
                // everything on a single transient failure
                Log.WriteError(kLogMessageTitle, "Error while recording the failed rule_owner mapping imports.", ex);
                return false;
            }
        }

        /// <summary>
        /// Forgets the remembered failures after a run that processed every pending import, so an import
        /// failing again much later is treated as the first failure it is.
        /// </summary>
        public async Task ClearFailedImports()
        {
            try
            {
                RuleOwnerMappingRunHistoryData history = await Load();
                if (history.FailedImports.Count == 0)
                {
                    return;
                }

                history.FailedImports = [];
                await Save(history);
            }
            catch (Exception ex)
            {
                Log.WriteError(kLogMessageTitle, "Error while clearing the failed rule_owner mapping imports.", ex);
            }
        }

        /// <summary>
        /// Writes the history to its config entry.
        /// </summary>
        /// <param name="history">History to store.</param>
        private async Task Save(RuleOwnerMappingRunHistoryData history)
        {
            await apiConnection.SendQueryAsync<object>(ConfigQueries.upsertConfigItem, new
            {
                config_key = kConfigKey,
                config_value = JsonSerializer.Serialize(history, SerializerOptions),
                config_user = 0
            });
        }

        /// <summary>
        /// Reads the stored history, runs with findings newest first.
        /// </summary>
        /// <returns>The stored history, empty when nothing is stored yet or the entry is unreadable.</returns>
        public async Task<RuleOwnerMappingRunHistoryData> Load()
        {
            try
            {
                List<ConfigItem>? configItems = await apiConnection.SendQueryAsync<List<ConfigItem>>(ConfigQueries.getConfigItemByKey, new { key = kConfigKey });
                string? storedValue = configItems?.FirstOrDefault()?.Value;

                return string.IsNullOrWhiteSpace(storedValue) ? new RuleOwnerMappingRunHistoryData() : Deserialize(storedValue);
            }
            catch (Exception ex)
            {
                Log.WriteError(kLogMessageTitle, "Error while reading the rule_owner mapping run history.", ex);
                return new RuleOwnerMappingRunHistoryData();
            }
        }

        /// <summary>
        /// Reads the stored value, accepting the plain run list written before the history was split into
        /// findings and the last clean run, so an installation does not lose what it recorded so far.
        /// </summary>
        /// <param name="storedValue">Stored config value.</param>
        /// <returns>The history in its current shape.</returns>
        private static RuleOwnerMappingRunHistoryData Deserialize(string storedValue)
        {
            if (!storedValue.TrimStart().StartsWith('['))
            {
                return JsonSerializer.Deserialize<RuleOwnerMappingRunHistoryData>(storedValue) ?? new RuleOwnerMappingRunHistoryData();
            }

            List<RuleOwnerMappingRun> storedRuns = JsonSerializer.Deserialize<List<RuleOwnerMappingRun>>(storedValue) ?? [];
            return new RuleOwnerMappingRunHistoryData
            {
                // same condition as in Store: a run that found nothing while imports were still pending
                // proves nothing and must not be migrated into "last verified correct"
                LastRunWithoutFindings = storedRuns.FirstOrDefault(run => HasNoFindings(run) && run.DiffMeaningful),
                RunsWithFindings = storedRuns.Where(run => !HasNoFindings(run)).Take(kMaxRuns).ToList()
            };
        }

        /// <summary>
        /// Checks whether a run found any difference at all.
        /// </summary>
        /// <param name="run">Run to check.</param>
        /// <returns>True if nothing was added or removed.</returns>
        private static bool HasNoFindings(RuleOwnerMappingRun run)
        {
            return run.AddedCount + run.RemovedCount == 0;
        }

        private static Dictionary<(long RuleId, int OwnerId), RuleOwner> ToPairMap(List<RuleOwner> ruleOwners)
        {
            return ruleOwners.GroupBy(ruleOwner => (ruleOwner.RuleId, ruleOwner.OwnerId))
                .ToDictionary(group => group.Key, group => group.First());
        }

        /// <summary>
        /// Turns the pairs into the stored form. The origin is only kept for mappings that were removed:
        /// for an added one it is the run's own control id and would only be repeated.
        /// </summary>
        /// <param name="pairs">Pairs to store.</param>
        /// <param name="ruleOwnersByPair">Mappings the pairs were taken from.</param>
        /// <param name="withOrigin">True to keep the import the mapping originally came from.</param>
        /// <returns>The stored pairs, ordered by rule and owner.</returns>
        private static List<RuleOwnerPair> ToPairList(IEnumerable<(long RuleId, int OwnerId)> pairs,
            Dictionary<(long RuleId, int OwnerId), RuleOwner> ruleOwnersByPair, bool withOrigin)
        {
            return pairs.OrderBy(pair => pair.RuleId).ThenBy(pair => pair.OwnerId)
                .Select(pair => new RuleOwnerPair
                {
                    RuleId = pair.RuleId,
                    OwnerId = pair.OwnerId,
                    Created = withOrigin && ruleOwnersByPair.TryGetValue(pair, out RuleOwner? origin) ? origin.Created : null,
                    RuleMetadataId = ruleOwnersByPair.TryGetValue(pair, out RuleOwner? ruleOwner) ? ruleOwner.RuleMetadataId : null
                }).ToList();
        }
    }
}
