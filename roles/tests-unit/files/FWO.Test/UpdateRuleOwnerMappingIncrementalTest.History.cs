using FWO.Basics;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Services;
using FWO.Services.EventMediator.Events;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Test
{
    /// <summary>
    /// Regression tests for the run history config entry itself: that an entry which could not be read is
    /// never written over, and that the note of a saved configuration change is dropped by every rebuild
    /// that completes - including the two that complete without recording a run.
    /// </summary>
    public partial class UpdateRuleOwnerMappingIncrementalTest
    {
        /// <summary>Import remembered as failed in the seeded entry, so a test can report the same one again.</summary>
        private const long kSeededFailedImportId = 77;

        /// <summary>
        /// A stored entry carrying all three kinds of state the history holds, so a test can tell a
        /// preserved entry from one that was replaced by an empty document.
        /// </summary>
        /// <returns>The serialized history.</returns>
        private static string SeededHistoryJson()
        {
            RuleOwnerMappingRunHistoryData stored = new()
            {
                LastRunWithoutFindings = new RuleOwnerMappingRun { ControlId = 11, MappingCount = 3, DiffMeaningful = true },
                RunsWithFindings = [new RuleOwnerMappingRun { ControlId = 12, AddedCount = 2, DiffMeaningful = true }],
                FailedImports = [kSeededFailedImportId],
                PendingChanges = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kMarker, From = "FWOC", To = "APP" }],
                PendingChangesRecordedAt = DateTime.UtcNow
            };
            return JsonSerializer.Serialize(stored);
        }

        [Test]
        public async Task RunAsync_ShouldKeepTheStoredHistory_WhenItCouldNotBeReadBeforeStoringARun()
        {
            // Save replaces the whole entry, so a read failure answered with an empty history would be saved
            // over the recorded runs, the failed import record and the change note - losing all of it
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);
            string storedBefore = SeededHistoryJson();
            apiConnection.SeedStoredHistoryJson(storedBefore);
            apiConnection.FailHistoryRead = true;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "the mapping itself succeeded, the history is only a diagnostic aid");
                Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo(storedBefore),
                    "an entry that could not be read must not be written over");
            });
        }

        [Test]
        public async Task RunAsync_ShouldKeepTheStoredHistory_WhenAFailedImportCannotBeRecorded()
        {
            // the path this matters most on: the imports being reported have just failed, often for the very
            // reason the read of the history fails too
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.AddRuleChange(1, ChangelogActionType.INSERT, kRuleId);
            apiConnection.FailRuleChangeLookupForImport = 1;
            string storedBefore = SeededHistoryJson();
            apiConnection.SeedStoredHistoryJson(storedBefore);
            apiConnection.FailHistoryRead = true;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "the failed import still has to be reported as a failure");
                Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo(storedBefore),
                    "the remembered failures and the change note must survive a read that failed");
            });
        }

        [Test]
        public async Task RecordPendingChanges_ShouldKeepTheStoredHistory_WhenItCouldNotBeRead()
        {
            RuleOwnerMappingFake apiConnection = new();
            string storedBefore = SeededHistoryJson();
            apiConnection.SeedStoredHistoryJson(storedBefore);
            apiConnection.FailHistoryRead = true;

            List<RuleOwnerMappingChange> savedChanges = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kSource, From = "IpBased", To = "NameField" }];
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(savedChanges);

            Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo(storedBefore),
                "not recording the note costs a wrong drift alert, writing over the entry costs the entry");
        }

        [Test]
        public async Task RecordFailedImports_ShouldReportNoRepeat_WhenTheHistoryCouldNotBeFetched()
        {
            // reporting "not seen before" delays the repair by one run; the opposite would rebuild
            // everything on a single transient failure
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson(SeededHistoryJson());
            apiConnection.FailHistoryRead = true;

            List<long> failedImports = [kSeededFailedImportId];
            RuleOwnerMappingFailedImportsResult failures = await new RuleOwnerMappingRunHistory(apiConnection).RecordFailedImports(failedImports);

            Assert.Multiple(() =>
            {
                Assert.That(failures.FailedBefore, Is.False, "without the stored state a repeat cannot be asserted");
                Assert.That(failures.RepairBlockedUntilReset, Is.False,
                    "the stored value was never reached, so the next read may well succeed and repair one run late");
                Assert.That(failures.WriteFailed, Is.False,
                    "no save was attempted, and this case keeps its own answer instead of being reported as a failed write");
            });
        }

        [Test]
        public async Task RecordFailedImports_ShouldReportTheRepairAsBlocked_WhenTheStoredValueCannotBeDecoded()
        {
            // nothing saves over an undecodable entry, so every later run would be answered "not seen
            // before" too and the repair would never run at all - that is not a delay, it is an outage
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson("{\"runsWithFindings\": [ truncated");

            List<long> failedImports = [kSeededFailedImportId];
            RuleOwnerMappingFailedImportsResult failures = await new RuleOwnerMappingRunHistory(apiConnection).RecordFailedImports(failedImports);

            Assert.Multiple(() =>
            {
                Assert.That(failures.FailedBefore, Is.False);
                Assert.That(failures.RepairBlockedUntilReset, Is.True);
                Assert.That(failures.WriteFailed, Is.False, "the entry is deliberately not written over, which is not a write that failed");
                Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo("{\"runsWithFindings\": [ truncated"),
                    "reporting the problem must not repair it by overwriting the entry");
            });
        }

        [Test]
        public async Task RecordFailedImports_ShouldStillReportTheRepeat_WhenTheSaveFailed()
        {
            // the read succeeded, so the repeat is known. Dropping it because writing it back failed would
            // switch the repair off for as long as the writes keep failing, with nothing reporting that
            RuleOwnerMappingFake apiConnection = new();
            string storedBefore = SeededHistoryJson();
            apiConnection.SeedStoredHistoryJson(storedBefore);
            apiConnection.FailHistoryWrite = true;

            List<long> failedImports = [kSeededFailedImportId];
            RuleOwnerMappingFailedImportsResult failures = await new RuleOwnerMappingRunHistory(apiConnection).RecordFailedImports(failedImports);

            Assert.Multiple(() =>
            {
                Assert.That(failures.FailedBefore, Is.True, "the stored entry named this import and the failing save does not unsay it");
                Assert.That(failures.RepairBlockedUntilReset, Is.False, "the entry is readable, so the repair is not waiting on a reset");
                Assert.That(failures.WriteFailed, Is.True, "the save failed, and the caller has to know this run is not remembered");
                Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo(storedBefore),
                    "the entry keeps the ids it had, which is what lets the next run read the same repeat");
            });
        }

        [Test]
        public async Task RecordFailedImports_ShouldReportTheFailedWrite_WhenNothingWasRememberedBefore()
        {
            // the entry holds nothing about this import, so the failing save leaves no repeat for any later
            // run to read. Answering "not seen before" alone would look like an ordinary first failure
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.FailHistoryWrite = true;

            List<long> failedImports = [kSeededFailedImportId];
            RuleOwnerMappingFailedImportsResult failures = await new RuleOwnerMappingRunHistory(apiConnection).RecordFailedImports(failedImports);

            Assert.Multiple(() =>
            {
                Assert.That(failures.WriteFailed, Is.True, "the save failed, so this failure never reaches the next run");
                Assert.That(failures.FailedBefore, Is.False, "nothing was stored about this import before");
                Assert.That(failures.RepairBlockedUntilReset, Is.False, "the entry is readable, so it does not have to be reset");
            });
        }

        [Test]
        public async Task Load_ShouldReportAFailedFetch_WithoutTheHistory()
        {
            // an empty history would be indistinguishable from "nothing recorded yet" - and because no
            // writer saves over an entry it could not read, nothing is recorded meanwhile
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson(SeededHistoryJson());
            apiConnection.FailHistoryRead = true;

            RuleOwnerMappingHistoryReadResult readResult = await new RuleOwnerMappingRunHistory(apiConnection).Load();

            Assert.Multiple(() =>
            {
                Assert.That(readResult.History, Is.Null);
                Assert.That(readResult.State, Is.EqualTo(RuleOwnerMappingHistoryReadState.NotFetched),
                    "the stored value was never reached, so it is untouched and must not be reported as damaged");
            });
        }

        [Test]
        public async Task Load_ShouldReportAnUndecodableValue_ApartFromAFailedFetch()
        {
            // the two are repaired by opposite means: this one lasts until the entry is reset, a failed
            // fetch may be gone on the next read - so only this one may ask a user to reset the entry
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson("{\"runsWithFindings\": [ truncated");

            RuleOwnerMappingHistoryReadResult readResult = await new RuleOwnerMappingRunHistory(apiConnection).Load();

            Assert.Multiple(() =>
            {
                Assert.That(readResult.History, Is.Null);
                Assert.That(readResult.State, Is.EqualTo(RuleOwnerMappingHistoryReadState.NotDecoded));
                Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo("{\"runsWithFindings\": [ truncated"),
                    "a read must not repair the entry by overwriting it");
            });
        }

        [Test]
        public async Task Load_ShouldAnswerAnEmptyHistory_WhenNothingIsStoredYet()
        {
            // the counterpart of the two tests above: nothing stored is a readable answer and stays one
            RuleOwnerMappingFake apiConnection = new();

            RuleOwnerMappingHistoryReadResult readResult = await new RuleOwnerMappingRunHistory(apiConnection).Load();

            Assert.Multiple(() =>
            {
                Assert.That(readResult.State, Is.EqualTo(RuleOwnerMappingHistoryReadState.Read));
                Assert.That(readResult.History, Is.Not.Null);
                Assert.That(readResult.History!.RunsWithFindings, Is.Empty);
                Assert.That(readResult.History.LastRunWithoutFindings, Is.Null);
            });
        }

        [Test]
        public async Task Store_ShouldNotWriteOverTheEntry_WhenItsValueCouldNotBeDecoded()
        {
            // the F36 protection has to hold for the second read failure too: the entry that cannot be
            // decoded is exactly the one a save would replace with a fresh, empty document
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson("not json at all");

            RuleOwnerMappingStoreResult stored = await new RuleOwnerMappingRunHistory(apiConnection).Store(new RuleOwnerMappingRun { ControlId = 9 });

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo("not json at all"));
                Assert.That(stored.WriteFailed, Is.False,
                    "no save was attempted, and the unreadable entry reports itself to the page instead");
            });
        }

        [Test]
        public async Task Store_ShouldReportTheFailedWrite_SoTheStaleEntryIsNotReadAsCurrent()
        {
            // the entry stays readable and keeps its earlier runs, so nothing in it marks the gap. Without
            // this flag the monitoring page shows the last recorded run as if it were the current state
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.FailHistoryWrite = true;

            RuleOwnerMappingStoreResult stored = await new RuleOwnerMappingRunHistory(apiConnection).Store(new RuleOwnerMappingRun { ControlId = 9 });

            Assert.Multiple(() =>
            {
                Assert.That(stored.WriteFailed, Is.True, "the save failed, so this run is not recorded anywhere");
                Assert.That(stored.Run.ControlId, Is.EqualTo(9), "the run still has to be judged for drift on its own");
            });
        }

        [Test]
        public async Task RunAsync_ShouldDropTheChangeNote_WhenTheRebuildFoundNoRuleBase()
        {
            // the rebuild completed, it just had nothing to judge. Left behind, the note would be taken over
            // by the next unrelated rebuild and mark it as the intended change, swallowing its drift alert
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(kRuleId, kOwnerId, 50);
            apiConnection.ClearRules();
            List<RuleOwnerMappingChange> savedChanges = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kMarker, From = "FWOC", To = "APP" }];
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(savedChanges);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.StoredRuns, Is.Empty, "a run that could not judge is still not recorded");
                Assert.That(apiConnection.StoredHistory.PendingChanges, Is.Empty,
                    "the rebuild this note was written for has completed, so the note is done");
            });
        }

        [Test]
        public async Task RunAsyncDisabled_ShouldDropTheChangeNote_WhenThereWasNothingLeftToRemove()
        {
            // switching the mapping off while no mapping is active establishes the requested state without
            // recording a run, so nothing else would drop the note of the save that triggered it
            RuleOwnerMappingFake apiConnection = new();
            List<RuleOwnerMappingChange> savedChanges = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kSource, From = "NameField", To = "Disabled" }];
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(savedChanges);

            UpdateRuleOwnerMappingDisabled service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.StoredRuns, Is.Empty, "nothing was removed, so there is no run to record");
                Assert.That(apiConnection.StoredHistory.PendingChanges, Is.Empty,
                    "the rebuild completed, so the note must not wait for the next unrelated one");
            });
        }
    }
}
