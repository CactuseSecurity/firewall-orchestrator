using FWO.Data.Enums;
using FWO.Services;
using FWO.Ui.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FWO.Test
{
    /// <summary>
    /// Covers how a recorded rule owner mapping run is presented: which of the stored runs is shown, and
    /// how its result has to be read - only a real deviation may be shown as a problem.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleOwnerMappingRunHandlerTest
    {
        private static RuleOwnerMappingRun Run(long controlId, int added = 0, int removed = 0,
            bool diffMeaningful = true, bool triggeredByChange = false, int runMinute = 0, int mappingCount = 10)
        {
            return new RuleOwnerMappingRun
            {
                RunTime = new DateTime(2026, 9, 16, 12, runMinute, 0, DateTimeKind.Utc),
                ControlId = controlId,
                MappingCount = mappingCount,
                AddedCount = added,
                RemovedCount = removed,
                Added = Enumerable.Range(1, added).Select(i => new RuleOwnerPair { RuleId = 100 + i, OwnerId = 1 }).ToList(),
                Removed = Enumerable.Range(1, removed).Select(i => new RuleOwnerPair { RuleId = 200 + i, OwnerId = 2, Created = 7 }).ToList(),
                DiffMeaningful = diffMeaningful,
                TriggeredByChange = triggeredByChange
            };
        }

        private static RuleOwnerMappingHistoryReadResult Read(RuleOwnerMappingRunHistoryData history)
        {
            return new RuleOwnerMappingHistoryReadResult { History = history, State = RuleOwnerMappingHistoryReadState.Read };
        }

        private static RuleOwnerMappingHistoryReadResult NotRead(RuleOwnerMappingHistoryReadState state)
        {
            return new RuleOwnerMappingHistoryReadResult { State = state };
        }

        private static RuleOwnerMappingHistoryReadResult History(params RuleOwnerMappingRun[] runsWithFindings)
        {
            return Read(new RuleOwnerMappingRunHistoryData { RunsWithFindings = runsWithFindings.ToList() });
        }

        [Test]
        public void Init_KeepsTheLastCleanRunApartFromTheFindings()
        {
            // the clean run must never be pushed out by newer findings - it answers "last verified correct"
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData
            {
                LastRunWithoutFindings = Run(99),
                RunsWithFindings = [Run(30, added: 1)]
            }));

            Assert.Multiple(() =>
            {
                Assert.That(handler.LastRunWithoutFindings!.ControlId, Is.EqualTo(99));
                Assert.That(handler.Runs, Has.Count.EqualTo(1));
                Assert.That(handler.SelectedRun!.ControlId, Is.EqualTo(30));
            });
        }

        [Test]
        public void SelectedRun_IsNull_WhenNothingWasRecordedYet()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData()));

            Assert.Multiple(() =>
            {
                Assert.That(handler.SelectedRun, Is.Null);
                Assert.That(handler.HasNewer, Is.False);
                Assert.That(handler.HasOlder, Is.False);
                Assert.That(handler.GetSelectedEntries(), Is.Empty);
            });
        }

        [Test]
        public void Init_ShowsTheNewestRun()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(30, added: 1), Run(20, added: 1), Run(10, added: 1)));

            Assert.Multiple(() =>
            {
                Assert.That(handler.SelectedRun!.ControlId, Is.EqualTo(30));
                Assert.That(handler.HasNewer, Is.False, "the newest run has nothing newer");
                Assert.That(handler.HasOlder, Is.True);
            });
        }

        [Test]
        public void SelectOlderAndNewer_StepThroughTheRunsAndStopAtTheEnds()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(30, added: 1), Run(20, added: 1), Run(10, added: 1)));

            handler.SelectOlder();
            handler.SelectOlder();
            handler.SelectOlder();

            Assert.That(handler.SelectedRun!.ControlId, Is.EqualTo(10), "stepping past the oldest run must not wrap around");

            handler.SelectNewer();
            Assert.That(handler.SelectedRun!.ControlId, Is.EqualTo(20));

            handler.SelectNewer();
            handler.SelectNewer();
            Assert.That(handler.SelectedRun!.ControlId, Is.EqualTo(30), "stepping past the newest run must not wrap around");
        }

        [Test]
        public void GetSelectedState_ReportsInSync_WhenNothingChanged()
        {
            // a run without findings is not listed any more, but the state is still what describes it
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(10)));

            Assert.That(handler.GetSelectedState(), Is.EqualTo(RuleOwnerMappingRunState.InSync));
        }

        [Test]
        public void GetSelectedState_ReportsDrift_WhenTheRebuiltStateDiffers()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(10, added: 2, removed: 1)));

            Assert.That(handler.GetSelectedState(), Is.EqualTo(RuleOwnerMappingRunState.Drift));
        }

        [Test]
        public void GetSelectedState_DoesNotReportDrift_WhenImportsWerePending()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(10, added: 5, diffMeaningful: false)));

            Assert.That(handler.GetSelectedState(), Is.EqualTo(RuleOwnerMappingRunState.ImportsPending),
                "an unprocessed backlog explains the difference on its own");
        }

        [Test]
        public void GetSelectedState_DoesNotReportDrift_AfterADeliberateChange()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(10, added: 5, triggeredByChange: true)));

            Assert.That(handler.GetSelectedState(), Is.EqualTo(RuleOwnerMappingRunState.ChangeApplied),
                "a deliberate change produces a different state by design");
        }

        [Test]
        public void GetSelectedState_DoesNotReportDrift_WhenTheSourceMatchedNothing()
        {
            // the source stopped matching and every mapping was removed. The middleware does not call that
            // drift either - it raises its own, more precise alert - so the page must not contradict it
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(10, removed: 3, mappingCount: 0)));

            Assert.That(handler.GetSelectedState(), Is.EqualTo(RuleOwnerMappingRunState.EmptyResult),
                "a source that matched nothing is a configuration problem, not a missed incremental change");
        }

        [Test]
        public void GetSelectedEntries_LabelsAddedAsMissingAndRemovedAsLeftOver()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(10, added: 2, removed: 1)));

            List<RuleOwnerMappingRunEntry> entries = handler.GetSelectedEntries();

            Assert.Multiple(() =>
            {
                Assert.That(entries, Has.Count.EqualTo(3));
                Assert.That(entries.Where(entry => entry.Finding == RuleOwnerMappingFinding.Missing).Select(entry => entry.RuleId),
                    Is.EquivalentTo(new List<long> { 101, 102 }), "added pairs were never created by the incremental mapping");
                Assert.That(entries.Where(entry => entry.Finding == RuleOwnerMappingFinding.Superfluous).Select(entry => entry.RuleId),
                    Is.EquivalentTo(new List<long> { 201 }), "removed pairs were left behind by the incremental mapping");
            });
        }

        [Test]
        public void GetSelectedEntries_MakeEveryRowFindableInRuleOwner()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(42, added: 1, removed: 1)));

            RuleOwnerMappingRunEntry missing = handler.GetSelectedEntries().Single(entry => entry.Finding == RuleOwnerMappingFinding.Missing);
            RuleOwnerMappingRunEntry leftOver = handler.GetSelectedEntries().Single(entry => entry.Finding == RuleOwnerMappingFinding.Superfluous);

            Assert.Multiple(() =>
            {
                Assert.That(missing.Created, Is.EqualTo(42), "the run established the missing mapping");
                Assert.That(missing.Removed, Is.Null, "it is active afterwards");
                Assert.That(leftOver.Created, Is.EqualTo(7), "the left over mapping came from an older import");
                Assert.That(leftOver.Removed, Is.EqualTo(42), "the run removed it");
            });
        }

        [Test]
        public void GetSelectedEntries_FallBackToTheRunsControlId_WhenNoOriginWasRecorded()
        {
            // entries written before the origin was recorded carry no created value
            RuleOwnerMappingRun run = Run(42, removed: 1);
            run.Removed[0].Created = null;

            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(run));

            Assert.That(handler.GetSelectedEntries().Single().Created, Is.EqualTo(42));
        }

        [Test]
        public void GetStateStyle_MarksWhatNeedsAttentionAsCritical()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleOwnerMappingRunHandler.GetStateStyle(RuleOwnerMappingRunState.Drift), Is.EqualTo("danger"));
                Assert.That(RuleOwnerMappingRunHandler.GetStateStyle(RuleOwnerMappingRunState.EmptyResult), Is.EqualTo("danger"),
                    "every mapping was removed, which needs the same attention as drift");
                Assert.That(RuleOwnerMappingRunHandler.GetStateStyle(RuleOwnerMappingRunState.InSync), Is.EqualTo("success"));
                Assert.That(RuleOwnerMappingRunHandler.GetStateStyle(RuleOwnerMappingRunState.ImportsPending), Is.EqualTo("warning"));
                Assert.That(RuleOwnerMappingRunHandler.GetStateStyle(RuleOwnerMappingRunState.ChangeApplied), Is.EqualTo("secondary"));
            });
        }

        [Test]
        public void GetFindingStyle_MarksThePairsOnlyOnADeviation()
        {
            // the label on the row reads neutrally in every state but Drift, so the badge has to as well -
            // a red badge under "newly added" tells the operator the opposite of what the text says
            Assert.Multiple(() =>
            {
                Assert.That(RuleOwnerMappingRunHandler.GetFindingStyle(RuleOwnerMappingRunState.Drift,
                    RuleOwnerMappingFinding.Missing), Is.EqualTo("danger"));
                Assert.That(RuleOwnerMappingRunHandler.GetFindingStyle(RuleOwnerMappingRunState.Drift,
                    RuleOwnerMappingFinding.Superfluous), Is.EqualTo("warning"));

                Assert.That(RuleOwnerMappingRunHandler.GetFindingStyle(RuleOwnerMappingRunState.ChangeApplied,
                    RuleOwnerMappingFinding.Missing), Is.EqualTo("secondary"),
                    "the change was intended, so its pairs must not be marked as a problem");
                Assert.That(RuleOwnerMappingRunHandler.GetFindingStyle(RuleOwnerMappingRunState.ImportsPending,
                    RuleOwnerMappingFinding.Missing), Is.EqualTo("secondary"),
                    "the backlog explains the difference, so the pairs prove nothing");
                Assert.That(RuleOwnerMappingRunHandler.GetFindingStyle(RuleOwnerMappingRunState.EmptyResult,
                    RuleOwnerMappingFinding.Superfluous), Is.EqualTo("secondary"),
                    "the state badge already carries this problem, and it is not one of the pairs");
                Assert.That(RuleOwnerMappingRunHandler.GetFindingStyle(RuleOwnerMappingRunState.InSync,
                    RuleOwnerMappingFinding.Missing), Is.EqualTo("secondary"));
            });
        }

        [Test]
        public void CurrentState_IsInSync_WhenTheNewestCheckFoundNothing()
        {
            // the listed runs are then already dealt with - without this the page would read as if the
            // findings were current, because the history never shows the clean runs
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData
            {
                LastRunWithoutFindings = Run(50, runMinute: 37),
                RunsWithFindings = [Run(45, added: 13, runMinute: 36)]
            }));

            Assert.Multiple(() =>
            {
                Assert.That(handler.CurrentState, Is.EqualTo(RuleOwnerMappingRunState.InSync));
                Assert.That(handler.SelectedRun!.AddedCount, Is.EqualTo(13), "the listed run stays readable as history");
            });
        }

        [Test]
        public void CurrentState_IsDrift_WhenAFindingFollowedTheLastCleanCheck()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData
            {
                LastRunWithoutFindings = Run(50, runMinute: 30),
                RunsWithFindings = [Run(55, added: 2, runMinute: 40)]
            }));

            Assert.Multiple(() =>
            {
                Assert.That(handler.CurrentState, Is.EqualTo(RuleOwnerMappingRunState.Drift));
                Assert.That(handler.GetSelectedState(), Is.EqualTo(RuleOwnerMappingRunState.Drift));
            });
        }

        [Test]
        public void CurrentState_IsDrift_WhenNothingWasEverVerified()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(45, added: 1)));

            Assert.That(handler.CurrentState, Is.EqualTo(RuleOwnerMappingRunState.Drift));
        }

        [Test]
        public void CurrentState_IsChangeApplied_WhenTheNewestRunFollowedADeliberateChange()
        {
            // a deliberate change makes the result differ on purpose, so the banner must not read as a problem
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData
            {
                LastRunWithoutFindings = Run(50, runMinute: 30),
                RunsWithFindings = [Run(55, added: 13, triggeredByChange: true, runMinute: 40)]
            }));

            Assert.That(handler.CurrentState, Is.EqualTo(RuleOwnerMappingRunState.ChangeApplied));
        }

        [Test]
        public void CurrentState_IsImportsPending_WhenTheNewestRunCouldNotJudge()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(55, added: 2, diffMeaningful: false, runMinute: 40)));

            Assert.That(handler.CurrentState, Is.EqualTo(RuleOwnerMappingRunState.ImportsPending));
        }

        [Test]
        public void CurrentState_IsNull_WhenNothingWasRecordedAtAll()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData()));

            Assert.That(handler.CurrentState, Is.Null);
        }

        [Test]
        public void GetSelectedState_ReportsInSync_WhenAChangeTurnedOutToHaveNoEffect()
        {
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(History(Run(10, triggeredByChange: true)));

            Assert.That(handler.GetSelectedState(), Is.EqualTo(RuleOwnerMappingRunState.InSync),
                "no difference is the strongest statement, whatever triggered the run");
        }

        [Test]
        public void GetSelectedState_AnswersNothing_WhenNoRunWasRecorded()
        {
            // "nothing was recorded" is not a state of a run. Answering it with one - ImportsPending renders
            // as a warning - would report a problem that nobody observed
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData()));

            Assert.That(handler.GetSelectedState(), Is.Null);
        }

        [Test]
        public void Init_ReportsTheHistoryAsUnreadable_WhenItCouldNotBeRead()
        {
            // an unreadable entry is never written over, so nothing is recorded meanwhile. Rendered as an
            // empty history the page would keep answering "no deviation was ever found", which is the one
            // answer it must not give
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(NotRead(RuleOwnerMappingHistoryReadState.NotDecoded));

            Assert.Multiple(() =>
            {
                Assert.That(handler.HistoryUnreadable, Is.True);
                Assert.That(handler.Runs, Is.Empty);
                Assert.That(handler.LastRunWithoutFindings, Is.Null);
                Assert.That(handler.CurrentState, Is.Null, "no run was read, so nothing can be said about the current state");
                Assert.That(handler.SelectedRun, Is.Null);
            });
        }

        [Test]
        public void UnreadableHistoryText_AsksForAReset_OnlyWhenTheStoredValueIsTheProblem()
        {
            // a fetch that failed leaves a healthy entry behind, so telling the user to reset it would
            // destroy the recorded history over a failure that may already be gone
            RuleOwnerMappingRunHandler handler = new();

            handler.Init(NotRead(RuleOwnerMappingHistoryReadState.NotFetched));
            string unfetched = handler.UnreadableHistoryText;

            handler.Init(NotRead(RuleOwnerMappingHistoryReadState.NotDecoded));

            Assert.Multiple(() =>
            {
                Assert.That(unfetched, Is.EqualTo(RuleOwnerMappingRunHandler.kUnfetchedHistoryText));
                Assert.That(handler.UnreadableHistoryText, Is.EqualTo(RuleOwnerMappingRunHandler.kUndecodedHistoryText));
                Assert.That(handler.HistoryUnreadable, Is.True, "both are unreadable, they only differ in what to do about it");
            });
        }

        [Test]
        public void Init_DoesNotReportTheHistoryAsUnreadable_WhenNothingWasRecordedYet()
        {
            // the state the page has to tell apart from the one above: readable, and empty because nothing
            // has run yet
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(Read(new RuleOwnerMappingRunHistoryData()));

            Assert.Multiple(() =>
            {
                Assert.That(handler.HistoryUnreadable, Is.False);
                Assert.That(handler.Runs, Is.Empty);
            });
        }

        [Test]
        public void Init_ClearsTheUnreadableFlag_WhenAReadSucceedsAfterAFailedOne()
        {
            // the case a failed fetch is expected to end in: the page is reloaded and the entry comes back,
            // so the notice has to go
            RuleOwnerMappingRunHandler handler = new();
            handler.Init(NotRead(RuleOwnerMappingHistoryReadState.NotFetched));

            handler.Init(History(Run(10, added: 1)));

            Assert.Multiple(() =>
            {
                Assert.That(handler.HistoryUnreadable, Is.False);
                Assert.That(handler.SelectedRun!.ControlId, Is.EqualTo(10));
            });
        }
    }
}
