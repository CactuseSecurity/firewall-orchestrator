using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FWO.Services
{
    /// <summary>
    /// Part of Variance Analysis Class getting the production state
    /// </summary>
    public partial class ModellingVarianceAnalysis
    {
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
                    if(!string.IsNullOrEmpty(extMgtData.ExtId) || !string.IsNullOrEmpty(extMgtData.ExtName))
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
                rule.DeviceName = mgt.Devices.FirstOrDefault(d => d.Id == rule.DeviceId)?.Name ?? "";
                string? connRef = FindModelledMarker(rule);
                if(connRef != null)
                {
                    if(long.TryParse(connRef, out long connId))
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
            if(idx >= 0 && idx < FieldString.Length)
            {
                int? contentLength = NonNumericRegex().Match(FieldString[idx..]).Captures.FirstOrDefault()?.Index;
                return contentLength!= null && contentLength > 0 ? FieldString.Substring(idx, (int)contentLength) : FieldString.Substring(idx);
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
            long? relImpId = await GetRelevantImportId(mgtId);
            if(modellingFilter.AnalyseRemainingRules)
            {
                var RuleVariables = new
                {
                    mgmId = mgtId,
                    import_id_start = relImpId,
                    import_id_end   = relImpId
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
                if (!allExistingAppServersHashes.TryGetValue(mgtId, out Dictionary<int, long>? appServerHashes))
                {
                    appServerHashes = [];
                    allExistingAppServersHashes.Add(mgtId, appServerHashes);
                }
                foreach (NetworkObject obj in objByMgt)
                {
                    ModellingAppServer appServer = new(obj);
                    appServerHashes.TryAdd(appServerComparer.GetHashCode(appServer), appServer.Id);
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
                return (await apiConnection.SendQueryAsync<List<Management>>(ReportQueries.getRelevantImportIdsAtTime,
                    Variables))?[0].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("import_id"), "Get ImportIds leads to error: ", exception);
            }
            return null;
        }
    }
}
