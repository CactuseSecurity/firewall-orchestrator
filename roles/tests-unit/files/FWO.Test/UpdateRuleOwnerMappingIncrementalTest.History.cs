using FWO.Basics;
using FWO.Data;
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
                FailedImports = [77],
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

            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(
                [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kSource, From = "IpBased", To = "NameField" }]);

            Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo(storedBefore),
                "not recording the note costs a wrong drift alert, writing over the entry costs the entry");
        }

        [Test]
        public async Task RecordFailedImports_ShouldReportNoRepeat_WhenTheHistoryCouldNotBeRead()
        {
            // reporting "not seen before" delays the repair by one run; the opposite would rebuild
            // everything on a single transient failure
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson(SeededHistoryJson());
            apiConnection.FailHistoryRead = true;

            bool failedBefore = await new RuleOwnerMappingRunHistory(apiConnection).RecordFailedImports([77]);

            Assert.That(failedBefore, Is.False, "without the stored state a repeat cannot be asserted");
        }

        [Test]
        public async Task Load_ShouldAnswerAnEmptyHistory_WhenItCouldNotBeRead()
        {
            // the monitoring page only displays the value, so degrading to an empty history stays right there
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson(SeededHistoryJson());
            apiConnection.FailHistoryRead = true;

            RuleOwnerMappingRunHistoryData history = await new RuleOwnerMappingRunHistory(apiConnection).Load();

            Assert.Multiple(() =>
            {
                Assert.That(history.RunsWithFindings, Is.Empty);
                Assert.That(history.LastRunWithoutFindings, Is.Null);
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
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(
                [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kMarker, From = "FWOC", To = "APP" }]);

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
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(
                [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kSource, From = "NameField", To = "Disabled" }]);

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
