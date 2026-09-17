using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
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
    public class UpdateRuleOwnerMappingIncrementalTest
    {
        private const string kCustomFieldOwnerKey = @"[""owner""]";
        private const long kRuleId = 101;
        private const int kOwnerId = 1;

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
            });
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

        /// <summary>
        /// Simulated API connection for the CustomField mapping source.
        /// </summary>
        private sealed class RuleOwnerMappingFake : SimulatedApiConnection
        {
            private static readonly ReturnId[] AlertReturnIds = [new ReturnId { NewIdLong = 1 }];

            private readonly List<ImportControl> pendingImports = [];
            private readonly Dictionary<long, List<RuleChange>> ruleChangesByImport = [];
            private readonly Dictionary<long, List<OwnerChange>> ownerChangesByImport = [];
            private readonly List<RuleOwner> activeRuleOwners = [];
            private readonly List<Rule> rules = [CreateRule(kRuleId)];
            private List<FwoOwner> owners = [new() { Id = kOwnerId, ExtAppId = "A" }];

            public List<long> CompletedImports { get; } = [];
            public List<string> RaisedAlerts { get; } = [];
            public long? FailRuleChangeLookupForImport { get; set; }
            public string? StoredHistoryJson { get; private set; }

            public RuleOwnerMappingRunHistoryData StoredHistory => StoredHistoryJson == null
                ? new RuleOwnerMappingRunHistoryData()
                : JsonSerializer.Deserialize<RuleOwnerMappingRunHistoryData>(StoredHistoryJson) ?? new RuleOwnerMappingRunHistoryData();

            public List<RuleOwnerMappingRun> StoredRuns
            {
                get
                {
                    RuleOwnerMappingRunHistoryData history = StoredHistory;
                    return history.LastRunWithoutFindings == null
                        ? history.RunsWithFindings
                        : [.. history.RunsWithFindings, history.LastRunWithoutFindings];
                }
            }

            public List<string> ActivePairs => activeRuleOwners.Select(ruleOwner => $"{ruleOwner.RuleId}->{ruleOwner.OwnerId}").OrderBy(pair => pair).ToList();

            public void AddPendingImport(long controlId, int importTypeId)
            {
                pendingImports.Add(new ImportControl { ControlId = controlId, ImportTypeId = importTypeId });
            }

            public void AddRuleChange(long controlId, char action, long ruleId)
            {
                ruleChangesByImport[controlId] = [new RuleChange { ChangeAction = action, NewRule = CreateRule(ruleId) }];
            }

            public void AddOwnerChange(long controlId, char action, int ownerId)
            {
                ownerChangesByImport[controlId] = [new OwnerChange { ChangeAction = action, NewOwner = owners.First(owner => owner.Id == ownerId) }];
            }

            public void SeedActiveMapping(long ruleId, int ownerId, long created)
            {
                activeRuleOwners.Add(new RuleOwner { RuleId = ruleId, OwnerId = ownerId, Created = created });
            }

            public void ClearOwners()
            {
                owners = [];
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                return Task.FromResult(Handle<QueryResponseType>(query, variables));
            }

            private QueryResponseType Handle<QueryResponseType>(string query, object? variables)
            {
                if (TryHandleImportQuery(query, variables, out object? importResult))
                {
                    return (QueryResponseType)importResult!;
                }

                if (TryHandleMappingInputQuery(query, variables, out object? inputResult))
                {
                    return (QueryResponseType)inputResult!;
                }

                if (TryHandleAlertQuery(query, variables, out object? alertResult))
                {
                    return (QueryResponseType)alertResult!;
                }

                if (TryHandleConfigQuery(query, variables, out object? configResult))
                {
                    return (QueryResponseType)configResult!;
                }

                if (TryHandleMappingWriteQuery(query, variables, out object? writeResult))
                {
                    return writeResult == null ? default! : (QueryResponseType)writeResult;
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private bool TryHandleImportQuery(string query, object? variables, out object? result)
            {
                result = null;

                if (query == ImportQueries.getPendingRuleOwnerImports)
                {
                    result = pendingImports.Where(import => !CompletedImports.Contains(import.ControlId)).ToList();
                    return true;
                }

                if (query == ImportQueries.addImportForRuleOwner)
                {
                    result = new InsertImportControl { Returning = [new ImportControl { ControlId = 999 }] };
                    return true;
                }

                if (query == ImportQueries.updateImportControlForRuleOwnerInc || query == ImportQueries.updateImportControlForRuleOwnerFull)
                {
                    CompletedImports.Add(ReadLong(variables, "controlId"));
                    result = new ImportControl();
                    return true;
                }

                return false;
            }

            private bool TryHandleMappingInputQuery(string query, object? variables, out object? result)
            {
                result = null;

                if (query == RuleQueries.getChangedRulesForRuleOwnerMappingCustomField)
                {
                    long controlId = ReadLong(variables, "controlId");
                    if (FailRuleChangeLookupForImport == controlId)
                    {
                        throw new InvalidOperationException($"Simulated API failure for import_control {controlId}.");
                    }
                    result = ruleChangesByImport.TryGetValue(controlId, out List<RuleChange>? changes) ? changes : new List<RuleChange>();
                    return true;
                }

                if (query == OwnerQueries.getChangedOwnersForRuleOwnerMappingCustomField)
                {
                    long controlId = ReadLong(variables, "controlId");
                    result = ownerChangesByImport.TryGetValue(controlId, out List<OwnerChange>? changes) ? changes : new List<OwnerChange>();
                    return true;
                }

                // both queries always return the current state, exactly like the real API does
                if (query == OwnerQueries.getOwnersForRuleOwnerCustomField)
                {
                    result = owners.ToList();
                    return true;
                }

                if (query == RuleQueries.getRulesForRuleOwnerCustomField || query == RuleQueries.getRulesForOwnerMappingCustomField)
                {
                    result = rules.ToList();
                    return true;
                }

                if (query == OwnerQueries.getActiveRuleOwners)
                {
                    result = activeRuleOwners.Select(Clone).ToList();
                    return true;
                }

                if (query == OwnerQueries.getRuleOwnerToRemoveByRule)
                {
                    List<long> ruleIds = ReadList<long>(variables, "ruleIds");
                    result = activeRuleOwners.Where(ruleOwner => ruleIds.Contains(ruleOwner.RuleId)).Select(Clone).ToList();
                    return true;
                }

                if (query == OwnerQueries.getRuleOwnerToRemoveByOwner)
                {
                    List<int> ownerIds = ReadList<int>(variables, "ownerIds");
                    result = activeRuleOwners.Where(ruleOwner => ownerIds.Contains(ruleOwner.OwnerId)).Select(Clone).ToList();
                    return true;
                }

                return false;
            }

            private bool TryHandleMappingWriteQuery(string query, object? variables, out object? result)
            {
                result = null;

                if (query == OwnerQueries.setAllActiveRuleOwnersRemoved)
                {
                    // returning delivers the state that was just replaced, like the real mutation does
                    result = new UpdateRuleOwner { AffectedRows = activeRuleOwners.Count, Returning = activeRuleOwners.Select(Clone).ToList() };
                    activeRuleOwners.Clear();
                    return true;
                }

                if (query == OwnerQueries.setAffectedRuleOwnersRemoved)
                {
                    RemoveAffectedRuleOwners(variables);
                    return true;
                }

                if (query == OwnerQueries.insertRuleOwners)
                {
                    InsertRuleOwners(variables);
                    return true;
                }

                return false;
            }

            private bool TryHandleConfigQuery(string query, object? variables, out object? result)
            {
                result = null;

                if (query == ConfigQueries.getConfigItemByKey)
                {
                    result = StoredHistoryJson == null ? new List<ConfigItem>() : new List<ConfigItem> { new() { Value = StoredHistoryJson } };
                    return true;
                }

                if (query == ConfigQueries.upsertConfigItem)
                {
                    StoredHistoryJson = ReadString(variables, "config_value");
                    result = new object();
                    return true;
                }

                return false;
            }

            private bool TryHandleAlertQuery(string query, object? variables, out object? result)
            {
                result = null;

                if (query == MonitorQueries.getOpenAlerts)
                {
                    result = new List<Alert>();
                    return true;
                }

                if (query == MonitorQueries.addLogEntry)
                {
                    result = new ReturnIdWrapper { ReturnIds = AlertReturnIds };
                    return true;
                }

                if (query == MonitorQueries.addAlert)
                {
                    RaisedAlerts.Add(ReadString(variables, "description"));
                    result = new ReturnIdWrapper { ReturnIds = AlertReturnIds };
                    return true;
                }

                if (query == MonitorQueries.acknowledgeAlert)
                {
                    result = new ReturnId();
                    return true;
                }

                return false;
            }

            private void InsertRuleOwners(object? variables)
            {
                foreach (RuleOwner ruleOwner in ReadList<RuleOwner>(variables, "objects"))
                {
                    if (activeRuleOwners.Any(existing => existing.RuleId == ruleOwner.RuleId && existing.OwnerId == ruleOwner.OwnerId))
                    {
                        // mirrors idx_rule_owner_removed_is_null_unique, which on_conflict on pk_rule_owner cannot catch
                        throw new InvalidOperationException("Uniqueness violation. duplicate key value violates unique constraint " +
                            $"\"idx_rule_owner_removed_is_null_unique\". Key (rule_id, owner_id)=({ruleOwner.RuleId}, {ruleOwner.OwnerId}) already exists.");
                    }
                    activeRuleOwners.Add(Clone(ruleOwner));
                }
            }

            private void RemoveAffectedRuleOwners(object? variables)
            {
                object? objects = variables?.GetType().GetProperty("objects")?.GetValue(variables);
                if (objects is not System.Collections.IEnumerable entries)
                {
                    return;
                }

                foreach (object entry in entries)
                {
                    long ruleId = ReadNestedLong(entry, "rule_id");
                    int ownerId = (int)ReadNestedLong(entry, "owner_id");
                    long created = ReadNestedLong(entry, "created");
                    activeRuleOwners.RemoveAll(ruleOwner => ruleOwner.RuleId == ruleId && ruleOwner.OwnerId == ownerId && ruleOwner.Created == created);
                }
            }

            private static Rule CreateRule(long ruleId)
            {
                return new Rule
                {
                    Id = ruleId,
                    CustomFields = "{'owner':'A'}",
                    Metadata = new RuleMetadata { Id = ruleId + 1000 }
                };
            }

            private static RuleOwner Clone(RuleOwner source)
            {
                return new RuleOwner
                {
                    RuleId = source.RuleId,
                    OwnerId = source.OwnerId,
                    Created = source.Created,
                    RuleMetadataId = source.RuleMetadataId,
                    OwnerMappingSourceId = source.OwnerMappingSourceId
                };
            }

            private static long ReadLong(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing long property '{propertyName}'.")
                };
            }

            private static string ReadString(object? variables, string propertyName)
            {
                return variables?.GetType().GetProperty(propertyName)?.GetValue(variables) as string ?? "";
            }

            private static List<TEntry> ReadList<TEntry>(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<TEntry> ?? [];
            }

            private static long ReadNestedLong(object source, string propertyName)
            {
                object? wrapper = source.GetType().GetProperty(propertyName)?.GetValue(source);
                object? eqValue = wrapper?.GetType().GetProperty("_eq")?.GetValue(wrapper);
                return eqValue switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing nested _eq value for '{propertyName}'.")
                };
            }
        }
    }
}
