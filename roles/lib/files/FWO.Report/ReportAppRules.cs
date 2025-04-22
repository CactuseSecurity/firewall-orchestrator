using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Modelling;
using FWO.Report.Filter;
using FWO.Config.Api;
using NetTools;
using System.Net;
using FWO.Basics;

namespace FWO.Report
{
    public class ReportAppRules : ReportRules
    {
        private readonly ModellingFilter modellingFilter;

        public ReportAppRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ModellingFilter modellingFilter) : base(query, userConfig, reportType)
        {
            this.modellingFilter = modellingFilter;
        }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await base.Generate(rulesPerFetch, apiConnection, callback, ct);
            ReportData.ManagementData = await PrepareAppRulesReport(ReportData.ManagementData, modellingFilter, apiConnection, Query.SelectedOwner?.Id);
        }

        public override async Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            int mid = (int)objQueryVariables.GetValueOrDefault("mgmIds")!;
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
        

        public static async Task<List<ManagementReport>> PrepareAppRulesReport(List<ManagementReport> managementData, ModellingFilter modellingFilter, ApiConnection apiConnection, int? ownerId)
        {
            List<IPAddressRange> ownerIps = await GetAppServers(apiConnection, ownerId);
            List<ManagementReport> relevantData = [];
            foreach(var mgt in managementData)
            {
                ManagementReport relevantMgt = new() { Name = mgt.Name, Id = mgt.Id, Import = mgt.Import };
                foreach (var dev in mgt.Devices)
                {
                    DeviceReport relevantDevice = new() { Name = dev.Name, Id = dev.Id };
                    var rbLink = dev.RulebaseLinks.FirstOrDefault(r => r.IsInitialRulebase());
                    RulebaseReport? rb = mgt.Rulebases.FirstOrDefault(r => r.Id == rbLink?.NextRulebaseId);
                    if (rb?.Rules != null)
                    {
                        foreach (var rule in rb.Rules)
                        {
                            RulebaseLink relevantRulebase = new();
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
                                    // if (relevantRulebase.NextRulebase != null)
                                    // {
                                    //     relevantRulebase.NextRulebase.Rules = [.. relevantRulebase.NextRulebase.Rules, rule];
                                    //     relevantMgt.ReportedRuleIds.Add(rule.Id);
                                    //     // relevantDevice.Rulebases = [.. relevantDevice.Rulebases, relevantRulebase];
                                    // }
                                }
                            }
                        }
                        if (relevantDevice.ContainsRules())
                        {
                            relevantMgt.Devices = [.. relevantMgt.Devices, relevantDevice];
                        }
                    }
                }
                if (relevantMgt.Devices.Length > 0)
                {
                    relevantMgt.ReportedRuleIds = relevantMgt.ReportedRuleIds.Distinct().ToList();
                    relevantData.Add(relevantMgt);
                }
            }
            return relevantData;
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
                    bool found = false;
                    if (obj.Object.Type.Name == ObjectType.Group)
                    {
                        foreach (var grpobj in obj.Object.ObjectGroupFlats)
                        {
                            if(grpobj.Object != null && CheckObj(grpobj.Object, negated, ownerIps))
                             {
                                relevantObjects.Add(obj);
                                found = true;
                                break;
                            }
                        }
                    }
                    else if(CheckObj(obj.Object, negated, ownerIps))
                    {
                        relevantObjects.Add(obj);
                        found = true;
                    }
                    if (!found)
                    {
                        disregardedObjects.Add(obj);
                    }
                }
            }
            return (relevantObjects, disregardedObjects);
        }

        private static bool CheckObj(NetworkObject obj, bool negated, List<IPAddressRange> ownerIps)
        {
            foreach (var ownerIpRange in ownerIps)
            {
                if(obj.IP == null)
                {
                    continue;
                }

                IPAddressRange objRange = new(IPAddress.Parse(obj.IP.StripOffNetmask()),
                    IPAddress.Parse((obj.IpEnd != null && obj.IpEnd != "" ? obj.IpEnd : obj.IP).StripOffNetmask()));

                if(negated)
                {
                    if (IpOperations.IpToUint(ownerIpRange.Begin) < IpOperations.IpToUint(objRange.Begin) ||
                            (IpOperations.IpToUint(ownerIpRange.End) > IpOperations.IpToUint(objRange.End)))
                    {
                        return true;
                    }
                }
                else if(IpOperations.RangeOverlapExists(objRange, ownerIpRange))
                {
                    return true;
                }
            }
            return false;
        }

        private void PrepareFilter(ManagementReport mgt, List<IPAddressRange> ownerIps)
        {
            mgt.RelevantObjectIds = [];
            mgt.HighlightedObjectIds = [];
            foreach (var dev in mgt.Devices)
            {
                if(dev.Rules != null)
                {
                    foreach(var rule in dev.Rules)
                    {
                        foreach(var from in rule.Froms)
                        {
                            mgt.RelevantObjectIds.Add(from.Object.Id);
                            mgt.HighlightedObjectIds.Add(from.Object.Id);
                            if(from.Object.Type.Name == ObjectType.Group)
                            {
                                foreach(var grpobj in from.Object.ObjectGroupFlats)
                                {
                                    if(grpobj.Object != null && CheckObj(grpobj.Object, rule.SourceNegated, ownerIps))
                                    {
                                        mgt.HighlightedObjectIds.Add(grpobj.Object.Id);
                                    }
                                }
                            }
                        }
                        if(rule.Froms.Length == 0)
                        {
                            foreach(var from in rule.DisregardedFroms)
                            {
                                mgt.RelevantObjectIds.Add(from.Object.Id);
                            }
                        }
                        foreach(var to in rule.Tos)
                        {
                            mgt.RelevantObjectIds.Add(to.Object.Id);
                            mgt.HighlightedObjectIds.Add(to.Object.Id);
                            if(to.Object.Type.Name == ObjectType.Group)
                            {
                                foreach(var grpobj in to.Object.ObjectGroupFlats)
                                {
                                    if(grpobj.Object != null && CheckObj(grpobj.Object, rule.DestinationNegated, ownerIps))
                                    {
                                        mgt.HighlightedObjectIds.Add(grpobj.Object.Id);
                                    }
                                }
                            }
                        }
                        if(rule.Tos.Length == 0)
                        {
                            foreach(var to in rule.DisregardedTos)
                            {
                                mgt.RelevantObjectIds.Add(to.Object.Id);
                            }
                        }
                    }
                }
            }
            mgt.RelevantObjectIds = mgt.RelevantObjectIds.Distinct().ToList();
            mgt.HighlightedObjectIds = mgt.HighlightedObjectIds.Distinct().ToList();
        }

        private static void PrepareRsbOutput(ManagementReport mgt)
        {
            foreach (var obj in mgt.ReportObjects)
            {
                obj.Highlighted = mgt.HighlightedObjectIds.Contains(obj.Id) || obj.IsAnyObject();
                if (obj.Type.Name == ObjectType.Group)
                {
                    foreach (var grpobj in obj.ObjectGroupFlats)
                    {
                        if (grpobj.Object != null)
                        {
                            grpobj.Object.Highlighted = mgt.HighlightedObjectIds.Contains(grpobj.Object.Id) || grpobj.Object.IsAnyObject();
                        }
                    }
                    foreach (var grpobj in obj.ObjectGroups)
                    {
                        if (grpobj.Object != null)
                        {
                            grpobj.Object.Highlighted = mgt.HighlightedObjectIds.Contains(grpobj.Object.Id) || grpobj.Object.IsAnyObject();
                        }
                    }
                }
            }
        }
    }
}
