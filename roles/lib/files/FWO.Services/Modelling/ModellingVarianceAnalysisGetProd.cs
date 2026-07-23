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
        private List<ImportControl>? PendingRuleOwnerMappingImports { get; set; }

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

        private async Task<bool> GetModelledRulesProductionState(ModellingFilter modellingFilter, bool useNameFieldRuleOwnerPreFilter = true)
        {
            try
            {
                int modelledRulesCount = 0;
                int notModelledRulesCount = 0;
                allModelledRules = [];

                foreach (Management mgt in RelevantManagements)
                {
                    varianceResult.UnModelledRules.Add(mgt.Id, []);
                    List<Rule>? rulesByMgt = await GetRules(mgt.Id, modellingFilter, useNameFieldRuleOwnerPreFilter);
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

        private async Task<List<Rule>?> GetRules(int mgtId, ModellingFilter modellingFilter, bool useNameFieldRuleOwnerPreFilter)
        {
            long? relImpId = await GetRelevantImportId(mgtId);
            await GetRuleDevices(mgtId, modellingFilter);

            if (relImpId != null && useNameFieldRuleOwnerPreFilter && ShouldUseNameFieldRuleOwnerPreFilter(modellingFilter) && await IsRuleOwnerMappingCurrent(mgtId, relImpId.Value))
            {
                List<Rule>? preFilteredRules = await TryGetNameFieldRuleOwnerPrefilteredRules(mgtId, relImpId);
                if (preFilteredRules?.Count > 0)
                {
                    return preFilteredRules;
                }

                if (preFilteredRules != null)
                {
                    Log.WriteDebug("Variance Rule Loading",
                        $"NameField rule_owner prefilter returned no rules for owner {owner.Id}, management {mgtId}. Falling back to marker query.");
                }
            }

            if (modellingFilter.AnalyseRemainingRules)
            {
                var RuleVariables = new
                {
                    mgmId = mgtId,
                    import_id_start = relImpId,
                    import_id_end = relImpId
                };
                return await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesByManagement, RuleVariables);
            }
            else
            {
                var RuleVariables = new
                {
                    mgmId = mgtId,
                    import_id_start = relImpId,
                    import_id_end = relImpId,
                    marker = $"%{userConfig.ModModelledMarker}%"
                };

                string query = userConfig.ModModelledMarkerLocation switch
                {
                    MarkerLocation.Rulename => RuleQueries.getModelledRulesByManagementName,
                    MarkerLocation.Comment => RuleQueries.getModelledRulesByManagementComment,
                    _ => throw new NotSupportedException("invalid or undefined Marker Location")
                };

                return await apiConnection.SendQueryAsync<List<Rule>>(query, RuleVariables);
            }
        }

        /// <summary>
        /// Uses the NameField rule_owner mapping as a prefilter only for the normal variance rule load.
        /// AnalyseRemainingRules must inspect rules beyond the current owner's mapped modelled rules,
        /// and RulesForDeletedConns may need historical/deleted model references that are not guaranteed
        /// to have an active rule_owner entry. Those expanded modes therefore keep using the existing
        /// marker-based rule loading path.
        /// </summary>
        private bool ShouldUseNameFieldRuleOwnerPreFilter(ModellingFilter modellingFilter)
        {
            return userConfig.OwnerSoruceMappingID == (int)OwnerMappingSourceStm.NameField
                && userConfig.ModModelledMarkerLocation == MarkerLocation.Rulename
                && owner.Id > 0
                && !string.IsNullOrWhiteSpace(userConfig.ModModelledMarker)
                && !modellingFilter.AnalyseRemainingRules
                && !modellingFilter.RulesForDeletedConns;
        }

        /// <summary>
        /// Checks import_control backlog for firewall-import driven rule_owner mapping lag.
        /// Owner/model changes are expected to update rule_owner mapping promptly; without an
        /// import_control marker there is no cheap freshness signal here.
        /// </summary>
        private async Task<bool> IsRuleOwnerMappingCurrent(int mgtId, long relImpId)
        {
            try
            {
                PendingRuleOwnerMappingImports ??= await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports) ?? [];

                bool hasRelevantPendingImport = PendingRuleOwnerMappingImports.Any(import => import.ControlId <= relImpId && (!import.MgmId.HasValue || import.MgmId.Value == mgtId));
                if (hasRelevantPendingImport)
                {
                    Log.WriteDebug("Variance Rule Loading",
                        $"Skipping NameField rule_owner prefilter because pending rule_owner mapping imports exist for management {mgtId} up to import {relImpId}.");
                }

                return !hasRelevantPendingImport;
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Variance Rule Loading",
                        $"Could not verify rule_owner mapping freshness for management {mgtId}, import {relImpId}. Falling back to marker query. {exception.Message}");

                return false;
            }
        }

        private async Task<List<Rule>?> TryGetNameFieldRuleOwnerPrefilteredRules(int mgtId, long? relImpId)
        {
            try
            {
                var RuleVariables = new
                {
                    mgmId = mgtId,
                    ownerId = owner.Id,
                    ownerMappingSourceId = (short)(int)OwnerMappingSourceStm.NameField,
                    active = true,  //  Used by ruleDetailsForReport fragments
                    marker = $"%{userConfig.ModModelledMarker}%",
                    import_id_start = relImpId,
                    import_id_end = relImpId
                };

                return await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getModelledRulesByRuleOwnerNameField, RuleVariables);
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
            try
            {
                var Variables = new
                {
                    time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    mgmIds = mgtId
                };
                List<Management> managements = (await apiConnection.SendQueryAsync<List<Management>>(ReportQueries.getRelevantImportIdsAtTime, Variables))!;
                if (managements.Count == 0)
                {
                    Log.WriteError("GetRelevantImportId", $"No management data found for management ID {mgtId}.");
                    return null;
                }
                // we may get multiple results if this management is a submanagement of a multi device manager
                return managements.Select(m => m.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1).Max();
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("import_id"), "Get ImportIds leads to error: ", exception);
            }
            return null;
        }
    }
}
