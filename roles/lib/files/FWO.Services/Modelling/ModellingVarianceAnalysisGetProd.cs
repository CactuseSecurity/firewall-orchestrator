using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FWO.Services.Modelling
{
    /// <summary>
    /// Part of Variance Analysis Class getting the production state
    /// </summary>
    public partial class ModellingVarianceAnalysis
    {
        private const int kRuleOwnerPollIntervalSeconds = 2;
        private const int kRuleOwnerRebuildMaxAgeHours = 2;
        private const int kDefaultRulesPerFetch = 100;

        private RuleOwnerPrefilterState? ruleOwnerPrefilterState;
        private HashSet<long>? NameFieldRuleOwnerConnectionIds { get; set; }
        private sealed record RelevantImportContext(long? ImportId, HashSet<int> ManagementIds);

        private async Task InitManagements()
        {
            try
            {
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);
                managements = [.. managements.Where(m => !string.IsNullOrEmpty(m.ExtMgtData))];
                RelevantManagements = [];
                foreach (Management mgt in managements)
                {
                    ExtMgtData extMgtData = JsonSerializer.Deserialize<ExtMgtData>(mgt.ExtMgtData ?? "");
                    if (!string.IsNullOrEmpty(extMgtData.ExtId) || !string.IsNullOrEmpty(extMgtData.ExtName))
                    {
                        RelevantManagements.Add(mgt);
                        if (!alreadyCreatedAppServers.ContainsKey(mgt.Id))
                        {
                            alreadyCreatedAppServers.Add(mgt.Id, []);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("managements"), "Init Managements leads to error: ", exception);
            }
        }

        private async Task<bool> GetModelledRulesProductionState(ModellingFilter modellingFilter)
        {
            try
            {
                int modelledRulesCount = 0;
                int notModelledRulesCount = 0;
                allModelledRules = [];

                foreach (Management mgt in RelevantManagements)
                {
                    varianceResult.UnModelledRules.Add(mgt.Id, []);
                    List<Rule>? rulesByMgt = await GetRules(mgt.Id, modellingFilter);
                    if (rulesByMgt != null)
                    {
                        IdentifyModelledRules(mgt, rulesByMgt);
                        modelledRulesCount += allModelledRules[mgt.Id].Count;
                        notModelledRulesCount += varianceResult.UnModelledRules[mgt.Id].Count;
                    }
                }
                Log.WriteDebug("GetModelledRulesProductionState", $"Found {modelledRulesCount} modelled rules, {notModelledRulesCount} others.");
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("load_rules"), "Get Production State leads to error: ", exception);
                displayMessageInUi(exception, userConfig.GetText("load_rules"), "Get Production State leads to error: ", true);
                return false;
            }
            return true;
        }

        private void IdentifyModelledRules(Management mgt, List<Rule> rulesByMgt)
        {
            allModelledRules.Add(mgt.Id, []);
            foreach (var rule in rulesByMgt)
            {
                rule.ManagementName = mgt.Name;
                rule.DeviceName = string.Join(", ", rule.EnforcingGateways.Select(g => g.Content.Name).Where(n => !string.IsNullOrEmpty(n)));
                string? connRef = FindModelledMarker(rule);
                if (connRef != null)
                {
                    if (long.TryParse(connRef, out long connId))
                    {
                        rule.ConnId = connId;
                    }
                    allModelledRules[mgt.Id].Add(rule);
                }
                else
                {
                    varianceResult.UnModelledRules[mgt.Id].Add(rule);
                }
            }
        }

        private string? FindModelledMarker(Rule rule)
        {
            return userConfig.ModModelledMarkerLocation switch
            {
                MarkerLocation.Rulename => !string.IsNullOrEmpty(rule.Name) && rule.Name.Contains(userConfig.ModModelledMarker) ? ParseFromString(rule.Name) : null,
                MarkerLocation.Comment => !string.IsNullOrEmpty(rule.Comment) && rule.Comment.Contains(userConfig.ModModelledMarker) ? ParseFromString(rule.Comment) : null,
                MarkerLocation.Customfields => !string.IsNullOrEmpty(rule.CustomFields) ? GetFromCustomField(rule) : null,
                _ => null,
            };
        }

        [GeneratedRegex("[^0-9]")]
        private static partial Regex NonNumericRegex();

        private string? ParseFromString(string FieldString)
        {
            int idx = FieldString.IndexOf(userConfig.ModModelledMarker) + userConfig.ModModelledMarker.Length;
            if (idx >= 0 && idx < FieldString.Length)
            {
                int? contentLength = NonNumericRegex().Match(FieldString[idx..]).Captures.FirstOrDefault()?.Index;
                return contentLength != null && contentLength > 0 ? FieldString.Substring(idx, (int)contentLength) : FieldString.Substring(idx);
            }
            return null;
        }

        private string? GetFromCustomField(Rule rule)
        {
            Dictionary<string, string>? customFields = JsonSerializer.Deserialize<Dictionary<string, string>>(rule.CustomFields);
            return customFields != null && customFields.TryGetValue(userConfig.ModModelledMarker, out string? value) ? value : null;
        }

        private async Task GetDeletedConnections()
        {
            try
            {
                DeletedConns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getDeletedConnections, new { appId = owner.Id });
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("connections"), "Get deleted connections leads to error: ", exception);
                displayMessageInUi(exception, userConfig.GetText("connections"), "Get deleted connections leads to error: ", true);
            }
        }

        private async Task<List<Rule>?> GetRules(int mgtId, ModellingFilter modellingFilter)
        {
            RelevantImportContext importContext = await GetRelevantImportContext(mgtId);
            long? relImpId = importContext.ImportId;
            await GetRuleDevices(mgtId, modellingFilter);

            if (relImpId != null && ShouldUseNameFieldRuleOwnerPreFilter(modellingFilter))
            {
                List<Rule>? preFilteredRules = await TryGetPreFilteredRules(mgtId, relImpId.Value, importContext.ManagementIds, modellingFilter);
                if (preFilteredRules != null)
                {
                    return preFilteredRules;
                }
            }

            return await GetRulesViaMarker(mgtId, relImpId, modellingFilter);
        }

        /// <summary>
        /// Loads the rules the way the analysis always did: by marker, or by loading every rule of the
        /// management when remaining rules are analysed. This is the standard path for every mapping
        /// source except NameField, and the fallback within it.
        /// </summary>
        /// <param name="mgtId">Management to load from.</param>
        /// <param name="relImpId">Import that defines the state to read.</param>
        /// <param name="modellingFilter">Filter of the running analysis.</param>
        /// <returns>The loaded rules.</returns>
        private async Task<List<Rule>?> GetRulesViaMarker(int mgtId, long? relImpId, ModellingFilter modellingFilter)
        {
            if (modellingFilter.AnalyseRemainingRules)
            {
                Dictionary<string, object?> allRuleVariables = new()
                {
                    ["mgmId"] = mgtId,
                    ["import_id_start"] = relImpId,
                    ["import_id_end"] = relImpId
                };
                return await SendPagedRuleQuery(RuleQueries.getRulesByManagement, allRuleVariables);
            }

            Dictionary<string, object?> markerVariables = new()
            {
                ["mgmId"] = mgtId,
                ["import_id_start"] = relImpId,
                ["import_id_end"] = relImpId,
                ["marker"] = $"%{userConfig.ModModelledMarker}%"
            };

            string query = userConfig.ModModelledMarkerLocation switch
            {
                MarkerLocation.Rulename => RuleQueries.getModelledRulesByManagementName,
                MarkerLocation.Comment => RuleQueries.getModelledRulesByManagementComment,
                _ => throw new NotSupportedException("invalid or undefined Marker Location")
            };

            return await SendPagedRuleQuery(query, markerVariables);
        }

        /// <summary>
        /// Loads the result of a rule query page by page. The rule fragments expand the flattened group
        /// members of every referenced object, so a single unpaged response grows with rulebase size times
        /// group size and can reach a size the transport does not deliver (#5301). Paging bounds each
        /// response by the page size instead. All pages are read for the same import, so the result is
        /// the same as that of one unpaged query.
        /// </summary>
        /// <param name="query">Rule query accepting $limit and $offset, with a total order on its result.</param>
        /// <param name="variables">Query variables without limit and offset, which are set here.</param>
        /// <returns>All rules matching the query.</returns>
        private async Task<List<Rule>> SendPagedRuleQuery(string query, Dictionary<string, object?> variables)
        {
            int pageSize = userConfig.ElementsPerFetch > 0 ? userConfig.ElementsPerFetch : kDefaultRulesPerFetch;
            List<Rule> allRules = [];
            int offset = 0;
            List<Rule> page;
            do
            {
                variables["limit"] = pageSize;
                variables["offset"] = offset;
                page = await apiConnection.SendQueryAsync<List<Rule>>(query, variables) ?? [];
                allRules.AddRange(page);
                offset += pageSize;
            }
            while (page.Count == pageSize);
            return allRules;
        }

        /// <summary>
        /// Uses the NameField rule_owner mapping as a prefilter when marker-based modelled
        /// rule loading is sufficient. AnalyseRemainingRules must inspect rules beyond the
        /// current owner's mapped modelled rules and therefore keeps using the full rule load.
        /// Deleted model references can use the prefilter because removed modelling
        /// connections stay included in the NameField rule_owner mapping.
        /// </summary>
        private bool ShouldUseNameFieldRuleOwnerPreFilter(ModellingFilter modellingFilter)
        {
            return userConfig.OwnerSoruceMappingID == (int)OwnerMappingSourceStm.NameField
                && userConfig.ModModelledMarkerLocation == MarkerLocation.Rulename
                && owner.Id > 0
                && !string.IsNullOrWhiteSpace(userConfig.ModModelledMarker)
                && !modellingFilter.AnalyseRemainingRules;
        }

        /// <summary>
        /// Runs the checks that decide whether the rule_owner mapping may be used as a prefilter and,
        /// if they pass, the prefilter itself.
        /// </summary>
        /// <param name="mgtId">Management to load from.</param>
        /// <param name="relImpId">Import that defines the state to read.</param>
        /// <param name="managementIds">Management and sub management ids the analysis reads.</param>
        /// <param name="modellingFilter">Filter of the running analysis.</param>
        /// <returns>The prefiltered rules, or null when the marker query has to be used instead.</returns>
        private async Task<List<Rule>?> TryGetPreFilteredRules(int mgtId, long relImpId, HashSet<int> managementIds, ModellingFilter modellingFilter)
        {
            RuleOwnerPrefilterState? state = await GetUsableRuleOwnerState(mgtId, managementIds, modellingFilter);
            if (state == null)
            {
                return null;
            }

            if (!await IsRuleOwnerPreFilterCompletenessVerified(mgtId, relImpId, modellingFilter))
            {
                return null;
            }

            return await RunNameFieldRuleOwnerPreFilter(mgtId, relImpId, state);
        }

        /// <summary>
        /// Reads the mapping state and decides whether it may be trusted. A pending import is waited
        /// out once where somebody is waiting for the result, because the mapping job usually closes
        /// that gap within its interval - far quicker than the marker query would finish.
        /// </summary>
        /// <param name="mgtId">Management to load from.</param>
        /// <param name="managementIds">Management and sub management ids the analysis reads.</param>
        /// <param name="modellingFilter">Filter of the running analysis.</param>
        /// <returns>The usable state, or null when the marker query has to be used.</returns>
        private async Task<RuleOwnerPrefilterState?> GetUsableRuleOwnerState(int mgtId, HashSet<int> managementIds, ModellingFilter modellingFilter)
        {
            RuleOwnerPrefilterState? state = await GetRuleOwnerPrefilterState();
            string? blocker = DescribeUnusableState(state, managementIds);
            if (blocker == null)
            {
                return state;
            }

            // only an unprocessed import can resolve itself while we watch - a rebuild has to finish
            // and an unreadable state will not fix itself within the wait time
            if (state != null && !state.RebuildRunning)
            {
                state = await WaitForRuleOwnerMapping(managementIds, modellingFilter);
                blocker = DescribeUnusableState(state, managementIds);
                if (blocker == null)
                {
                    return state;
                }
            }

            await ReportPreFilterFallback(mgtId, blocker);
            return null;
        }

        /// <summary>
        /// Names why the mapping state cannot be used, or null when it can. Keeping this in one place
        /// makes sure the reason that ends up in the log is the one that actually applies, also after
        /// waiting - where a failed query or a rebuild that started meanwhile would otherwise be
        /// reported as a pending import.
        /// </summary>
        /// <param name="state">State read from the database, or null if it could not be read.</param>
        /// <param name="managementIds">Management and sub management ids the analysis reads.</param>
        /// <returns>The reason, or null if the state is usable.</returns>
        private static string? DescribeUnusableState(RuleOwnerPrefilterState? state, HashSet<int> managementIds)
        {
            if (state == null)
            {
                return "the rule_owner mapping state could not be read";
            }

            if (state.RebuildRunning)
            {
                return "a full rule_owner reinitialize is rebuilding the mapping";
            }

            if (state.HasPendingImportFor(managementIds))
            {
                return "imports are still waiting for their rule_owner mapping";
            }

            return null;
        }

        /// <summary>
        /// Checks whether this analysis may wait for the mapping job at all. Only callers where
        /// somebody waits for the result allow it, and only once per <see cref="WaitState"/>, so a run
        /// over several managements - or over several owners sharing one state - cannot accumulate
        /// wait times.
        /// </summary>
        /// <param name="modellingFilter">Filter of the running analysis.</param>
        /// <returns>True if waiting is allowed right now.</returns>
        private bool MayWaitForRuleOwnerMapping(ModellingFilter modellingFilter)
        {
            return modellingFilter.AllowWaitForRuleOwnerMapping
                && userConfig.VarianceNameFieldWaitTime > 0
                && !WaitState.WaitDone;
        }

        /// <summary>
        /// Polls the mapping state until the pending imports are processed or the configured wait time
        /// is used up. Polling rather than waiting the full time keeps the delay at the actual gap,
        /// which is usually far shorter than the configured maximum.
        /// </summary>
        /// <param name="managementIds">Management and sub management ids the analysis reads.</param>
        /// <param name="modellingFilter">Filter of the running analysis.</param>
        /// <returns>The state after waiting, or null if it could not be read.</returns>
        private async Task<RuleOwnerPrefilterState?> WaitForRuleOwnerMapping(HashSet<int> managementIds, ModellingFilter modellingFilter)
        {
            if (!MayWaitForRuleOwnerMapping(modellingFilter))
            {
                return ruleOwnerPrefilterState;
            }

            WaitState.WaitDone = true;
            int attempts = (int)Math.Ceiling((double)userConfig.VarianceNameFieldWaitTime / kRuleOwnerPollIntervalSeconds);
            TimeSpan pollInterval = TimeSpan.FromSeconds(kRuleOwnerPollIntervalSeconds);

            for (int attempt = 0; attempt < attempts && !CancellationToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    await DelayAsync(pollInterval, CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // navigating away cancels the wait; the analysis still finishes over the marker
                    // query, so this must not travel up as an error
                    return ruleOwnerPrefilterState;
                }

                ruleOwnerPrefilterState = null;
                RuleOwnerPrefilterState? state = await GetRuleOwnerPrefilterState();
                if (state == null || (!state.RebuildRunning && !state.HasPendingImportFor(managementIds)))
                {
                    return state;
                }
            }

            return ruleOwnerPrefilterState;
        }

        /// <summary>
        /// Reads the rule_owner mapping state and caches it for this analysis. The cache is dropped
        /// while waiting, so every poll sees the current state.
        /// <para>
        /// Three separate queries rather than one with aliases, following the one query per file
        /// convention. They are small enough that reading them in sequence costs nothing measurable
        /// against the marker query this whole check exists to avoid.
        /// </para>
        /// </summary>
        /// <returns>The state, or null if it could not be read.</returns>
        private async Task<RuleOwnerPrefilterState?> GetRuleOwnerPrefilterState()
        {
            if (ruleOwnerPrefilterState != null)
            {
                return ruleOwnerPrefilterState;
            }

            try
            {
                var rebuildVariables = new
                {
                    rebuildCutoff = DateTime.Now.AddHours(-kRuleOwnerRebuildMaxAgeHours).ToString("yyyy-MM-dd HH:mm:ss")
                };
                var mappingVariables = new
                {
                    ownerMappingSourceId = (short)(int)OwnerMappingSourceStm.NameField
                };

                List<ImportControl> runningRebuild = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getRunningRuleOwnerRebuild, rebuildVariables) ?? [];
                List<ImportControl> pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleAffectingImports) ?? [];
                List<RuleOwner> existingMapping = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getAnyActiveRuleOwnerMapping, mappingVariables) ?? [];

                ruleOwnerPrefilterState = new()
                {
                    RunningRuleOwnerRebuild = runningRebuild,
                    PendingRuleAffectingImports = pendingImports,
                    MappingExists = existingMapping.Count > 0
                };
                return ruleOwnerPrefilterState;
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Variance Rule Loading",
                    $"Could not read the rule_owner mapping state for owner {owner.Id}. Falling back to marker query. {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// Runs the prefilter query and decides what an empty result means. Empty is a valid answer
        /// once the mapping exists at all - the owner simply has nothing on this management. Without
        /// any mapping it says nothing, so the marker query has to answer instead.
        /// </summary>
        /// <param name="mgtId">Management to load from.</param>
        /// <param name="relImpId">Import that defines the state to read.</param>
        /// <param name="state">Mapping state read before.</param>
        /// <returns>The prefiltered rules, or null when the marker query has to be used.</returns>
        private async Task<List<Rule>?> RunNameFieldRuleOwnerPreFilter(int mgtId, long relImpId, RuleOwnerPrefilterState state)
        {
            List<Rule>? preFilteredRules = await TryGetNameFieldRuleOwnerPrefilteredRules(mgtId, relImpId);
            if (preFilteredRules == null)
            {
                await ReportPreFilterFallback(mgtId, "the rule_owner prefilter query failed");
                return null;
            }

            if (preFilteredRules.Count > 0 || state.MappingExists)
            {
                return preFilteredRules;
            }

            await ReportPreFilterFallback(mgtId, "no rule_owner mapping has been built yet");
            return null;
        }

        /// <summary>
        /// Records that the analysis falls back to the marker query. The debug log gets every occurrence.
        /// The database log gets one entry per reason and analysis, because nearly every reason is a
        /// global state that would otherwise be repeated for each management - and none at all where
        /// <see cref="LogPrefilterFallbackToDb"/> is off. The message to the user is shown once per
        /// analysis, so a run over several managements does not repeat it.
        /// <para>
        /// Only ever reached inside the NameField branch - for the other mapping sources the marker
        /// query is the normal path and there is nothing to report.
        /// </para>
        /// </summary>
        /// <param name="mgtId">Management the analysis was loading.</param>
        /// <param name="reason">Why the prefilter could not be used.</param>
        private async Task ReportPreFilterFallback(int mgtId, string reason)
        {
            Log.WriteDebug("Variance Rule Loading",
                $"Falling back to marker query for owner {owner.Id}, management {mgtId}: {reason}.");
            if (LogPrefilterFallbackToDb && WaitState.LoggedFallbackReasons.Add(reason))
            {
                await AlertHelper.AddLogEntry(apiConnection, 0, reason,
                    $"Variance analysis for owner {owner.Id} used the marker query on management {mgtId}.",
                    GlobalConst.kVarianceRuleOwnerPrefilter, mgtId);
            }

            if (WaitState.FallbackReported)
            {
                return;
            }
            WaitState.FallbackReported = true;
            displayMessageInUi(null, userConfig.GetText("variance_analysis"), userConfig.GetText("U9045"), true);
        }

        private async Task<HashSet<long>> GetNameFieldRuleOwnerConnectionIds()
        {
            if (NameFieldRuleOwnerConnectionIds != null)
            {
                return NameFieldRuleOwnerConnectionIds;
            }

            List<int> ownerIds = new() { owner.Id };
            List<ModellingConnection> ownerConnections =
                await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getOwnersForRuleOwnerNameFieldFilteredByOwner, new { ownerIds }) ?? [];

            NameFieldRuleOwnerConnectionIds = ownerConnections
                .Select(connection => (long)connection.Id)
                .ToHashSet();

            return NameFieldRuleOwnerConnectionIds;
        }

        private Dictionary<string, object?> BuildNameFieldRuleOwnerRuleVariables(int mgtId, long? relImpId, bool includeActive)
        {
            Dictionary<string, object?> ruleVariables = new()
            {
                ["mgmId"] = mgtId,
                ["ownerId"] = owner.Id,
                ["ownerMappingSourceId"] = (short)(int)OwnerMappingSourceStm.NameField,
                ["marker"] = $"%{userConfig.ModModelledMarker}%",
                ["import_id_start"] = relImpId,
                ["import_id_end"] = relImpId
            };

            if (includeActive)
            {
                ruleVariables["active"] = true;
            }

            return ruleVariables;
        }

        private async Task<bool> IsRuleOwnerPreFilterCompletenessVerified(int mgtId, long relImpId, ModellingFilter modellingFilter)
        {
            if (!modellingFilter.VerifyRuleOwnerPreFilterCompleteness)
            {
                return true;
            }

            try
            {
                HashSet<long> ownerConnectionIds = await GetNameFieldRuleOwnerConnectionIds();
                Dictionary<string, object?> ruleVariables = BuildNameFieldRuleOwnerRuleVariables(mgtId, relImpId, includeActive: false);

                List<Rule> markerRules = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getNameFieldRuleOwnerPreFilterCompletenessRules, ruleVariables) ?? [];

                int missingMappingCount = markerRules.Count(rule => long.TryParse(FindModelledMarker(rule), out long connectionId) && ownerConnectionIds.Contains(connectionId));

                if (missingMappingCount > 0)
                {
                    await ReportPreFilterFallback(mgtId, $"{missingMappingCount} owner marker rules have no active rule_owner mapping");
                }

                return missingMappingCount == 0;
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Variance Rule Loading",
                    $"Could not verify NameField rule_owner prefilter completeness for owner {owner.Id}, management {mgtId}. Falling back to marker query. {exception.Message}");
                await ReportPreFilterFallback(mgtId, "the rule_owner prefilter completeness check failed");
                return false;
            }
        }

        private async Task<List<Rule>?> TryGetNameFieldRuleOwnerPrefilteredRules(int mgtId, long? relImpId)
        {
            try
            {
                Dictionary<string, object?> ruleVariables = BuildNameFieldRuleOwnerRuleVariables(mgtId, relImpId, includeActive: true);

                return await SendPagedRuleQuery(RuleQueries.getModelledRulesByRuleOwnerNameField, ruleVariables);
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Variance Rule Loading",
                    $"NameField rule_owner prefilter failed for owner {owner.Id}, management {mgtId}. Falling back to marker query. {exception.Message}");
                return null;
            }
        }

        private async Task GetRuleDevices(int mgtId, ModellingFilter modellingFilter)
        {
            if (modellingFilter.AnalyseRemainingRules || modellingFilter.RulesForDeletedConns)
            {
                DeviceRules[mgtId] = await apiConnection.SendQueryAsync<List<DeviceReport>>(DeviceQueries.getDevicesWithRulebaseLinks, new { mgmId = mgtId });
            }
        }

        private async Task GetNwObjectsProductionState()
        {
            try
            {
                int aRCount = 0;
                int aSCount = 0;
                foreach (var mgtId in RelevantManagements.Select(m => m.Id))
                {
                    aRCount += await CollectGroupObjects(mgtId);
                    aSCount += await CollectAppServers(mgtId);
                }
                Log.WriteDebug("GetNwObjectsProductionState", $"Found {aRCount} AppRoles, {aSCount} AppServer.");
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("network_objects"), "Get Production State leads to error: ", exception);
            }
        }

        private async Task<int> CollectGroupObjects(int mgtId)
        {
            int aRCount = 0;
            List<NetworkObject>? objGrpByMgt = await GetObjects(mgtId, [2]);
            if (objGrpByMgt != null)
            {
                if (!allProdAppRoles.TryGetValue(mgtId, out List<ModellingAppRole>? aRList))
                {
                    aRList = [];
                    allProdAppRoles.Add(mgtId, aRList);
                }
                foreach (NetworkObject objGrp in objGrpByMgt)
                {
                    aRList.Add(new(objGrp, namingConvention));
                    aRCount++;
                }
            }
            return aRCount;
        }

        private async Task<int> CollectAppServers(int mgtId)
        {
            int aSCount = 0;
            List<NetworkObject>? objByMgt = await GetObjects(mgtId, [1, 3, 12]);
            if (objByMgt != null)
            {
                if (!allExistingAppServers.TryGetValue(mgtId, out Dictionary<ModellingAppServer, long>? appServers))
                {
                    appServers = new(appServerComparer);
                    allExistingAppServers.Add(mgtId, appServers);
                }
                foreach (NetworkObject obj in objByMgt)
                {
                    ModellingAppServer appServer = new(obj);
                    appServers.TryAdd(appServer, appServer.Id);
                    aSCount++;
                }
            }
            return aSCount;
        }

        private async Task<List<NetworkObject>?> GetObjects(int mgtId, int[] objTypeIds)
        {
            try
            {
                long? relImpId = await GetRelevantImportId(mgtId);
                if (relImpId != null)
                {
                    var ObjGroupVariables = new
                    {
                        mgmId = mgtId,
                        objTypeIds = objTypeIds,
                        import_id_start = relImpId,
                        import_id_end = relImpId
                    };
                    return await apiConnection.SendQueryAsync<List<NetworkObject>>(ObjectQueries.getNetworkObjectsForManagement, ObjGroupVariables);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("network_objects"), "Get Production Objects leads to error: ", exception);
            }
            return [];
        }

        private async Task<long?> GetRelevantImportId(int mgtId)
        {
            return (await GetRelevantImportContext(mgtId)).ImportId;
        }

        private async Task<RelevantImportContext> GetRelevantImportContext(int mgtId)
        {
            try
            {
                var Variables = new
                {
                    time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    mgmIds = mgtId
                };

                List<ManagementReport> managements = (await apiConnection.SendQueryAsync<List<ManagementReport>>(ReportQueries.getRelevantImportIdsAtTime, Variables))!;
                if (managements.Count == 0)
                {
                    Log.WriteError("GetRelevantImportId", $"No management data found for management ID {mgtId}.");
                    return new(null, new HashSet<int> { mgtId });
                }

                HashSet<int> managementIds = managements.Select(management => management.Id).ToHashSet();
                foreach (ManagementReport management in managements)
                {
                    foreach (Management subManagement in management.SubManagements)
                    {
                        managementIds.Add(subManagement.Id);
                    }
                }
                managementIds.Add(mgtId);

                long importId = managements.Select(management => management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1).Max();

                return new(importId, managementIds);
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("import_id"), "Get ImportIds leads to error: ", exception);
                return new(null, new HashSet<int> { mgtId });
            }
        }
    }
}
