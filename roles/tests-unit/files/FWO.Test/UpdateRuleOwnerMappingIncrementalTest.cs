using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Services;
using FWO.Services.EventMediator.Events;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FWO.Test
{
    /// <summary>
    /// Regression tests for the incremental rule_owner mapping. The simulated API mirrors two properties of
    /// the real backend that the older simulations did not: the owner and rule queries always return the
    /// current state regardless of which import is being processed, and an insert that would create a second
    /// active mapping for the same (rule_id, owner_id) pair fails like the partial unique index does.
    /// </summary>
    [TestFixture]
    public partial class UpdateRuleOwnerMappingIncrementalTest
    {
        private const string kCustomFieldOwnerKey = @"[""owner""]";
        private const long kRuleId = 101;
        private const int kOwnerId = 1;

        /// <summary>Marks the alert raised when a failing import cannot be remembered, so no repeat is ever read.</summary>
        private const string kUnrecordableAlertMarker = "could not be written";

        private static GlobalConfig CustomFieldConfig()
        {
            return new GlobalConfig { CustomFieldOwnerKey = kCustomFieldOwnerKey };
        }

        [Test]
        public async Task RunAsync_ShouldSkipStillActiveMapping_WhenOwnerImportFollowsRuleImport()
        {
            // the rule import maps the rule to the owner because the owner already exists by the time the
            // pending import is processed; the owner insert then rebuilds the very same pair without
            // removing anything first, which used to collide with the partial unique index
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.AddPendingImport(2, ImportType.OWNER);
            apiConnection.AddRuleChange(1, ChangelogActionType.INSERT, kRuleId);
            apiConnection.AddOwnerChange(2, ChangelogActionType.INSERT, kOwnerId);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "both imports should be processed without error");
                Assert.That(apiConnection.CompletedImports, Is.EquivalentTo(new List<long> { 1, 2 }), "the owner import must not stay pending");
                Assert.That(apiConnection.ActivePairs, Is.EquivalentTo(new List<string> { "101->1" }), "the rule must keep exactly one active mapping");
                Assert.That(apiConnection.RaisedAlerts, Is.Empty, "a clean run must not raise an alert");
            });
        }

        [Test]
        public async Task RunAsync_ShouldStillProcessLaterImports_WhenOneImportFails()
        {
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.AddPendingImport(2, ImportType.RULE);
            apiConnection.AddRuleChange(2, ChangelogActionType.INSERT, kRuleId);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "a failed import must not be reported as success");
                Assert.That(apiConnection.CompletedImports, Does.Contain(2L), "the healthy import behind the broken one must still be processed");
                Assert.That(apiConnection.CompletedImports, Does.Not.Contain(1L), "the failed import has to stay pending for the next run");
            });
        }

        [Test]
        public async Task RunAsync_ShouldReportFailure_WhenMarkingTheImportDoneFails()
        {
            // the mapping changes are written but the import stays pending, so it would be processed again -
            // reporting the run as successful would hide exactly that
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.AddRuleChange(1, ChangelogActionType.INSERT, kRuleId);
            apiConnection.FailCompletionForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "a completion that failed must not be reported as success");
                Assert.That(apiConnection.CompletedImports, Is.Empty, "the import stays pending");
                Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("import_control 1"));
            });
        }

        [Test]
        public async Task RunAsync_ShouldRaiseAlert_WhenImportFails()
        {
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync();

            Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("import_control 1"));
        }

        [Test]
        public async Task RunAsync_ShouldRemoveObsoleteMappingsAndAlert_WhenFullReinitializeMatchesNothing()
        {
            // the configured mapping source stops matching, for instance after the custom field key changed:
            // that is a valid result and the obsolete mappings have to go
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(kRuleId, kOwnerId, 50);
            apiConnection.ClearOwners();

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "an empty result is valid and must not be reported as failure");
                Assert.That(apiConnection.ActivePairs, Is.Empty, "the obsolete mappings must be removed");
                Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("matched no rule"));
                Assert.That(apiConnection.RaisedAlerts, Has.None.Contains("incremental mapping missed"),
                    "the source matched nothing, which is its own problem and not one the incremental mapping caused");
                Assert.That(apiConnection.StoredRuns.Single().State, Is.EqualTo(RuleOwnerMappingRunState.EmptyResult),
                    "the monitoring page has to read the same verdict off the run as the alert did");
            });
        }

        [Test]
        public async Task RunAsync_ShouldNotStoreDerivedFields_InTheRunHistoryConfigEntry()
        {
            // the entry is written with config_user = 0, which the anonymous role may read - so it is kept to
            // what cannot be recomputed. State and HasNoFindings are derived from the counts and must stay out
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.That(apiConnection.StoredHistoryJson, Is.Not.Null, "the run has to have been written at all");

            // asserted over the property names the stored document actually carries, not over substrings of
            // it: the derived properties have no JsonPropertyName, so dropping their JsonIgnore would write
            // them under their CLR name and a lower-cased needle would never see them
            using JsonDocument storedDocument = JsonDocument.Parse(apiConnection.StoredHistoryJson!);
            List<string> propertyNames = CollectPropertyNames(storedDocument.RootElement);

            Assert.Multiple(() =>
            {
                Assert.That(propertyNames, Is.Not.Empty, "the stored document has to carry properties at all");
                Assert.That(propertyNames, Has.None.EqualTo(nameof(RuleOwnerMappingRun.State)).IgnoreCase,
                    "State is derived, not stored");
                Assert.That(propertyNames, Has.None.EqualTo(nameof(RuleOwnerMappingRun.HasNoFindings)).IgnoreCase,
                    "HasNoFindings is derived, not stored");
            });
        }

        /// <summary>
        /// Collects every property name of a stored document, at any depth.
        /// </summary>
        /// <param name="element">Element to walk.</param>
        /// <returns>All property names found below and including the element.</returns>
        private static List<string> CollectPropertyNames(JsonElement element)
        {
            List<string> names = [];
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    names.AddRange(CollectPropertyNames(property.Value));
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    names.AddRange(CollectPropertyNames(item));
                }
            }
            return names;
        }

        [Test]
        public async Task RunAsync_ShouldRecordDriftAndAlert_WhenFullReinitializeChangesMappingsWithoutPendingImports()
        {
            // a stale mapping the rebuild does not produce any more: with a correct incremental mapping it
            // would already be gone, so the full reinitialize finding it means the incremental path missed it
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            List<RuleOwnerMappingRun> runs = apiConnection.StoredRuns;

            Assert.Multiple(() =>
            {
                Assert.That(runs, Has.Count.EqualTo(1), "the run has to be recorded in the history");
                Assert.That(runs[0].DiffMeaningful, Is.True, "no import was pending, so the difference is drift");
                Assert.That(runs[0].AddedCount, Is.EqualTo(1));
                Assert.That(runs[0].Added.Single().RuleId, Is.EqualTo(kRuleId));
                Assert.That(runs[0].RemovedCount, Is.EqualTo(1));
                Assert.That(runs[0].Removed.Single().RuleId, Is.EqualTo(999));
                Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("incremental mapping missed"));
            });
        }

        [Test]
        public async Task RunAsync_ShouldNotReportDrift_WhenTheRunFollowsADeliberateChange()
        {
            // same starting point as the drift test above, but triggered by saving a changed mapping
            // configuration: the rebuilt state differs by design, so it is not the incremental mapping's fault
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true, TriggeredByChange = true });

            List<RuleOwnerMappingRun> runs = apiConnection.StoredRuns;

            Assert.Multiple(() =>
            {
                Assert.That(runs[0].TriggeredByChange, Is.True, "the run has to be marked so the difference can be read correctly");
                Assert.That(runs[0].AddedCount, Is.EqualTo(1), "the change is still recorded in full");
                Assert.That(runs[0].RemovedCount, Is.EqualTo(1));
                Assert.That(apiConnection.RaisedAlerts, Has.None.Contains("incremental mapping missed"),
                    "a deliberate configuration change must not be reported as drift");
            });
        }

        [Test]
        public async Task RunAsync_ShouldKeepRunsWithoutFindingsOutOfTheLimitedHistory()
        {
            // pressing the rebuild button repeatedly must not push runs that did find something out of the
            // history, so a run without findings only updates "last verified correct"
            RuleOwnerMappingFake apiConnection = new();
            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            RuleOwnerMappingRunHistoryData history = apiConnection.StoredHistory;

            Assert.Multiple(() =>
            {
                Assert.That(history.RunsWithFindings, Has.Count.EqualTo(1), "only the first run found something");
                Assert.That(history.RunsWithFindings[0].AddedCount, Is.EqualTo(1));
                Assert.That(history.LastRunWithoutFindings, Is.Not.Null, "the later clean runs are recorded separately");
            });
        }

        [Test]
        public async Task RunAsync_ShouldMarkDiffAsNotMeaningful_WhenImportsWereStillPending()
        {
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);
            apiConnection.AddPendingImport(7, ImportType.RULE);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            List<RuleOwnerMappingRun> runs = apiConnection.StoredRuns;

            Assert.Multiple(() =>
            {
                Assert.That(runs[0].DiffMeaningful, Is.False, "a pending backlog explains the difference, it is not drift");
                Assert.That(runs[0].PendingImportsBefore, Is.EquivalentTo(new List<long> { 7 }), "the unprocessed imports have to be documented");
                Assert.That(apiConnection.RaisedAlerts, Has.None.Contains("incremental mapping missed"), "drift must not be reported while imports are pending");
            });
        }

        [Test]
        public async Task RunAsync_ShouldKeepOnlyTheCounts_WhenTheMappingSourceWasSwitched()
        {
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs
            {
                isFullReInitialize = true,
                TriggeredByChange = true,
                Changes = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kSource, From = "IpBased", To = "CustomField" }]
            });

            RuleOwnerMappingRun run = apiConnection.StoredHistory.RunsWithFindings.Single();

            Assert.Multiple(() =>
            {
                Assert.That(run.AddedCount, Is.EqualTo(1), "the counts stay complete");
                Assert.That(run.RemovedCount, Is.EqualTo(1));
                Assert.That(run.Added, Is.Empty, "a source switch replaces every mapping, listing them says nothing");
                Assert.That(run.Removed, Is.Empty);
                Assert.That(run.PairListsTruncated, Is.False, "the lists were left out on purpose, not cut off");
            });
        }

        [Test]
        public async Task RunAsync_ShouldKeepThePairLists_WhenOnlyTheMarkerWasChanged()
        {
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs
            {
                isFullReInitialize = true,
                TriggeredByChange = true,
                Changes = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kMarker, From = "FWOC", To = "APP" }]
            });

            RuleOwnerMappingRun run = apiConnection.StoredHistory.RunsWithFindings.Single();

            Assert.Multiple(() =>
            {
                Assert.That(run.Added, Has.Count.EqualTo(1));
                Assert.That(run.Removed, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RunAsyncDisabled_ShouldRecordTheRunWithItsChangeNote()
        {
            // switching the mapping off removes everything, and that belongs in the history just like any
            // other rebuild - otherwise nothing explains why the mapping table is empty
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(kRuleId, kOwnerId, 50);

            UpdateRuleOwnerMappingDisabled service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs
            {
                isFullReInitialize = true,
                TriggeredByChange = true,
                Changes = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kSource, From = "NameField", To = "Disabled" }]
            });

            RuleOwnerMappingRun run = apiConnection.StoredHistory.RunsWithFindings.Single();

            Assert.Multiple(() =>
            {
                Assert.That(run.RemovedCount, Is.EqualTo(1), "the removed mappings are counted");
                Assert.That(run.MappingCount, Is.EqualTo(0), "nothing is mapped afterwards");
                Assert.That(run.TriggeredByChange, Is.True, "switching off is deliberate and must not read as drift");
                Assert.That(run.Changes.Single().To, Is.EqualTo("Disabled"), "the change note has to survive this path too");
                Assert.That(apiConnection.RaisedAlerts, Is.Empty, "a deliberate switch off raises no alert");
            });
        }

        [Test]
        public async Task RunAsync_ShouldRecordAChangeEvenWhenItHadNoEffect()
        {
            // that an edited setting changed nothing is exactly what somebody who just edited it needs to
            // see - without this the change note would vanish and only the clean stamp would be refreshed
            RuleOwnerMappingFake apiConnection = new();
            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            // first run establishes the mapping, the second changes a setting without any effect
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs
            {
                isFullReInitialize = true,
                TriggeredByChange = true,
                Changes = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kCustomFieldKeys, From = @"[""owner""]", To = @"[""owner"",""unused""]" }]
            });

            RuleOwnerMappingRunHistoryData history = apiConnection.StoredHistory;
            RuleOwnerMappingRun changeRun = history.RunsWithFindings[0];

            Assert.Multiple(() =>
            {
                Assert.That(changeRun.TriggeredByChange, Is.True);
                Assert.That(changeRun.AddedCount + changeRun.RemovedCount, Is.EqualTo(0), "the change had no effect");
                Assert.That(changeRun.Changes.Single().To, Does.Contain("unused"), "the note survives all the same");
                Assert.That(history.LastRunWithoutFindings, Is.Not.Null, "it still counts as a verification");
            });
        }

        [Test]
        public async Task RunAsync_ShouldNotClaimAVerification_WhenImportsWerePending()
        {
            // such a run cannot judge anything, so it must not refresh "last verified without deviation"
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(7, ImportType.OWNER);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.That(apiConnection.StoredHistory.LastRunWithoutFindings, Is.Null);
        }

        [Test]
        public async Task RunAsync_ShouldRepairByFullReinitialize_WhenTheSameImportFailsAgain()
        {
            // the healthy imports drain, so a stuck one never lets the backlog reach the fallback threshold -
            // without this its changes would stay unapplied for good
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool firstRun = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(firstRun, Is.False, "the first failure is reported and retried, not repaired");
                Assert.That(apiConnection.FullReinitializeCount, Is.EqualTo(0));
                Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("import_control 1"));
            });

            await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.FullReinitializeCount, Is.EqualTo(1), "the repeated failure triggers the rebuild");
                Assert.That(apiConnection.CompletedImports, Does.Contain(1L), "the rebuild completes the stuck import");
                Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("import_control 1"), "the open alert is not raised twice");
            });
        }

        [Test]
        public async Task RunAsync_ShouldReportTheBlockedRepair_WhenTheHistoryCannotBeDecoded()
        {
            // the repair above waits for a repeat that an undecodable entry can never report, because no
            // writer saves over it. Without saying so the import would stay stuck for good behind an alert
            // that only names the first failure
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson("not json at all");
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync();
            await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.FullReinitializeCount, Is.EqualTo(0),
                    "rebuilding every run for as long as both conditions last costs more than the stuck import");
                Assert.That(apiConnection.OpenAlerts, Has.Exactly(1).Contains("has to be reset"),
                    "the admin is told what to do, and the constant text keeps it to one open alert per run");
                Assert.That(apiConnection.StoredHistoryJson, Is.EqualTo("not json at all"),
                    "reporting the problem must not repair it by overwriting the entry");
            });
        }

        [Test]
        public async Task RunAsync_ShouldRepairAgain_OnceTheUndecodableHistoryWasReset()
        {
            // the alert asks for a reset, so the run after one has to be back to the normal repair - the
            // whole point of naming the remedy
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson("not json at all");
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync();
            apiConnection.SeedStoredHistoryJson("");
            await service.RunAsync();
            await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.FullReinitializeCount, Is.EqualTo(1), "the repeated failure triggers the rebuild again");
                Assert.That(apiConnection.CompletedImports, Does.Contain(1L), "the rebuild completes the stuck import");
            });
        }

        [Test]
        public async Task RunAsync_ShouldReportThatItCannotRemember_WhenTheHistoryCannotBeWrittenAtAll()
        {
            // the writes fail before the first failure is recorded, so no run ever reads a repeat and the
            // rebuild is never reached. Reporting it is what keeps that from being silent
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;
            apiConnection.FailHistoryWrite = true;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync();
            await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.FullReinitializeCount, Is.EqualTo(0),
                    "no repeat can be established, so the rebuild the repair relies on is never reached");
                Assert.That(apiConnection.RaisedAlerts.Count(description => description.Contains(kUnrecordableAlertMarker)), Is.EqualTo(2),
                    "the condition persists, so each run reports it rather than passing as an ordinary first failure");
                Assert.That(apiConnection.OpenAlerts.Count(description => description.Contains(kUnrecordableAlertMarker)), Is.EqualTo(1),
                    "the description is constant, so the repeats do not pile up as open alerts");
                Assert.That(apiConnection.RaisedAlerts, Has.Some.Contains("config_user = 0"),
                    "the alert has to say where the entry is, because no screen offers to fix it");
            });
        }

        [Test]
        public async Task RunAsync_ShouldStillRepair_WhenTheRepeatCouldNotBeWrittenBack()
        {
            // the first failure was recorded, so the second run reads the repeat. Forfeiting it because
            // writing the same ids back failed would leave the import stuck for as long as that lasts
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync();
            apiConnection.FailHistoryWrite = true;
            await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.FullReinitializeCount, Is.EqualTo(1),
                    "the repeat came from the read, so the failing write does not disarm the repair");
                Assert.That(apiConnection.CompletedImports, Does.Contain(1L), "the rebuild completes the stuck import");
                Assert.That(apiConnection.RaisedAlerts, Has.None.Contains(kUnrecordableAlertMarker),
                    "the repair ran, so the failing write is not reported as one that prevented it");
            });
        }

        [Test]
        public async Task RunAsync_ShouldStillRepair_WhenTheAlertWasAcknowledgedInBetween()
        {
            // acknowledging an alert is the normal response to it and must not disarm the repair: the run
            // history remembers the previous failure, the open alert does not
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync();
            Assert.That(apiConnection.StoredHistory.FailedImports, Does.Contain(1L), "the failure is remembered where an ack cannot reach it");

            apiConnection.AcknowledgeAlerts();
            await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.FullReinitializeCount, Is.EqualTo(1), "the repeated failure still triggers the rebuild");
                Assert.That(apiConnection.CompletedImports, Does.Contain(1L), "the rebuild completes the stuck import");
                Assert.That(apiConnection.StoredHistory.FailedImports, Is.Empty, "the repaired failure is forgotten again");
            });
        }

        [Test]
        public async Task RunAsync_ShouldNotMigrateAnUnverifiableRun_FromTheLegacyHistoryFormat()
        {
            // the older format stored a plain run list. A run that found nothing while imports were still
            // pending proves nothing, so it must not arrive as "last verified without deviation"
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson("""
                [{"runTime":"2026-09-01T10:00:00Z","controlId":5,"mappingSource":"CustomField","mappingCount":3,
                  "addedCount":0,"removedCount":0,"pendingImportsBefore":[9],"diffMeaningful":false}]
                """);
            apiConnection.AddPendingImport(7, ImportType.OWNER);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.That(apiConnection.StoredHistory.LastRunWithoutFindings, Is.Null,
                "a run taken over from the legacy format must pass the same check as a new one");
        }

        [Test]
        public async Task RunAsync_ShouldMigrateADeliberateChangeWithoutEffect_FromTheLegacyHistoryFormat()
        {
            // Store keeps a run that followed a deliberate change even when it changed nothing - that it had
            // no effect is what the admin who edited the configuration needs to see. The migration has to
            // apply the same rule, otherwise such a run is written on one run and dropped on the next read
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedStoredHistoryJson("""
                [{"runTime":"2026-09-01T10:00:00Z","controlId":5,"mappingSource":"CustomField","mappingCount":3,
                  "addedCount":0,"removedCount":0,"pendingImportsBefore":[],"diffMeaningful":true,
                  "triggeredByChange":true}]
                """);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.That(apiConnection.StoredHistory.RunsWithFindings.Select(run => run.ControlId), Does.Contain(5L),
                "a deliberate change without effect must survive the migration, as it survives a store");
        }

        [Test]
        public async Task RunAsync_ShouldReplaceTheOpenAlert_WhenTheSameProblemOccursAgain()
        {
            // the open alert has to carry the latest occurrence, otherwise "failed once last week" and
            // "failing on every run since" are indistinguishable in the alert list
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.ClearOwners();

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });
            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RaisedAlerts.Count(description => description.Contains("matched no rule")), Is.EqualTo(2),
                    "a problem that is still there has to be reported again");
                Assert.That(apiConnection.OpenAlerts, Has.Exactly(1).Contains("matched no rule"),
                    "the previous alert is acknowledged, so the list does not accumulate identical open entries");
            });
        }

        [Test]
        public async Task RunAsync_ShouldNotAlertTwice_WhileTheSameImportKeepsFailing()
        {
            // the one condition that can repeat on every run: alerting per run would leave one acknowledged
            // alert per run behind, so only the state change is reported and the repair takes over
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.AddPendingImport(1, ImportType.RULE);
            apiConnection.FailRuleChangeLookupForImport = 1;

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync();
            await service.RunAsync();

            Assert.That(apiConnection.RaisedAlerts.Count(description => description.Contains("import_control 1")), Is.EqualTo(1));
        }

        [Test]
        public async Task RunAsync_ShouldKeepTheMappings_WhenNoRuleCouldBeLoaded()
        {
            // an empty rule base is not "the source matched nothing": there is nothing to judge, so removing
            // every mapping would act on an input the run never had
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(kRuleId, kOwnerId, 50);
            apiConnection.AddPendingImport(7, ImportType.RULE);
            apiConnection.ClearRules();

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "there was nothing to do, which is not a failure");
                Assert.That(apiConnection.ActivePairs, Is.EquivalentTo(new List<string> { "101->1" }), "the existing mappings must survive");
                Assert.That(apiConnection.RaisedAlerts, Is.Empty, "nothing was observed, so nothing is reported");
                Assert.That(apiConnection.StoredRuns, Is.Empty, "a run that could not judge must not be recorded");
                Assert.That(apiConnection.CompletedImports, Does.Contain(7L), "the backlog still has to drain");
            });
        }

        [Test]
        public async Task RunAsync_ShouldRemoveTheMappingsAndAlert_WhenRulesExistButNoneCanBeMapped()
        {
            // the counterpart of the test above, and the reason the rule base is probed separately: the rule
            // query is narrowed to what the source can map, so its empty result means "nothing matches any
            // more" as long as rules exist at all. The obsolete mappings have to go, and somebody has to hear
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(kRuleId, kOwnerId, 50);
            apiConnection.ReplaceRulesWithUnmappableRule(kRuleId);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            bool result = await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, "an empty result is a result, not a failure");
                Assert.That(apiConnection.ActivePairs, Is.Empty, "a source that stopped matching must not leave its mappings active");
                Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("matched no rule"), "the operator has to hear about it");
                Assert.That(apiConnection.StoredRuns, Is.Not.Empty, "this run judged the state, so it belongs in the history");
            });
        }

        [Test]
        public async Task RunAsync_ShouldNotReportDrift_WhenAChangeWasSavedButItsRebuildNeverCompleted()
        {
            // the configuration is written before the rebuild runs. After a failed rebuild the new setting is
            // live while the mappings still follow the old one, and the next rebuild - here the manual
            // recalculation, which knows nothing about the save - would report that difference as drift
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);
            List<RuleOwnerMappingChange> savedChanges = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kMarker, From = "FWOC", To = "APP" }];
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(savedChanges);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            RuleOwnerMappingRunHistoryData history = apiConnection.StoredHistory;
            RuleOwnerMappingRun run = history.RunsWithFindings.Single();

            Assert.Multiple(() =>
            {
                Assert.That(run.TriggeredByChange, Is.True, "the saved change explains the difference");
                Assert.That(run.Changes.Single().To, Is.EqualTo("APP"), "and is named with the run");
                Assert.That(apiConnection.RaisedAlerts, Has.None.Contains("incremental mapping missed"));
                Assert.That(history.PendingChanges, Is.Empty, "the note is consumed once a rebuild applied it");
            });
        }

        [Test]
        public async Task RunAsync_ShouldReportDrift_WhenTheSavedChangeWasNeverAppliedForTooLong()
        {
            // the note outlives the rebuild it was written for, because that rebuild may fail - but not
            // forever. Left behind by a save nobody ever retried, it would mark an unrelated rebuild weeks
            // later as intended and swallow its drift alert, which is the one signal this history exists for
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);
            RuleOwnerMappingRunHistoryData staleHistory = new()
            {
                PendingChanges = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kMarker, From = "FWOC", To = "APP" }],
                PendingChangesRecordedAt = DateTime.UtcNow - RuleOwnerMappingRunHistory.kPendingChangesMaxAge - TimeSpan.FromMinutes(1)
            };
            apiConnection.SeedStoredHistoryJson(JsonSerializer.Serialize(staleHistory));

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            RuleOwnerMappingRunHistoryData history = apiConnection.StoredHistory;
            RuleOwnerMappingRun run = history.RunsWithFindings.Single();

            Assert.Multiple(() =>
            {
                Assert.That(run.TriggeredByChange, Is.False, "an expired note must not explain this run away");
                Assert.That(run.Changes, Is.Empty, "and must not be named as the cause of its difference");
                Assert.That(apiConnection.RaisedAlerts, Has.Exactly(1).Contains("was never applied by a rebuild"),
                    "the difference has to be reported, naming the note the run dropped as a possible cause");
                Assert.That(apiConnection.RaisedAlerts, Has.None.Contains("incremental mapping missed these changes"),
                    "having dropped the evidence, the run cannot assert that cause");
                Assert.That(run.DroppedChangeRecordedAt, Is.EqualTo(staleHistory.PendingChangesRecordedAt).Within(TimeSpan.FromSeconds(1)),
                    "the page needs the dropped note as well, the log line alone does not reach the operator");
                Assert.That(history.PendingChanges, Is.Empty, "the expired note is dropped rather than carried on");
            });
        }

        [Test]
        public async Task RunAsync_ShouldDropThePairLists_WhenThePendingChangeSwitchedTheSource()
        {
            // same rule as in BuildRun: after a source switch every mapping differs, so the pairs say nothing
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);
            List<RuleOwnerMappingChange> savedChanges = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kSource, From = "IpBased", To = "CustomField" }];
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(savedChanges);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs { isFullReInitialize = true });

            RuleOwnerMappingRun run = apiConnection.StoredHistory.RunsWithFindings.Single();

            Assert.Multiple(() =>
            {
                Assert.That(run.AddedCount, Is.EqualTo(1), "the counts stay complete");
                Assert.That(run.RemovedCount, Is.EqualTo(1));
                Assert.That(run.Added, Is.Empty);
                Assert.That(run.Removed, Is.Empty);
                Assert.That(run.PairListsTruncated, Is.False, "the lists were left out on purpose, not cut off");
            });
        }

        [Test]
        public async Task RunAsync_ShouldNameTheChangeOnce_WhenTheRebuildCarriesItAsWell()
        {
            // the save records the note and the rebuild it triggers carries the same one
            RuleOwnerMappingFake apiConnection = new();
            apiConnection.SeedActiveMapping(999, kOwnerId, 50);
            List<RuleOwnerMappingChange> changes = [new RuleOwnerMappingChange { Setting = RuleOwnerMappingChangeSetting.kMarker, From = "FWOC", To = "APP" }];
            await new RuleOwnerMappingRunHistory(apiConnection).RecordPendingChanges(changes);

            UpdateRuleOwnerMappingCustomField service = new(apiConnection, CustomFieldConfig());

            await service.RunAsync(new UpdateRuleOwnerMappingEventArgs
            {
                isFullReInitialize = true,
                TriggeredByChange = true,
                Changes = changes
            });

            Assert.That(apiConnection.StoredHistory.RunsWithFindings.Single().Changes, Has.Count.EqualTo(1));
        }
    }
}
