using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Enums;
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

        /// <summary>
        /// When a change note was written that this run dropped as expired instead of applying it, or
        /// <see langword="null"/> when none was dropped. The run is still judged on its own - that is what
        /// the expiry is for - but the difference it found may just as well be the effect of that never
        /// applied setting, so whoever is told about the difference has to be told about the note as well.
        /// </summary>
        [JsonPropertyName("droppedChangeRecordedAt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? DroppedChangeRecordedAt { get; set; }

        /// <summary>
        /// True when the run found no difference at all. Not stored - it is derived from the counts, and the
        /// config entry is kept to what cannot be recomputed.
        /// </summary>
        [JsonIgnore]
        public bool HasNoFindings => AddedCount + RemovedCount == 0;

        /// <summary>
        /// How this run has to be read. The single place that question is decided: the middleware raises its
        /// drift alert off this, and the monitoring page colours and labels the run off this. Written out
        /// separately per caller before, which let the alert and the page contradict each other about the
        /// same run.
        /// <para>
        /// Whether a run is kept in the history is a different question and is deliberately not answered
        /// here - see <see cref="RuleOwnerMappingRunHistory"/>, which asks <see cref="HasNoFindings"/> and
        /// <see cref="TriggeredByChange"/> directly. A deliberate change without effect reads as
        /// <see cref="RuleOwnerMappingRunState.InSync"/> and is kept all the same, so the two rules cannot
        /// be folded into one.
        /// </para>
        /// <para>
        /// Order matters. A backlog explains any difference on its own, so it is answered first; no
        /// difference is the strongest statement there is, whatever triggered the run; a deliberate change is
        /// expected to differ; and a source that matched nothing is a configuration problem with its own
        /// alert rather than something the incremental mapping missed.
        /// </para>
        /// </summary>
        [JsonIgnore]
        public RuleOwnerMappingRunState State
        {
            get
            {
                if (!DiffMeaningful)
                {
                    return RuleOwnerMappingRunState.ImportsPending;
                }
                if (HasNoFindings)
                {
                    return RuleOwnerMappingRunState.InSync;
                }
                if (TriggeredByChange)
                {
                    return RuleOwnerMappingRunState.ChangeApplied;
                }
                return MappingCount == 0 ? RuleOwnerMappingRunState.EmptyResult : RuleOwnerMappingRunState.Drift;
            }
        }
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

        /// <summary>
        /// Mapping-relevant settings that were saved but whose full reinitialize has not completed yet. The
        /// configuration is written before the rebuild runs, so a rebuild that fails leaves the new setting
        /// live while the stored mappings still follow the old one. Whichever rebuild comes next - the manual
        /// recalculation, the backlog fallback or the repeated-failure repair, none of which knows about the
        /// save - then produces that difference on purpose and must not report it as drift.
        /// <para>
        /// Kept here rather than in the editor: the editor state lives in one browser circuit, while the
        /// rebuild that finally applies the change may run in the middleware.
        /// </para>
        /// <para>
        /// Bounded by <see cref="RuleOwnerMappingRunHistory.kPendingChangesMaxAge"/>: a note nobody ever
        /// retried must not explain away a rebuild that runs much later for an unrelated reason.
        /// </para>
        /// </summary>
        [JsonPropertyName("pendingChanges")]
        public List<RuleOwnerMappingChange> PendingChanges { get; set; } = [];

        /// <summary>
        /// When <see cref="PendingChanges"/> was last written. The note has to outlive the rebuild it was
        /// written for, because that rebuild may fail - but not indefinitely, see
        /// <see cref="RuleOwnerMappingRunHistory.kPendingChangesMaxAge"/>. Default while no note is stored.
        /// </summary>
        [JsonPropertyName("pendingChangesRecordedAt"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime PendingChangesRecordedAt { get; set; }
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

        /// <summary>
        /// How long a pending change is taken over by the next stored run. The rebuild a save triggers may
        /// fail, so the note must survive it - the whole point of recording it. It must not survive forever
        /// though: consumed by a rebuild that runs much later for an unrelated reason, it would mark that run
        /// as intended and swallow the drift alert this history exists to raise. Generous enough that a
        /// retried rebuild days later still counts as the one applying the change.
        /// </summary>
        public static readonly TimeSpan kPendingChangesMaxAge = TimeSpan.FromDays(7);
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
        /// Prepends the run to the stored history and drops everything beyond the newest entries. A change
        /// that was saved but not yet applied is taken over onto the run first, see
        /// <see cref="RuleOwnerMappingRunHistoryData.PendingChanges"/>.
        /// </summary>
        /// <param name="run">Run to store.</param>
        /// <returns>
        /// The stored run. It differs from the one passed in when a pending change was taken over, so the
        /// caller has to decide about drift on the returned run rather than on its own.
        /// </returns>
        public async Task<RuleOwnerMappingRun> Store(RuleOwnerMappingRun run)
        {
            try
            {
                RuleOwnerMappingRunHistoryData history = await Load();
                TakeOverPendingChanges(run, history);

                // a run without findings updates "last verified correct", so a repeated rebuild cannot push
                // anything out of the limited history - but only when it could judge at all: with imports
                // still pending it proves nothing and must not claim a verification
                if (run.HasNoFindings && run.DiffMeaningful)
                {
                    history.LastRunWithoutFindings = run;
                }

                if (BelongsIntoRunsWithFindings(run))
                {
                    history.RunsWithFindings.Insert(0, run);
                    history.RunsWithFindings = history.RunsWithFindings.Take(kMaxRuns).ToList();
                }

                history.PendingChanges = [];
                history.PendingChangesRecordedAt = default;
                await Save(history);
            }
            catch (Exception ex)
            {
                // the history is a diagnostic aid, it must never break the mapping itself
                Log.WriteError(kLogMessageTitle, "Error while storing the rule_owner mapping run history.", ex);
            }
            return run;
        }

        /// <summary>
        /// Takes a change that was saved but not yet applied over onto the run that applies it now, so its
        /// difference is read as the intended result instead of as drift of the incremental mapping.
        /// </summary>
        /// <param name="run">Run being stored, adjusted in place.</param>
        /// <param name="history">Stored history holding the pending changes.</param>
        private static void TakeOverPendingChanges(RuleOwnerMappingRun run, RuleOwnerMappingRunHistoryData history)
        {
            if (history.PendingChanges.Count == 0)
            {
                return;
            }

            // nothing else ties the note to the rebuild it was written for: it is cleared by a stored run, so
            // after a rebuild that failed it waits for whichever comes next. Beyond kPendingChangesMaxAge that
            // is no longer plausibly the rebuild applying the change, and taking it over would mark an
            // unrelated run as intended - silencing exactly the drift alert this history exists to raise.
            // The caller clears the note either way, so an expired one is dropped rather than carried on
            if (DateTime.UtcNow - history.PendingChangesRecordedAt > kPendingChangesMaxAge)
            {
                // kept on the run rather than only in this log line: the run is judged on its own from here
                // on, so whatever difference it reports has to carry that a never applied change may explain it
                run.DroppedChangeRecordedAt = history.PendingChangesRecordedAt;
                Log.WriteWarning(kLogMessageTitle, "Dropping the rule_owner mapping change note of " +
                    $"{history.PendingChangesRecordedAt:u}: no rebuild applied it within {kPendingChangesMaxAge.TotalDays} days, " +
                    "so a difference of this run is judged on its own.");
                return;
            }

            run.TriggeredByChange = true;
            run.Changes = MergeChanges(run.Changes, history.PendingChanges);

            // same reasoning as in BuildRun: after a source switch every mapping differs, so the individual
            // pairs say nothing and only the counts are kept
            if (run.Changes.Exists(change => change.Setting == RuleOwnerMappingChangeSetting.kSource))
            {
                run.Added = [];
                run.Removed = [];
                run.PairListsTruncated = false;
            }
        }

        /// <summary>
        /// Merges change notes about the same setting into one: the value it started from and the value it
        /// ended at. Saving twice before the rebuild succeeds would otherwise report the intermediate value
        /// as the starting point.
        /// </summary>
        /// <param name="earlier">Change notes recorded first.</param>
        /// <param name="later">Change notes recorded afterwards.</param>
        /// <returns>One note per setting, ordered as first seen.</returns>
        public static List<RuleOwnerMappingChange> MergeChanges(List<RuleOwnerMappingChange> earlier, List<RuleOwnerMappingChange> later)
        {
            List<RuleOwnerMappingChange> merged = [];
            foreach (RuleOwnerMappingChange change in earlier.Concat(later))
            {
                RuleOwnerMappingChange? known = merged.Find(entry => entry.Setting == change.Setting);
                if (known == null)
                {
                    merged.Add(new RuleOwnerMappingChange { Setting = change.Setting, From = change.From, To = change.To });
                    continue;
                }
                known.To = change.To;
            }
            return merged.Where(change => change.From != change.To).ToList();
        }

        /// <summary>
        /// Remembers a mapping-relevant setting that was saved, before the rebuild applying it is triggered.
        /// The next stored run takes it over, so a rebuild that only happens after a failed attempt is still
        /// recognized as following a deliberate change. Cleared by <see cref="Store"/>, and taken over only
        /// within <see cref="kPendingChangesMaxAge"/> of this call.
        /// </summary>
        /// <param name="changes">Settings that were changed, empty to record nothing.</param>
        public async Task RecordPendingChanges(List<RuleOwnerMappingChange> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }

            try
            {
                RuleOwnerMappingRunHistoryData history = await Load();
                history.PendingChanges = MergeChanges(history.PendingChanges, changes);

                // the whole note is stamped, not the single change: every setting in it is still unapplied,
                // so this save confirms the older entries as much as the one it adds
                history.PendingChangesRecordedAt = DateTime.UtcNow;
                await Save(history);
            }
            catch (Exception ex)
            {
                // without the note the next rebuild reports the intended change as drift, which is a wrong
                // alert but not a wrong mapping - it must not stop the save from triggering the rebuild
                Log.WriteError(kLogMessageTitle, "Error while recording the pending rule_owner mapping changes.", ex);
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
                // same conditions as in Store, through the same predicates: a run that found nothing while
                // imports were still pending proves nothing and must not be migrated into "last verified
                // correct", and a run kept only because it followed a deliberate change must survive here too
                LastRunWithoutFindings = storedRuns.FirstOrDefault(run => run.HasNoFindings && run.DiffMeaningful),
                RunsWithFindings = storedRuns.Where(BelongsIntoRunsWithFindings).Take(kMaxRuns).ToList()
            };
        }

        /// <summary>
        /// Decides whether a run is kept in the list of runs with findings. A deliberate change is kept even
        /// when it changed nothing: that it had no effect is exactly what somebody who just edited the
        /// configuration needs to see.
        /// <para>
        /// Shared by <see cref="Store"/> and <see cref="Deserialize"/> on purpose - the rule was written out
        /// twice before and the two copies drifted apart, so a run kept on write was dropped again on read.
        /// </para>
        /// </summary>
        /// <param name="run">Run to judge.</param>
        /// <returns>True if the run belongs into the kept list.</returns>
        private static bool BelongsIntoRunsWithFindings(RuleOwnerMappingRun run)
        {
            return !run.HasNoFindings || run.TriggeredByChange;
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
