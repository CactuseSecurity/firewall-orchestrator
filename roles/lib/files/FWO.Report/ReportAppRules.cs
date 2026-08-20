using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Modelling;
using FWO.Report.Filter;
using FWO.Config.Api;
using FWO.Logging;
using NetTools;
using System.Net;
using FWO.Basics;

namespace FWO.Report
{
    public class ReportAppRules : ReportRules
    {
        private const string kRuleOwnerPrefilterMarker = "appRulesRuleOwnerPrefilterMarker";
        private readonly ModellingFilter modellingFilter;
        private readonly ReportTemplate? reportTemplate;
        private bool ruleOwnerPrefilterApplied;

        public ReportAppRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ModellingFilter modellingFilter, ReportTemplate reportTemplate) : base(query, userConfig, reportType)
        {
            this.modellingFilter = modellingFilter;
            this.reportTemplate = reportTemplate;
        }

        public ReportAppRules(ReportRules reportRules, ModellingFilter modellingFilter) : base(reportRules.Query, reportRules.userConfig, reportRules.ReportType)
        {
            this.modellingFilter = modellingFilter;
        }

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await base.Generate(elementsPerFetch, apiConnection, callback, ct);
            ReportData.ManagementData = await PrepareAppRulesReport(ReportData.ManagementData, modellingFilter, apiConnection, Query.SelectedOwner?.Id);
        }

        /// <summary>
        /// Adds the NameField rule_owner prefilter to App Rules reports when the mapping can safely replace an early marker scan.
        /// </summary>
        protected override async Task PrepareQueryBeforeFetch(List<ManagementReport> managementsWithRelevantImportId, ApiConnection apiConnection)
        {
            if (reportTemplate == null || ruleOwnerPrefilterApplied || !ShouldUseNameFieldRuleOwnerPreFilter())
            {
                return;
            }

            if (!await IsRuleOwnerMappingCurrent(managementsWithRelevantImportId, apiConnection)
                || !await IsRuleOwnerPreFilterCompletenessVerified(managementsWithRelevantImportId, apiConnection))
            {
                return;
            }

            ApplyNameFieldRuleOwnerPreFilter();
            Log.WriteDebug("App Rules Report",
                $"Using NameField rule_owner prefilter for owner {Query.SelectedOwner?.Id}.");
        }

        public override async Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            int mid = (int)objQueryVariables.GetValueOrDefault(QueryVar.MgmIds)!;
            ManagementReport managementReport = ReportData.ManagementData.FirstOrDefault(m => m.Id == mid) ?? throw new ArgumentException("Given management id does not exist for this report");
            PrepareFilter(managementReport, await GetAppServers(apiConnection, Query.SelectedOwner?.Id));
            UseAdditionalFilter = !modellingFilter.ShowFullRules;

            bool gotAllObjects = await base.GetObjectsForManagementInReport(objQueryVariables, objects, maxFetchCycles, apiConnection, callback);
            if (gotAllObjects)
            {
                PrepareRsbOutput(managementReport);
            }
            return gotAllObjects;
        }

        /// <summary>
        /// Checks the App Rules specific preconditions for using NameField rule_owner data as an early rule prefilter.
        /// </summary>
        private bool ShouldUseNameFieldRuleOwnerPreFilter()
        {
            return userConfig.OwnerSoruceMappingID == (int)OwnerMappingSourceStm.NameField
                && userConfig.ModModelledMarkerLocation == MarkerLocation.Rulename
                && Query.SelectedOwner?.Id > 0
                && !string.IsNullOrWhiteSpace(userConfig.ModModelledMarker);
        }

        /// <summary>
        /// Verifies that no pending rule_owner mapping imports can make the NameField mapping stale for this report.
        /// </summary>
        private async Task<bool> IsRuleOwnerMappingCurrent(List<ManagementReport> managementsWithRelevantImportId, ApiConnection apiConnection)
        {
            try
            {
                List<ImportControl> pendingRuleOwnerMappingImports =
                    await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports) ?? new List<ImportControl>();
                HashSet<int> relevantManagementIds = BuildRelevantManagementIds(managementsWithRelevantImportId);
                bool hasRelevantPendingImport = pendingRuleOwnerMappingImports.Any(import => !import.MgmId.HasValue || relevantManagementIds.Contains(import.MgmId.Value));

                if (hasRelevantPendingImport)
                {
                    Log.WriteDebug("App Rules Report",
                        $"Skipping NameField rule_owner prefilter because pending rule_owner mapping imports exist for managements {string.Join(", ", relevantManagementIds)}.");
                }

                return !hasRelevantPendingImport;
            }
            catch (Exception exception)
            {
                Log.WriteWarning("App Rules Report",
                    $"Could not verify rule_owner mapping freshness. Falling back to App Rules query. {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures marker rules for the selected owner already have active NameField rule_owner mappings before the prefilter is used.
        /// </summary>
        private async Task<bool> IsRuleOwnerPreFilterCompletenessVerified(List<ManagementReport> managementsWithRelevantImportId, ApiConnection apiConnection)
        {
            try
            {
                HashSet<long> ownerConnectionIds = await GetNameFieldRuleOwnerConnectionIds(apiConnection);
                int missingMappingCount = 0;
                foreach (ManagementReport management in managementsWithRelevantImportId)
                {
                    Dictionary<string, object?> ruleVariables = BuildNameFieldRuleOwnerRuleVariables(management);
                    List<Rule> markerRules = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getNameFieldRuleOwnerPreFilterCompletenessRules, ruleVariables) ?? new List<Rule>();
                    missingMappingCount += markerRules.Count(rule => long.TryParse(ParseMarkerConnectionId(rule.Name), out long connectionId) && ownerConnectionIds.Contains(connectionId));
                }

                if (missingMappingCount > 0)
                {
                    Log.WriteDebug("App Rules Report",
                        $"Skipping NameField rule_owner prefilter because {missingMappingCount} owner marker rules have no active rule_owner mapping " +
                        $"for owner {Query.SelectedOwner?.Id}.");
                }

                return missingMappingCount == 0;
            }
            catch (Exception exception)
            {
                Log.WriteWarning("App Rules Report",
                    $"Could not verify NameField rule_owner prefilter completeness for owner {Query.SelectedOwner?.Id}. Falling back to App Rules query. {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads modelling connection IDs that should be represented by NameField rule_owner mappings for the selected app.
        /// </summary>
        private async Task<HashSet<long>> GetNameFieldRuleOwnerConnectionIds(ApiConnection apiConnection)
        {
            List<int> ownerIds = new() { Query.SelectedOwner!.Id };
            List<ModellingConnection> ownerConnections =
                await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getOwnersForRuleOwnerNameFieldFilteredByOwner, new { ownerIds }) ?? new List<ModellingConnection>();
            return ownerConnections.Select(connection => (long)connection.Id).ToHashSet();
        }

        /// <summary>
        /// Builds variables for the shared NameField marker completeness query for one management import snapshot.
        /// </summary>
        private Dictionary<string, object?> BuildNameFieldRuleOwnerRuleVariables(ManagementReport management)
        {
            return new()
            {
                ["mgmId"] = management.Id,
                ["ownerId"] = Query.SelectedOwner!.Id,
                ["ownerMappingSourceId"] = (short)(int)OwnerMappingSourceStm.NameField,
                ["marker"] = $"%{userConfig.ModModelledMarker}%",
                ["import_id_start"] = management.RelevantImportId,
                ["import_id_end"] = management.RelevantImportId
            };
        }

        /// <summary>
        /// Adds the marker and rule_owner predicates to the App Rules query and rebuilds the legacy report query.
        /// </summary>
        private void ApplyNameFieldRuleOwnerPreFilter()
        {
            Query.QueryParameters.Add($"${kRuleOwnerPrefilterMarker}: String! ");
            Query.QueryVariables[kRuleOwnerPrefilterMarker] = $"%{userConfig.ModModelledMarker}%";
            Query.AddRuleWhereAndFilter($"{{ rule_name: {{ _ilike: ${kRuleOwnerPrefilterMarker} }} }}");
            Query.AddRuleWhereAndFilter(
                $"{{ rule_metadatum: {{ rule_owners: {{ owner_id: {{ _eq: {Query.SelectedOwner!.Id} }}, " +
                $"owner_mapping_source_id: {{ _eq: {(short)(int)OwnerMappingSourceStm.NameField} }}, " +
                "removed: { _is_null: true } } } }");
            Query.RebuildLegacyRulesQuery(reportTemplate!);
            ruleOwnerPrefilterApplied = true;
        }

        /// <summary>
        /// Collects management and sub-management IDs that can be affected by pending rule_owner mapping imports.
        /// </summary>
        private HashSet<int> BuildRelevantManagementIds(List<ManagementReport> managementsWithRelevantImportId)
        {
            HashSet<int> managementIds = new();
            foreach (ManagementReport management in managementsWithRelevantImportId)
            {
                managementIds.Add(management.Id);
                foreach (Management subManagement in management.SubManagements)
                {
                    managementIds.Add(subManagement.Id);
                }
            }
            return managementIds;
        }

        /// <summary>
        /// Extracts the connection ID from the configured marker in a rule name.
        /// </summary>
        private string? ParseMarkerConnectionId(string? ruleName)
        {
            if (string.IsNullOrEmpty(ruleName))
            {
                return null;
            }

            int markerIndex = ruleName.IndexOf(userConfig.ModModelledMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return null;
            }

            int start = markerIndex + userConfig.ModModelledMarker.Length;
            int end = start;
            while (end < ruleName.Length && char.IsDigit(ruleName[end]))
            {
                end++;
            }
            return end > start ? ruleName[start..end] : null;
        }

        public static async Task<List<ManagementReport>> PrepareAppRulesReport(List<ManagementReport> managementData, ModellingFilter modellingFilter, ApiConnection apiConnection, int? ownerId)
        {
            List<IPAddressRange> ownerIps = await GetAppServers(apiConnection, ownerId);
            List<ManagementReport> relevantData = [];
            foreach (var mgt in managementData)
            {
                ManagementReport relevantMgt = new() { Name = mgt.Name, Id = mgt.Id, Import = mgt.Import };
                foreach (var rulebase in mgt.Rulebases)
                {
                    PrepareRulebase(rulebase, modellingFilter, relevantMgt, ownerIps);
                }
                if (relevantMgt.Rulebases.Length > 0)
                {
                    relevantMgt.ReportedRuleIds = [.. relevantMgt.ReportedRuleIds.Distinct()];
                    relevantMgt.Devices = [.. PrepareDevices(mgt.Devices)];
                    relevantData.Add(relevantMgt);
                }
            }
            return relevantData;
        }

        private static List<DeviceReport> PrepareDevices(DeviceReport[] deviceReports)
        {
            List<DeviceReport> selectedDeviceReports = [];
            foreach (var devReport in deviceReports)
            {
                DeviceReport selectedDevReport = new(devReport);
                foreach (var rule in devReport.GetRuleList())
                {
                    if (selectedDevReport.IsLinked(rule))
                    {
                        selectedDevReport.AddRule(rule);
                    }
                }
                if (selectedDevReport.ContainsRules())
                {
                    selectedDeviceReports.Add(selectedDevReport);
                }
            }
            return selectedDeviceReports;
        }

        private static void PrepareRulebase(RulebaseReport rulebase, ModellingFilter modellingFilter, ManagementReport relevantMgt, List<IPAddressRange> ownerIps)
        {
            RulebaseReport relevantRulebase = new() { Name = rulebase.Name, Id = rulebase.Id };
            foreach (var rule in rulebase.Rules)
            {
                PrepareRule(rule, modellingFilter, relevantMgt, relevantRulebase, ownerIps);
            }
            if (relevantRulebase.Rules.Length > 0)
            {
                relevantMgt.Rulebases = [.. relevantMgt.Rulebases, relevantRulebase];
            }
        }

        private static void PrepareRule(Rule rule, ModellingFilter modellingFilter, ManagementReport relevantMgt, RulebaseReport relevantRulebase, List<IPAddressRange> ownerIps)
        {
            if (modellingFilter.ShowDropRules || !rule.IsDropRule())
            {
                List<NetworkLocation> relevantFroms = [];
                List<NetworkLocation> disregardedFroms = [.. rule.Froms];
                if (modellingFilter.ShowSourceMatch)
                {
                    (relevantFroms, disregardedFroms) = CheckNetworkObjects(rule.Froms, rule.SourceNegated, modellingFilter, ownerIps);
                }
                List<NetworkLocation> relevantTos = [];
                List<NetworkLocation> disregardedTos = [.. rule.Tos];
                if (modellingFilter.ShowDestinationMatch)
                {
                    (relevantTos, disregardedTos) = CheckNetworkObjects(rule.Tos, rule.DestinationNegated, modellingFilter, ownerIps);
                }

                if (relevantFroms.Count > 0 || relevantTos.Count > 0)
                {
                    rule.Froms = [.. relevantFroms];
                    rule.Tos = [.. relevantTos];
                    rule.DisregardedFroms = [.. disregardedFroms];
                    rule.DisregardedTos = [.. disregardedTos];
                    rule.ShowDisregarded = modellingFilter.ShowFullRules;
                    relevantRulebase.Rules = [.. relevantRulebase.Rules, rule];
                    relevantMgt.ReportedRuleIds.Add(rule.Id);
                }
            }
        }

        private static async Task<List<IPAddressRange>> GetAppServers(ApiConnection apiConnection, int? ownerId)
        {
            List<ModellingAppServer> appServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersForOwner,
                new { appId = ownerId });
            return [.. appServers.ConvertAll(s => new IPAddressRange(IPAddress.Parse(s.Ip.StripOffNetmask()),
                IPAddress.Parse((s.IpEnd != "" ? s.IpEnd : s.Ip).StripOffNetmask())))];
        }

        private static (List<NetworkLocation>, List<NetworkLocation>) CheckNetworkObjects(NetworkLocation[] objList, bool negated, ModellingFilter modellingFilter, List<IPAddressRange> ownerIps)
        {
            List<NetworkLocation> relevantObjects = [];
            List<NetworkLocation> disregardedObjects = [];
            foreach (var obj in objList)
            {
                if (obj.Object.IsAnyObject())
                {
                    if (modellingFilter.ShowAnyMatch)
                    {
                        relevantObjects.Add(obj);
                    }
                    else
                    {
                        disregardedObjects.Add(obj);
                    }
                }
                else
                {
                    CheckSpecificObj(obj, negated, ownerIps, relevantObjects, disregardedObjects);
                }
            }
            return (relevantObjects, disregardedObjects);
        }

        private static void CheckSpecificObj(NetworkLocation obj, bool negated, List<IPAddressRange> ownerIps, List<NetworkLocation> relevantObjects, List<NetworkLocation> disregardedObjects)
        {
            bool found = false;
            if (obj.Object.Type.Name == ObjectType.Group)
            {
                foreach (var grpobj in obj.Object.ObjectGroupFlats.Select(o => o.Object))
                {
                    if (grpobj != null && CheckObj(grpobj, negated, ownerIps))
                    {
                        relevantObjects.Add(obj);
                        found = true;
                        break;
                    }
                }
            }
            else if (CheckObj(obj.Object, negated, ownerIps))
            {
                relevantObjects.Add(obj);
                found = true;
            }
            if (!found)
            {
                disregardedObjects.Add(obj);
            }
        }

        private static bool CheckObj(NetworkObject obj, bool negated, List<IPAddressRange> ownerIps)
        {
            foreach (var ownerIpRange in ownerIps)
            {
                if (obj.IP == null)
                {
                    continue;
                }

                IPAddressRange objRange = new(IPAddress.Parse(obj.IP.StripOffNetmask()),
                    IPAddress.Parse((obj.IpEnd != null && obj.IpEnd != "" ? obj.IpEnd : obj.IP).StripOffNetmask()));

                if (negated)
                {
                    if (IpOperations.IpToUint(ownerIpRange.Begin) < IpOperations.IpToUint(objRange.Begin) ||
                            (IpOperations.IpToUint(ownerIpRange.End) > IpOperations.IpToUint(objRange.End)))
                    {
                        return true;
                    }
                }
                else if (IpOperations.RangeOverlapExists(objRange, ownerIpRange))
                {
                    return true;
                }
            }
            return false;
        }

        private static void PrepareFilter(ManagementReport mgt, List<IPAddressRange> ownerIps)
        {
            mgt.RelevantObjectIds = [];
            mgt.HighlightedObjectIds = [];
            foreach (var rb in mgt.Rulebases)
            {
                foreach (var rule in rb.Rules)
                {
                    PrepareObjects(rule.Froms, rule.SourceNegated, rule.DisregardedFroms, mgt, ownerIps);
                    PrepareObjects(rule.Tos, rule.DestinationNegated, rule.DisregardedTos, mgt, ownerIps);
                }
            }
            mgt.RelevantObjectIds = [.. mgt.RelevantObjectIds.Distinct()];
            mgt.HighlightedObjectIds = [.. mgt.HighlightedObjectIds.Distinct()];
        }

        private static void PrepareObjects(NetworkLocation[] networkLocations, bool negated, NetworkLocation[] disregardedLocations, ManagementReport mgt, List<IPAddressRange> ownerIps)
        {
            foreach (var from in networkLocations.Select(f => f.Object))
            {
                mgt.RelevantObjectIds.Add(from.Id);
                mgt.HighlightedObjectIds.Add(from.Id);
                if (from.Type.Name == ObjectType.Group)
                {
                    foreach (var grpobj in from.ObjectGroupFlats.Select(g => g.Object).Where(gr => gr != null && CheckObj(gr, negated, ownerIps)))
                    {
                        mgt.HighlightedObjectIds.Add(grpobj!.Id);
                    }
                }
            }
            if (networkLocations.Length == 0)
            {
                foreach (var from in disregardedLocations)
                {
                    mgt.RelevantObjectIds.Add(from.Object.Id);
                }
            }
        }

        private static void PrepareRsbOutput(ManagementReport mgt)
        {
            foreach (var obj in mgt.ReportObjects)
            {
                obj.Highlighted = mgt.HighlightedObjectIds.Contains(obj.Id) || obj.IsAnyObject();
                if (obj.Type.Name == ObjectType.Group)
                {
                    foreach (var grpobj in obj.ObjectGroupFlats.Select(g => g.Object).Where(g => g != null))
                    {
                        grpobj!.Highlighted = mgt.HighlightedObjectIds.Contains(grpobj.Id) || grpobj.IsAnyObject();
                    }
                    foreach (var grpobj in obj.ObjectGroups.Select(g => g.Object).Where(g => g != null))
                    {
                        grpobj!.Highlighted = mgt.HighlightedObjectIds.Contains(grpobj.Id) || grpobj.IsAnyObject();
                    }
                }
            }
        }
    }
}
