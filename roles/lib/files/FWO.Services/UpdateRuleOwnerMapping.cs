using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;
using NetTools;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace FWO.Services
{
    public class UpdateRuleOwnerMapping : FWImportChangesNotifierBase<UpdateRuleOwnerMappingEventArgs>
    {
        private const string LogMessageTitle = "Update rule_owner Notifier";
        protected readonly ApiConnection apiConnection;
        protected GlobalConfig globalConfig;

        public UpdateRuleOwnerMapping(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        protected override async Task<bool> Execute(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            await Task.Delay(1000);

            switch ((OwnerMappingSourceStm)globalConfig.OwnerSoruceMappingID)
            {
                case OwnerMappingSourceStm.IpBased:
                    return await UpdateRuleOwnersIpBased(eventArgs);
                case OwnerMappingSourceStm.CustomField:
                    return await UpdateRuleOwnersCustomField(eventArgs);
                case OwnerMappingSourceStm.NameField:
                    return false;
                case OwnerMappingSourceStm.Manual:
                    return false;
                default:
                    return false;
            }
        }

        public async Task HandleEvent(UpdateRuleOwnerMappingEvent evt)
        {
            try
            {
                bool success = await Run(evt.EventArgs);
                evt.EventArgs.Completion?.SetResult(success);
            }
            catch (Exception ex)
            {
                Log.WriteError("UpdateOwnerRuleMappings failed", ex.ToString());
                evt.EventArgs.Completion?.SetException(ex);
            }
        }

        private static async Task<bool> UpdateRuleOwners(Func<Task<bool>> fullReinitFunc, Func<Task<bool>> incrementalFunc, bool isFullReInitialize)
        {
            return isFullReInitialize ? await fullReinitFunc() : await incrementalFunc();
        }

        public async Task<bool> UpdateRuleOwnersCustomField(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            bool isFullReInitialize = eventArgs?.isFullReInitialize ?? false;
            return await UpdateRuleOwners(RunFullReinitializeCustomField, RunIncrementalCustomField, isFullReInitialize);
        }

        public async Task<bool> UpdateRuleOwnersIpBased(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            bool isFullReInitialize = eventArgs?.isFullReInitialize ?? false;
            return await UpdateRuleOwners(RunFullReinitializeIpBased, RunIncrementalIpBased, isFullReInitialize);
        }

        private async Task<bool> RunFullReinitializeCustomField()
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForOwnerMappingCustomField);
            var ownersTask = apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerCustomField);
            await Task.WhenAll(rulesTask, ownersTask);
            var rules = rulesTask.Result;
            var owners = ownersTask.Result;

            var newRuleOwners = BuildNewRuleOwnersCustomField(rules, owners);

            if (!newRuleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert. Aborting import.");
                return false;
            }

            long importControlId = await CreateImportControl();

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = importControlId;
            }

            await SetAllActiveRuleOwnersRemoved(importControlId);
            await InsertNewRuleOwners(newRuleOwners);
            await CompleteImportControlFullReInit(importControlId);

            Log.WriteInfo(LogMessageTitle, "FULL rule_owner reinitialize completed.");
            return true;
        }

        private async Task<bool> RunFullReinitializeIpBased()
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForOwnerMappingIpBased);
            var ownersTask = apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerIpBased);
            await Task.WhenAll(rulesTask, ownersTask);
            var rules = rulesTask.Result;
            var owners = ownersTask.Result;

            var newRuleOwners = BuildNewRuleOwnersIpBased(rules, owners);

            if (!newRuleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert. Aborting import.");
                return false;
            }

            long importControlId = await CreateImportControl();

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = importControlId;
            }

            await SetAllActiveRuleOwnersRemoved(importControlId);
            await InsertNewRuleOwners(newRuleOwners);
            await CompleteImportControlFullReInit(importControlId);

            Log.WriteInfo(LogMessageTitle, "FULL rule_owner reinitialize completed.");
            return true;
        }
        private async Task<bool> RunIncrementalCustomField()
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);

            if (pendingImports == null || !pendingImports.Any())
            {
                return false;
            }

            foreach (var import in pendingImports.OrderBy(i => i.ControlId))
            {
                try
                {
                    await ProcessIncrementalImportCustomField(import);
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while processing import_control {import.ControlId}. ", ex);
                    break;
                }
            }

            return true;
        }
        private async Task<bool> RunIncrementalIpBased()
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);

            if (pendingImports == null || !pendingImports.Any())
            {
                return false;
            }

            foreach (var import in pendingImports.OrderBy(i => i.ControlId))
            {
                try
                {
                    await ProcessIncrementalImportIpBased(import);
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while processing import_control {import.ControlId}. ", ex);
                    break;
                }
            }

            return true;
        }
        private async Task ProcessIncrementalImportCustomField(ImportControl import)
        {
            List<Rule> rulesToMap = new List<Rule>();
            List<FwoOwner> owners = new List<FwoOwner>();
            List<RuleOwner> ruleOwnersToRemove = new List<RuleOwner>();

            switch (import.ImportTypeId)
            {
                case ImportType.RULE:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleRuleImportCustomField(import);
                        break;
                    }
                case ImportType.OWNER:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleOwnerImportCustomField(import);
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException($"ImportType '{import.ImportTypeId}' is not supported in LoadRulesAndOwnersAsync.");
                    }
            }

            var newRuleOwners = BuildNewRuleOwnersCustomField(rulesToMap, owners);

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = import.ControlId;
            }

            await SetAffectedRuleOwnersRemoved(ruleOwnersToRemove, import.ControlId);

            await InsertNewRuleOwners(newRuleOwners);

            await CompleteImportControl(import.ControlId);
        }

        private async Task ProcessIncrementalImportIpBased(ImportControl import)
        {
            List<Rule> rulesToMap = new List<Rule>();
            List<FwoOwner> owners = new List<FwoOwner>();
            List<RuleOwner> ruleOwnersToRemove = new List<RuleOwner>();

            switch (import.ImportTypeId)
            {
                case ImportType.RULE:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleRuleImportIpBased(import);
                        break;
                    }
                case ImportType.OWNER:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleOwnerImportIpBased(import);
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException($"ImportType '{import.ImportTypeId}' is not supported in LoadRulesAndOwnersAsync.");
                    }
            }

            var newRuleOwners = BuildNewRuleOwnersIpBased(rulesToMap, owners);

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = import.ControlId;
            }

            await SetAffectedRuleOwnersRemoved(ruleOwnersToRemove, import.ControlId);

            await InsertNewRuleOwners(newRuleOwners);

            await CompleteImportControl(import.ControlId);
        }

        private async Task<long> CreateImportControl()
        {
            try
            {
                var result = await apiConnection.SendQueryAsync<InsertImportControl>(ImportQueries.addImportForRuleOwner, new { importTypeId = ImportType.ADMIN_VIA_REINITIALIZE_BTN });

                var firstControl = result.Returning.FirstOrDefault();

                if (firstControl == null)
                {
                    Log.WriteError(LogMessageTitle, "No ImportControl returned. Mutation may have failed.");
                    throw new InvalidOperationException("Failed to create ImportControl. Returning list empty.");
                }

                Log.WriteInfo(LogMessageTitle, $"Created new import control with ID {firstControl.ControlId}.");
                return firstControl.ControlId;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, "Error while creating a new import control.", exception);
                throw;
            }
        }

        private async Task SetAllActiveRuleOwnersRemoved(long controlId)
        {
            try
            {
                await apiConnection.SendQueryAsync<RuleOwnerMutationWrapper>(OwnerQueries.setAllActiveRuleOwnersRemoved, new { controlId });
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while marking all active rule owners as removed.", ex);
                throw;
            }
        }

        private async Task SetAffectedRuleOwnersRemoved(List<RuleOwner> ruleOwnersToSetRemoved, long importControlId)
        {
            try
            {
                if (!ruleOwnersToSetRemoved.Any()) return;

                var listRuleOwnersToRemove = ruleOwnersToSetRemoved
                .Select(r => new
                {
                    rule_id = new { _eq = r.RuleId },
                    owner_id = new { _eq = r.OwnerId },
                    created = new { _eq = r.Created }
                })
                .ToList();

                await apiConnection.SendQueryAsync<RuleOwnerMutationWrapper>(OwnerQueries.setAffectedRuleOwnersRemoved,
                    new
                    {
                        objects = listRuleOwnersToRemove,
                        removed = importControlId
                    });
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle,
                    "Error while marking affected rule owners as removed.", ex);
                throw;
            }
        }

        public List<RuleOwner> BuildNewRuleOwnersCustomField(List<Rule> rulesToMap, List<FwoOwner> ownersToMap)
        {
            // create a dictionary for owner name to id mapping for faster lookup
            var ownerNameToIdMap = ownersToMap.Where(o => !string.IsNullOrWhiteSpace(o.ExtAppId))
                                              .ToDictionary(o => o.ExtAppId!, o => o.Id);
            var newRuleOwners = new List<RuleOwner>();

            // iterate through rules and create new mappings based on CustomFields
            foreach (Rule rule in rulesToMap)
            {
                try
                {
                    var customFieldValue = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, globalConfig.CustomFieldOwnerKey);

                    if (!string.IsNullOrWhiteSpace(customFieldValue) && ownerNameToIdMap.TryGetValue(customFieldValue, out var ownerId))
                    {
                        newRuleOwners.Add(new RuleOwner
                        {
                            RuleId = rule.Id,
                            OwnerId = ownerId,
                            RuleMetadataId = rule.Metadata.Id,
                            OwnerMappingSourceId = (int)OwnerMappingSourceStm.CustomField
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteWarning(LogMessageTitle, $"Rule {rule.Id} has invalid CustomFields: {ex.Message}");
                }
            }
            return newRuleOwners;
        }

        public List<RuleOwner> BuildNewRuleOwnersIpBased(List<Rule> rulesToMap, List<FwoOwner> ownersToMap)
        {
            var newRuleOwners = new List<RuleOwner>();
            var ownerNetworksPrepared = PrepareOwnerNetworks(ownersToMap);

            foreach (var rule in rulesToMap)
            {
                var matchesByOwner = GetMatchingOwnerIds(rule, ownerNetworksPrepared);

                foreach (var matchByOwner in matchesByOwner)
                {
                    var minimalMatchedObjectsForOwner = matchByOwner.Value.ToDictionary(
                        dirKvp => dirKvp.Key,
                        dirKvp => dirKvp.Value.Select(o => new { o.Id, o.Name, o.IP, o.IpEnd }).ToList()
                    );

                    newRuleOwners.Add(new RuleOwner
                    {
                        RuleId = rule.Id,
                        OwnerId = matchByOwner.Key,
                        RuleMetadataId = rule.Metadata.Id,
                        OwnerMappingSourceId = (int)OwnerMappingSourceStm.IpBased,
                        MatchedObjects = JsonSerializer.Serialize(minimalMatchedObjectsForOwner)
                    });
                }
            }

            return newRuleOwners;
        }

        private async Task InsertNewRuleOwners(List<RuleOwner> ruleOwners)
        {
            if (!ruleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert.");
                return;
            }

            try
            {
                await apiConnection.SendQueryAsync<RuleOwnerMutationWrapper>(OwnerQueries.insertRuleOwners, new { objects = ruleOwners });
                Log.WriteInfo(LogMessageTitle, $"{ruleOwners.Count} rule owners inserted successfully.");
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while inserting new rule owners.", ex);
                throw;
            }
        }

        private async Task<(List<Rule> rulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportCustomField(ImportControl import)
        {
            var changelogRules = await apiConnection.SendQueryAsync<List<RuleChange>>(RuleQueries.getChangedRulesForRuleOwnerMappingCustomField, new { controlId = import.ControlId });
            if (changelogRules == null || !changelogRules.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            var relevantChanges = changelogRules
                .Where(IsOwnerSourceFieldChanged)
                .ToList();

            var rulesToMap = relevantChanges
                .Select(c => c.NewRule)
                .Where(r => r != null)
                .ToList();

            var rulesToRemove = relevantChanges
                .Select(c => c.OldRule)
                .Where(r => r != null)
                .ToList();

            var owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerCustomField);

            var ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByRule, new { ruleIds = rulesToRemove.Select(r => r.Id).ToList() });

            return (rulesToMap, owners, ruleOwnersToRemove);
        }
        private async Task<(List<Rule> rulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportIpBased(ImportControl import)
        {
            var changelogRules = await apiConnection.SendQueryAsync<List<RuleChange>>(RuleQueries.getChangedRulesForRuleOwnerMappingIpBased, new { controlId = import.ControlId });
            if (changelogRules == null || !changelogRules.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            var relevantChanges = changelogRules
                .Where(IsIpBasedObjectChanged)
                .ToList();

            var rulesToMap = relevantChanges
                .Select(c => c.NewRule)
                .Where(r => r != null)
                .ToList();

            var rulesToRemove = relevantChanges
                .Select(c => c.OldRule)
                .Where(r => r != null)
                .ToList();

            var owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerIpBased);

            var ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByRule, new { ruleIds = rulesToRemove.Select(r => r.Id).ToList() });

            return (rulesToMap, owners, ruleOwnersToRemove);
        }

        private static bool IsIpBasedObjectChanged(RuleChange ruleChange)
        {
            var oldRule = ruleChange.OldRule;
            var newRule = ruleChange.NewRule;

            if (IsObjectListChanged(oldRule?.Tos, newRule?.Tos))
            {
                return true;
            }

            if (IsObjectListChanged(oldRule?.Froms, newRule?.Froms))
            {
                return true;
            }
            return false;
        }

        private static bool IsObjectListChanged(NetworkLocation[]? oldList, NetworkLocation[]? newList)
        {
            oldList ??= Array.Empty<NetworkLocation>();
            newList ??= Array.Empty<NetworkLocation>();

            if (oldList.Length != newList.Length)
            {
                return true;
            }

            return oldList
                .Where(o => o.Object != null)
                .Any(oldEntry => !newList
                    .Any(n => n.Object != null &&
                              n.Object.Id == oldEntry.Object.Id &&
                              n.Object.IP == oldEntry.Object.IP &&
                              n.Object.IpEnd == oldEntry.Object.IpEnd));
        }

        public bool IsOwnerSourceFieldChanged(RuleChange ruleChange)
        {
            var oldFields = DeserializeCustomFields(ruleChange.OldRule?.CustomFields);
            var newFields = DeserializeCustomFields(ruleChange.NewRule?.CustomFields);

            oldFields.TryGetValue(globalConfig.CustomFieldOwnerKey, out var oldValue);
            newFields.TryGetValue(globalConfig.CustomFieldOwnerKey, out var newValue);

            return !string.Equals(oldValue, newValue, StringComparison.Ordinal);
        }

        public static Dictionary<string, string> DeserializeCustomFields(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(raw.Replace("'", "\"")) ?? new();
            }
            catch (JsonException)
            {
                return new();
            }
        }

        private async Task<(List<Rule> RulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleOwnerImportCustomField(ImportControl import)
        {
            var changelogOwners = await apiConnection.SendQueryAsync<List<OwnerChange>>(OwnerQueries.getChangedOwnersForRuleOwnerMappingCustomField, new { controlId = import.ControlId });

            var ownersToAdd = new List<FwoOwner>();
            var ownersToRemove = new List<FwoOwner>();
            var ownersToUpdate = new List<FwoOwner>();

            var rulesToMap = new List<Rule>();
            var ruleOwnersToRemove = new List<RuleOwner>();

            if (!ProcessOwnerChanges(changelogOwners, ownersToAdd, ownersToRemove, ownersToUpdate))
            {
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            if (ownersToAdd.Any())
            {
                rulesToMap = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwnerCustomField);
            }
            else if (ownersToUpdate.Any())
            {
                rulesToMap = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwnerByOwnerToUpdateCustomField, new { ownerIds = ownersToUpdate.Select(o => o.Id).ToList() });
            }
            if (ownersToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByOwner, new { ownerIds = ownersToRemove.Select(o => o.Id).ToList() });
            }
            return (rulesToMap, ownersToAdd.Concat(ownersToUpdate).ToList(), ruleOwnersToRemove);
        }

        private async Task<(List<Rule> RulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleOwnerImportIpBased(ImportControl import)
        {
            var changelogOwners = await apiConnection.SendQueryAsync<List<OwnerChange>>(OwnerQueries.getChangedOwnersForRuleOwnerMappingIpBased, new { controlId = import.ControlId });
            var ownersToAdd = new List<FwoOwner>();
            var ownersToRemove = new List<FwoOwner>();
            var ruleOwnersToRemove = new List<RuleOwner>();
            var rulesToMap = new List<Rule>();

            if (!ProcessOwnerChanges(changelogOwners, ownersToAdd, ownersToRemove, new List<FwoOwner>(), true))
            {
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            if (ownersToAdd.Any())
            {
                rulesToMap = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwnerIpBased);
            }

            if (ownersToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByOwner, new { ownerIds = ownersToRemove.Select(o => o.Id).ToList() });
            }
            return (rulesToMap, ownersToAdd, ruleOwnersToRemove);
        }

        private static bool ProcessOwnerChanges(List<OwnerChange> changelogOwners, List<FwoOwner> ownersToAdd, List<FwoOwner> ownersToRemove, List<FwoOwner> ownersToUpdate, bool changeMeansAddAndRemove = false)
        {
            if (changelogOwners == null || !changelogOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                return false;
            }
            foreach (var change in changelogOwners)
            {
                switch (change.ChangeAction)
                {
                    case ChangelogActionType.INSERT:
                    case ChangelogActionType.REACTIVATE:
                        ownersToAdd.Add(change.NewOwner);
                        break;

                    case ChangelogActionType.DELETE:
                    case ChangelogActionType.DEACTIVATE:
                        ownersToRemove.Add(change.OldOwner);
                        break;

                    case ChangelogActionType.CHANGE:
                        if (changeMeansAddAndRemove)
                        {
                            ownersToAdd.Add(change.NewOwner);
                            ownersToRemove.Add(change.NewOwner);
                        }
                        else
                        {
                            ownersToUpdate.Add(change.NewOwner);
                        }
                        break;
                }
            }

            return true;
        }

        private async Task CompleteImportControl(long importControlId)
        {
            try
            {
                await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerInc,
                new
                {
                    controlId = importControlId,
                    rule_owner_mapping_done = true
                }
            );
                Log.WriteInfo(LogMessageTitle, $"Import control {importControlId} completed successfully.");
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
            }
        }

        private async Task CompleteImportControlFullReInit(long importControlId)
        {
            try
            {
                await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerFull,
                new
                {
                    controlId = importControlId,
                    stopTime = DateTime.UtcNow,
                    successful = true,
                    rule_owner_mapping_done = true
                });

                Log.WriteInfo(LogMessageTitle, $"Import control {importControlId} completed successfully.");

                await CompleteOlderPendingImports(importControlId);
            }
            catch (Exception ex)
            {
                Log.WriteError(LogMessageTitle, "Error while updating import control completion status.", ex);
            }
        }

        private async Task CompleteOlderPendingImports(long referenceControlId)
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);

            if (pendingImports == null || !pendingImports.Any())
            {
                return;
            }

            var olderImports = pendingImports.Where(i => i.ControlId < referenceControlId);

            foreach (var import in olderImports)
            {
                try
                {
                    await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerInc,
                        new
                        {
                            controlId = import.ControlId,
                            rule_owner_mapping_done = true
                        });

                    Log.WriteInfo(LogMessageTitle, $"Older import control {import.ControlId} marked as rule_owner_mapping_done.");
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while updating older import_control {import.ControlId}.", ex);
                }
            }
        }

        public static (IPAddressRange? range, AddressFamily? ipVersion) GetIpRangeAndVersion(string ipStart, string ipEnd)
        {
            var start = ipStart.StripOffUnnecessaryNetmask();
            var end = ipEnd.StripOffUnnecessaryNetmask();

            if (!IPAddress.TryParse(start, out var startIp))
            {
                Log.WriteError(LogMessageTitle, $"Invalid start IP: {start}");
                return (null, null);
            }

            if (!IPAddress.TryParse(end, out var endIp))
            {
                Log.WriteError(LogMessageTitle, $"Invalid end IP: {end}");
                return (null, null);
            }

            if (startIp.AddressFamily != endIp.AddressFamily)
            {
                Log.WriteError(LogMessageTitle, $"IP families do not match: {start}-{end}");
                return (null, null);
            }

            // compare start and end IPs to ensure start is less than or equal to end
            int cmp = 0;
            if (startIp.AddressFamily == AddressFamily.InterNetwork)
            {
                cmp = IpOperations.IpToUint(startIp).CompareTo(IpOperations.IpToUint(endIp));
            }
            else if (startIp.AddressFamily == AddressFamily.InterNetworkV6)
            {
                cmp = IpOperations.ToBigInteger(startIp).CompareTo(IpOperations.ToBigInteger(endIp));
            }
            else
            {
                Log.WriteError(LogMessageTitle, "Unsupported AddressFamily");
                return (null, null);
            }

            if (cmp > 0)
            {
                Log.WriteError(LogMessageTitle, $"Invalid range: {start}-{end} (start > end)");
                return (null, null);
            }

            var range = IpOperations.GetIPAdressRange($"{start}-{end}");
            return (range, range.Begin.AddressFamily);
        }

        public List<OwnerNetworkPrepared> PrepareOwnerNetworks(List<FwoOwner> ownersToMap)
        {
            return ownersToMap
                    .Where(o => o.OwnerNetworks != null && o.OwnerNetworks.Any())
                    .Select(o => new OwnerNetworkPrepared
                    {
                        OwnerId = o.Id,
                        Ranges = o.OwnerNetworks
                            .Where(nw => !string.IsNullOrWhiteSpace(nw.IP) && !string.IsNullOrWhiteSpace(nw.IpEnd))
                            .Select(nw =>
                            {
                                if (!nw.IP.TryParseIPStringToRange(out var _))
                                {
                                    Console.WriteLine($"Ung�ltige IP in Regel {o.Id}: {nw.IP} - {nw.IpEnd}");
                                }

                                var (range, version) = GetIpRangeAndVersion(nw.IP, nw.IpEnd);

                                if (range == null || version == null)
                                {
                                    Log.WriteWarning(LogMessageTitle, $"Skipping invalid owner network: {nw.IP}-{nw.IpEnd} for Owner {o.Id}");
                                    return null;
                                }

                                return new OwnerRange
                                {
                                    Range = range,
                                    IpVersion = version
                                };
                            })
                            .Where(x => x != null)
                            .ToList()
                    })
                    .ToList();
        }

        public static Dictionary<int, Dictionary<string, List<NetworkObject>>> GetMatchingOwnerIds(Rule rule, List<OwnerNetworkPrepared> ownerNetworksPrepared)
        {
            var matchesByOwner = new Dictionary<int, Dictionary<string, List<NetworkObject>>>();

            var ruleNetworksWithDirections = rule.Froms.Where(n => n?.Object != null).Select(n => (Obj: n.Object!, Direction: "From"))
                                                       .Concat
                                             (rule.Tos.Where(n => n?.Object != null).Select(n => (Obj: n.Object!, Direction: "To"))).ToList();

            if (!ruleNetworksWithDirections.Any())
            {
                Log.WriteWarning(LogMessageTitle, $"Rule {rule.Id} has no network locations and will be skipped.");
                return matchesByOwner;
            }

            // Iterate through each network location of the rule
            foreach (var (obj, direction) in ruleNetworksWithDirections)
            {
                if (obj == null || string.IsNullOrWhiteSpace(obj.IP) || string.IsNullOrWhiteSpace(obj.IpEnd))
                {
                    continue;
                }

                var (ruleRange, ruleIpVersion) = GetIpRangeAndVersion(obj.IP, obj.IpEnd);

                if (ruleRange == null || ruleIpVersion == null)
                {
                    continue;
                }

                var matchingOwnerIds = ownerNetworksPrepared
                .Where(owner => owner.Ranges.Any(r => r != null
                                                      && r.IpVersion == ruleIpVersion
                                                      && IpOperations.RangeOverlapExists(ruleRange, r.Range)))
                .Select(owner => owner.OwnerId);

                foreach (var ownerId in matchingOwnerIds)
                {
                    var dictForOwner = matchesByOwner.GetValueOrDefault(ownerId) ?? new Dictionary<string, List<NetworkObject>>();
                    if (!dictForOwner.ContainsKey(direction))
                        dictForOwner[direction] = new List<NetworkObject>();

                    dictForOwner[direction].Add(obj);
                    matchesByOwner[ownerId] = dictForOwner;
                }
            }
            return matchesByOwner;
        }

        public class OwnerNetworkPrepared
        {
            public int OwnerId { get; set; }
            public List<OwnerRange?> Ranges { get; set; } = new List<OwnerRange?>();
        }

        public class OwnerRange
        {
            public IPAddressRange Range { get; set; } = default!;
            public AddressFamily? IpVersion { get; set; }
        }
    }
}
