using FWO.Data.Enums;
using FWO.Services;

namespace FWO.Ui.Services
{
    /// <summary>
    /// What a recorded difference says about a single rule-owner pair.
    /// </summary>
    public enum RuleOwnerMappingFinding
    {
        /// <summary>The full reinitialize established the mapping, so the incremental mapping never created it.</summary>
        Missing,

        /// <summary>The full reinitialize does not produce the mapping, so the incremental mapping left it behind.</summary>
        Superfluous
    }

    /// <summary>
    /// One rule-owner pair of a recorded run together with what it says about the incremental mapping.
    /// </summary>
    public class RuleOwnerMappingRunEntry
    {
        /// <summary>Rule the mapping belongs to.</summary>
        public long RuleId { get; init; }

        /// <summary>Owner the rule was mapped to.</summary>
        public int OwnerId { get; init; }

        /// <summary>Rule metadata the mapping belongs to, which survives new versions of the rule.</summary>
        public long? RuleMetadataId { get; init; }

        /// <summary>What the difference means for this pair.</summary>
        public RuleOwnerMappingFinding Finding { get; init; }

        /// <summary>
        /// Import that created the rule_owner row. For a missing pair this is the run itself, for a left over
        /// pair the import it had originally been established by.
        /// </summary>
        public long Created { get; init; }

        /// <summary>
        /// Import that removed the rule_owner row, or <see langword="null"/> while the mapping is active.
        /// A left over pair is removed by the run itself, a missing pair is active afterwards.
        /// </summary>
        public long? Removed { get; init; }
    }

    /// <summary>
    /// Editor state of the rule owner mapping run history: which of the stored runs is shown and how its
    /// result has to be read.
    /// </summary>
    public class RuleOwnerMappingRunHandler
    {
        /// <summary>Text key shown while no full reinitialize has been recorded yet.</summary>
        public const string kNoHistoryText = "U7551";

        /// <summary>Recorded runs that found a difference, newest first.</summary>
        public List<RuleOwnerMappingRun> Runs { get; private set; } = [];

        /// <summary>
        /// Most recent run that found no difference. Kept apart from <see cref="Runs"/> so it can never be
        /// pushed out by newer findings - it is the answer to "when was the mapping last verified correct".
        /// </summary>
        public RuleOwnerMappingRun? LastRunWithoutFindings { get; private set; }

        /// <summary>Position of the shown run, 0 being the newest.</summary>
        public int SelectedIndex { get; private set; }

        /// <summary>Run currently shown, or <see langword="null"/> when nothing was recorded yet.</summary>
        public RuleOwnerMappingRun? SelectedRun => SelectedIndex < Runs.Count ? Runs[SelectedIndex] : null;

        /// <summary>
        /// How the mapping stands right now, taken from whichever run happened last. The history below only
        /// holds runs that found something and therefore cannot answer this - which is the question the page
        /// is opened for. Null while nothing was recorded at all.
        /// </summary>
        public RuleOwnerMappingRunState? CurrentState
        {
            get
            {
                if (Runs.Count == 0)
                {
                    return LastRunWithoutFindings == null ? null : RuleOwnerMappingRunState.InSync;
                }
                return LastRunWithoutFindings != null && LastRunWithoutFindings.RunTime >= Runs[0].RunTime
                    ? RuleOwnerMappingRunState.InSync
                    : Runs[0].State;
            }
        }

        /// <summary>True when a newer run than the shown one exists.</summary>
        public bool HasNewer => SelectedIndex > 0;

        /// <summary>True when an older run than the shown one exists.</summary>
        public bool HasOlder => SelectedIndex + 1 < Runs.Count;

        /// <summary>
        /// Takes over the recorded history and shows the newest run that found a difference.
        /// </summary>
        /// <param name="history">History as it is stored.</param>
        public void Init(RuleOwnerMappingRunHistoryData history)
        {
            Runs = history.RunsWithFindings;
            LastRunWithoutFindings = history.LastRunWithoutFindings;
            SelectedIndex = 0;
        }

        /// <summary>Shows the next newer run, if there is one.</summary>
        public void SelectNewer()
        {
            if (HasNewer)
            {
                SelectedIndex--;
            }
        }

        /// <summary>Shows the next older run, if there is one.</summary>
        public void SelectOlder()
        {
            if (HasOlder)
            {
                SelectedIndex++;
            }
        }

        /// <summary>
        /// Lists the pairs of the shown run, missing ones first, each with what it says about the
        /// incremental mapping.
        /// </summary>
        /// <returns>The listed pairs, empty when the run found no difference.</returns>
        public List<RuleOwnerMappingRunEntry> GetSelectedEntries()
        {
            RuleOwnerMappingRun? run = SelectedRun;
            if (run == null)
            {
                return [];
            }

            // the recorded pairs are the difference itself. rule_owner cannot be filtered down to it: the run
            // stamps its control id on every mapping it rebuilt and on every one it replaced, so that column
            // identifies the run, not what actually changed
            List<RuleOwnerMappingRunEntry> entries = run.Added
                .Select(pair => new RuleOwnerMappingRunEntry
                {
                    RuleId = pair.RuleId,
                    OwnerId = pair.OwnerId,
                    RuleMetadataId = pair.RuleMetadataId,
                    Finding = RuleOwnerMappingFinding.Missing,
                    Created = pair.Created ?? run.ControlId,
                    Removed = null
                })
                .ToList();

            entries.AddRange(run.Removed
                .Select(pair => new RuleOwnerMappingRunEntry
                {
                    RuleId = pair.RuleId,
                    OwnerId = pair.OwnerId,
                    RuleMetadataId = pair.RuleMetadataId,
                    Finding = RuleOwnerMappingFinding.Superfluous,
                    Created = pair.Created ?? run.ControlId,
                    Removed = run.ControlId
                }));

            return entries;
        }

        /// <summary>
        /// Decides how the result of the shown run has to be read, by asking the run itself - the same
        /// property the middleware raises its drift alert off, so page and alert cannot disagree. A pending
        /// backlog, a configuration change and a source that matched nothing each explain a difference on
        /// their own, so none of them is reported as drift.
        /// </summary>
        /// <returns>
        /// The state of the shown run, or <see langword="null"/> when no run is shown at all. "Nothing was
        /// recorded" is not a state of a run and must not be answered with one, least of all with a state
        /// that renders as a warning.
        /// </returns>
        public RuleOwnerMappingRunState? GetSelectedState()
        {
            return SelectedRun?.State;
        }

        /// <summary>
        /// Resolves the bootstrap context of a state, so what needs attention reads at a glance.
        /// </summary>
        /// <param name="state">State to display.</param>
        /// <returns>The bootstrap context name.</returns>
        public static string GetStateStyle(RuleOwnerMappingRunState state)
        {
            return state switch
            {
                RuleOwnerMappingRunState.InSync => "success",
                RuleOwnerMappingRunState.Drift => "danger",
                // every mapping was removed because the source matched nothing, which needs the same
                // attention as drift even though it is a different problem
                RuleOwnerMappingRunState.EmptyResult => "danger",
                RuleOwnerMappingRunState.ImportsPending => "warning",
                _ => "secondary"
            };
        }
    }
}
