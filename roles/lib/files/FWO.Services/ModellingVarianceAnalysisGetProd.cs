using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using FWO.FwLogic;

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
                Log.WriteError(userConfig.GetText("fetch_data"), "Init Managements leads to error: ", exception);
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
                        AnalyseModelledRules(mgt, rulesByMgt);
                        modelledRulesCount += allModelledRules[mgt.Id].Count;
                        notModelledRulesCount += varianceResult.UnModelledRules[mgt.Id].Count;
                    }
                }
                Log.WriteDebug("GetModelledRulesProductionState", $"Found {modelledRulesCount} modelled rules, {notModelledRulesCount} others.");
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("fetch_data"), "Get Production State leads to error: ", exception);
                displayMessageInUi(exception, userConfig.GetText("fetch_data"), "Get Production State leads to error: ", true);
                return false;
            }
            return true;
        }

        private void AnalyseModelledRules(Management mgt, List<Rule> rulesByMgt)
        {
            // get all rulebase links for this management
            List<RulebaseLink> rulebaseLinks = mgt.Devices.Cast<DeviceReport>().SelectMany(d => d.RulebaseLinks.Where(rl => rl.Removed!=null)).ToList();
            allModelledRules.Add(mgt.Id, []);
            foreach (var rule in rulesByMgt)
            {
                string? connRef = FindModelledMarker(rule);
                if(connRef != null)
                {
                    if(long.TryParse(connRef, out long connId))
                    {
                        rule.ConnId = connId;
                    }
                    rule.ManagementName = mgt.Name;
                    int enforcingDeviceId = rulebaseLinks.FirstOrDefault(rl => rl.NextRulebaseId == rule.RulebaseId)?.GatewayId ?? 0;
                    rule.DeviceName = mgt.Devices.FirstOrDefault(d => d.Id == enforcingDeviceId)?.Name ?? "";
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
                "rulename" => !string.IsNullOrEmpty(rule.Name) && rule.Name.Contains(userConfig.ModModelledMarker) ? ParseFromString(rule.Name) : null,
                "comment" => !string.IsNullOrEmpty(rule.Comment) && rule.Comment.Contains(userConfig.ModModelledMarker) ? ParseFromString(rule.Comment) : null,
                "customfields" => !string.IsNullOrEmpty(rule.CustomFields) ? GetFromCustomField(rule) : null,
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

        private async Task<List<Rule>?> GetRules(int mgtId, ModellingFilter modellingFilter)
        {
            long? relImpId = await GetRelevantImportId(mgtId);
            if(modellingFilter.AnalyseRemainingRules)
            {
                var RuleVariables = new
                {
                    mgmId = mgtId,
                    relevantImportId = relImpId
                };
                return await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesByManagement, RuleVariables);
            }
            else
            {
                var RuleVariables = new
                {
                    mgmId = mgtId,
                    relevantImportId = relImpId,
                    marker = $"%{userConfig.ModModelledMarker}%"
                };

                string query = userConfig.ModModelledMarkerLocation switch
                {
                    "rulename" => RuleQueries.getModelledRulesByManagementName,
                    "comment" => RuleQueries.getModelledRulesByManagementComment,
                    _ => throw new Exception("invalid or undefined Marker Location")
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
                foreach (Management mgt in RelevantManagements)
                {
                    List<NetworkObject>? objGrpByMgt = await GetObjects(mgt.Id, [2]);
                    if (objGrpByMgt != null)
                    {
                        foreach (NetworkObject objGrp in objGrpByMgt)
                        {
                            // Todo: filter for naming convention??
                            if (!allProdAppRoles.ContainsKey(mgt.Id))
                            {
                                allProdAppRoles.Add(mgt.Id, []);
                            }
                            allProdAppRoles[mgt.Id].Add(new(objGrp, namingConvention));
                            aRCount++;
                        }
                    }

                    List<NetworkObject>? objByMgt = await GetObjects(mgt.Id, [1, 3, 12]);
                    if (objByMgt != null)
                    {
                        foreach (NetworkObject obj in objByMgt)
                        {
                            if (!allExistingAppServers.ContainsKey(mgt.Id))
                            {
                                allExistingAppServers.Add(mgt.Id, []);
                            }
                            allExistingAppServers[mgt.Id].Add(new(obj));
                            aSCount++;
                        }
                    }
                }

                string aRappRoles = "";
                string aRappServers = "";
                foreach (int mgt in allProdAppRoles.Keys)
                {
                    aRappRoles += $" Management {mgt}: " + string.Join(",", allProdAppRoles[mgt].Where(a => a.Name.StartsWith("AR")).ToList().ConvertAll(x => $"{x.Name}({x.IdString})").ToList());
                }
                foreach (int mgt in allExistingAppServers.Keys)
                {
                    aRappServers += $" Management {mgt}: " + string.Join(",", allExistingAppServers[mgt].ConvertAll(x => $"{x.Name}({x.Ip})").ToList());
                }

                Log.WriteDebug("GetNwObjectsProductionState",
                    $"Found {aRCount} AppRoles, {aSCount} AppServer. AppRoles with AR: {aRappRoles},  AppServers: {aRappServers}");
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("fetch_data"), "Get Production State leads to error: ", exception);
            }
        }

        private async Task<List<NetworkObject>?> GetObjects(int mgtId, int[] objTypeIds)
        {
            try
            {
                long? relImpId = await GetRelevantImportId(mgtId);
                if(relImpId != null)
                {
                    var ObjGroupVariables = new
                    {
                        mgmId = mgtId,
                        objTypeIds = objTypeIds,
                        relevantImportId = relImpId
                    };
                    return await apiConnection.SendQueryAsync<List<NetworkObject>>(ObjectQueries.getNetworkObjectsForManagement, ObjGroupVariables);
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("fetch_data"), "Get Production Objects leads to error: ", exception);
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
                return (await apiConnection.SendQueryAsync<List<Management>>(ReportQueries.getRelevantImportIdsAtTime, Variables))?
                    .First().Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("fetch_data"), "Get ImportIds leads to error: ", exception);
            }
            return null;
        }
    }
}
