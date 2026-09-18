using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Services;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// The simulated backend the rule_owner mapping tests run against. It mirrors the properties of the real
    /// one that the outcome depends on: the owner and rule queries always answer with the current state
    /// regardless of which import is being processed, an insert creating a second active mapping for the same
    /// (rule_id, owner_id) pair fails like the partial unique index does, and an alert stays open until
    /// somebody acknowledges it.
    /// </summary>
    public partial class UpdateRuleOwnerMappingIncrementalTest
    {
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
            private readonly List<Alert> openAlerts = [];
            private List<Rule> rules = [CreateRule(kRuleId)];
            private List<FwoOwner> owners = [new() { Id = kOwnerId, ExtAppId = "A" }];
            private long nextAlertId = 1;

            public List<long> CompletedImports { get; } = [];

            /// <summary>Every alert that was raised, including ones acknowledged again since.</summary>
            public List<string> RaisedAlerts { get; } = [];

            /// <summary>Alerts still waiting for somebody to acknowledge them.</summary>
            public List<string> OpenAlerts => openAlerts.Select(alert => alert.Description ?? "").ToList();
            public long? FailRuleChangeLookupForImport { get; set; }
            public long? FailCompletionForImport { get; set; }

            /// <summary>
            /// Fails the pending-import lookup once the full reinitialize completed its own import control.
            /// That is the lookup CompleteOlderPendingImports makes to drain the backlog the rebuild has
            /// already covered - everything the rebuild itself needs is written by then.
            /// </summary>
            public bool FailBacklogDrainAfterFullReinitialize { get; set; }

            private bool fullReinitializeCompleted;
            public int FullReinitializeCount { get; private set; }
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

            /// <summary>Clears the open alerts, as acknowledging them in the monitoring view does.</summary>
            public void AcknowledgeAlerts()
            {
                openAlerts.Clear();
            }

            /// <summary>Seeds the stored config entry, for instance in the shape an older version wrote.</summary>
            /// <param name="json">Value to store under the history config key.</param>
            public void SeedStoredHistoryJson(string json)
            {
                StoredHistoryJson = json;
            }

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

            /// <summary>Empties the rule base, as a fresh installation or a failed rule query leaves it.</summary>
            public void ClearRules()
            {
                rules = [];
            }

            /// <summary>
            /// Replaces the rule base with a rule the custom field source cannot map, as a rule base whose
            /// custom fields disappeared leaves it: the mapping query comes back empty while rules exist.
            /// The jsonb column is null in that case, which the non-nullable property carries at run time.
            /// </summary>
            /// <param name="ruleId">Id of the remaining rule.</param>
            public void ReplaceRulesWithUnmappableRule(long ruleId)
            {
                rules = [new Rule { Id = ruleId, CustomFields = null!, Metadata = new RuleMetadata { Id = ruleId + 1000 } }];
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
                    if (FailBacklogDrainAfterFullReinitialize && fullReinitializeCompleted)
                    {
                        throw new InvalidOperationException("Simulated API failure while looking up the imports the rebuild already covered.");
                    }

                    result = pendingImports.Where(import => !CompletedImports.Contains(import.ControlId)).ToList();
                    return true;
                }

                if (query == ImportQueries.addImportForRuleOwner)
                {
                    FullReinitializeCount++;
                    result = new InsertImportControl { Returning = [new ImportControl { ControlId = 999 }] };
                    return true;
                }

                if (query == ImportQueries.updateImportControlForRuleOwnerInc || query == ImportQueries.updateImportControlForRuleOwnerFull)
                {
                    long completedId = ReadLong(variables, "controlId");
                    if (FailCompletionForImport == completedId)
                    {
                        throw new InvalidOperationException($"Simulated API failure while completing import_control {completedId}.");
                    }
                    CompletedImports.Add(completedId);
                    fullReinitializeCompleted |= query == ImportQueries.updateImportControlForRuleOwnerFull;
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

                if (query == RuleQueries.getRulesForRuleOwnerCustomField)
                {
                    result = rules.ToList();
                    return true;
                }

                // mirrors rule_custom_fields: { _is_null: false } in the real query, which drops the null of the
                // jsonb column and nothing else: a rule the source cannot map never reaches the mapper, which
                // is what makes an empty result ambiguous by itself
                if (query == RuleQueries.getRulesForOwnerMappingCustomField)
                {
                    result = rules.Where(rule => rule.CustomFields is not null).ToList();
                    return true;
                }

                if (query == RuleQueries.countActiveRulesForOwnerMapping)
                {
                    result = new AggregateCount { Aggregate = new Aggregate { Count = rules.Count } };
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
                    // like the real table: an alert stays open until somebody acknowledges it
                    result = openAlerts.Select(alert => new Alert { Id = alert.Id, AlertCode = alert.AlertCode, Description = alert.Description }).ToList();
                    return true;
                }

                if (query == MonitorQueries.addLogEntry)
                {
                    result = new ReturnIdWrapper { ReturnIds = AlertReturnIds };
                    return true;
                }

                if (query == MonitorQueries.addAlert)
                {
                    string description = ReadString(variables, "description");
                    RaisedAlerts.Add(description);
                    openAlerts.Add(new Alert { Id = nextAlertId++, AlertCode = AlertCode.RuleOwnerMapping, Description = description });
                    result = new ReturnIdWrapper { ReturnIds = AlertReturnIds };
                    return true;
                }

                if (query == MonitorQueries.acknowledgeAlert)
                {
                    // SetAlert acknowledges the older alert for the same problem, so the open list keeps
                    // exactly one entry per condition and that entry is always the most recent one
                    long acknowledgedId = ReadLong(variables, "id");
                    openAlerts.RemoveAll(alert => alert.Id == acknowledgedId);
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
