using FWO.Basics;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using NetTools;
using System.Net;
using FWO.Basics;

namespace FWO.Report
{
    public class ReportAppRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ModellingFilter modellingFilter) : ReportRules(query, userConfig, reportType)
    {
        private List<IPAddressRange> ownerIps = [];

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await base.Generate(rulesPerFetch, apiConnection, callback, ct);
            await PrepareAppRulesReport(apiConnection);
        }

        public override async Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            int mid = (int)objQueryVariables.GetValueOrDefault("mgmIds")!;
            ManagementReport managementReport = ReportData.ManagementData.FirstOrDefault(m => m.Id == mid) ?? throw new ArgumentException("Given management id does not exist for this report");
            PrepareFilter(managementReport);
            UseAdditionalFilter = !modellingFilter.ShowFullRules;

            bool gotAllObjects = await base.GetObjectsForManagementInReport(objQueryVariables, objects, maxFetchCycles, apiConnection, callback);
            if (gotAllObjects)
            {
                PrepareRsbOutput(managementReport);
            }
            return gotAllObjects;
        }

        private async Task PrepareAppRulesReport(ApiConnection apiConnection)
        {
            await GetAppServers(apiConnection);
            List<ManagementReport> relevantData = [];
            foreach(var mgt in ReportData.ManagementData)
            {
                ManagementReport relevantMgt = new(){ Name = mgt.Name, Id = mgt.Id, Import = mgt.Import };
                foreach(var dev in mgt.Devices)
                {
                    DeviceReport relevantDevice = new(){ Name = dev.Name, Id = dev.Id };
                    foreach (var rb in dev.OrderedRulebases)
                    {
                        if(rb.Rulebase.RuleMetadata[0].Rules != null)
                        {
                            // relevantDevice.Rules = [];
                            foreach(var rule in rb.Rulebase.RuleMetadata[0].Rules)
                            {
                                RulebaseOnGateway relevantRulebase = new();
                                if(modellingFilter.ShowDropRules || !rule.IsDropRule())
                                {
                                    List<NetworkLocation> relevantFroms = [];
                                    List<NetworkLocation> disregardedFroms = [.. rule.Froms];
                                    if(modellingFilter.ShowSourceMatch)
                                    {
                                        (relevantFroms, disregardedFroms) = CheckNetworkObjects(rule.Froms);
                                    }
                                    List<NetworkLocation> relevantTos = [];
                                    List<NetworkLocation> disregardedTos = [.. rule.Tos];
                                    if(modellingFilter.ShowDestinationMatch)
                                    {
                                        (relevantTos, disregardedTos) = CheckNetworkObjects(rule.Tos);
                                    }

                                    if(relevantFroms.Count > 0 || relevantTos.Count > 0)
                                    {
                                        rule.Froms = [.. relevantFroms];
                                        rule.Tos = [.. relevantTos];
                                        rule.DisregardedFroms = [.. disregardedFroms];
                                        rule.DisregardedTos = [.. disregardedTos];
                                        rule.ShowDisregarded = modellingFilter.ShowFullRules;
                                        relevantRulebase.Rulebase.RuleMetadata[0].Rules = [.. relevantRulebase.Rulebase.RuleMetadata[0].Rules, rule];
                                        relevantMgt.ReportedRuleIds.Add(rule.Id);
                                        relevantDevice.OrderedRulebases = [.. relevantDevice.OrderedRulebases, relevantRulebase];
                                    }
                                }
                            }
                            if(relevantDevice.ContainsRules())
                            {
                                relevantMgt.Devices = [.. relevantMgt.Devices, relevantDevice];
                            }
                        }
                    }
                }
                if(relevantMgt.Devices.Length > 0)
                {
                    relevantMgt.ReportedRuleIds = relevantMgt.ReportedRuleIds.Distinct().ToList();
                    relevantData.Add(relevantMgt);
                }
            }
            ReportData.ManagementData = relevantData;
        }

        private async Task GetAppServers(ApiConnection apiConnection)
        {
            List<ModellingAppServer> appServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, 
                new { appId = Query.SelectedOwner?.Id });
            ownerIps = [.. appServers.ConvertAll(s => new IPAddressRange(IPAddress.Parse(s.Ip.StripOffNetmask()),
                IPAddress.Parse((s.IpEnd != "" ? s.IpEnd : s.Ip).StripOffNetmask())))];
        }

        private (List<NetworkLocation>, List<NetworkLocation>) CheckNetworkObjects(NetworkLocation[] objList)
        {
            List<NetworkLocation> relevantObjects = [];
            List<NetworkLocation> disregardedObjects = [];
            foreach(var obj in objList)
            {
                if(obj.Object.IsAnyObject())
                {
                    if(modellingFilter.ShowAnyMatch)
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
                    if(obj.Object.Type.Name == ObjectType.Group)
                    {
                        foreach(var grpobj in obj.Object.ObjectGroupFlats)
                        {
                            if(grpobj.Object != null && CheckObj(grpobj.Object))
                            {
                                relevantObjects.Add(obj);
                                found = true;
                                break;
                            }
                        }
                    }
                    else if(CheckObj(obj.Object))
                    {
                        relevantObjects.Add(obj);
                        found = true;
                    }
                    if(!found)
                    {
                        disregardedObjects.Add(obj);
                    }
                }
            }
            return (relevantObjects, disregardedObjects);
        }

        private bool CheckObj(NetworkObject obj)
        {
            foreach(var ownerIpRange in ownerIps)
            {
                if(obj.IP != null &&
                    ComplianceNetworkZone.OverlapExists(new IPAddressRange(IPAddress.Parse(obj.IP.StripOffNetmask()),
                    IPAddress.Parse((obj.IpEnd != null && obj.IpEnd != "" ? obj.IpEnd : obj.IP).StripOffNetmask())), ownerIpRange))
                {
                    return true;
                }
            }
            return false;
        }

        private void PrepareFilter(ManagementReport mgt)
        {
            mgt.RelevantObjectIds = [];
            mgt.HighlightedObjectIds = [];
            foreach(var dev in mgt.Devices)
            {
                foreach (var rb in dev.OrderedRulebases)
                {
                    if(rb.Rulebase.RuleMetadata[0].Rules != null)
                    {
                        foreach(var rule in rb.Rulebase.RuleMetadata[0].Rules)
                        {
                            foreach(var from in rule.Froms)
                            {
                                mgt.RelevantObjectIds.Add(from.Object.Id);
                                mgt.HighlightedObjectIds.Add(from.Object.Id);
                                if(from.Object.Type.Name == ObjectType.Group)
                        //     mgt.RelevantObjectIds.Add(from.Object.Id);
                        //     mgt.HighlightedObjectIds.Add(from.Object.Id);
                        //     if(from.Object.Type.Name == ObjectType.Group)
                        //     {
                        //         foreach(var grpobj in from.Object.ObjectGroupFlats)
                        //         {
                        //             if(grpobj.Object != null && CheckObj(grpobj.Object))
                        //             {
                        //                 mgt.HighlightedObjectIds.Add(grpobj.Object.Id);
                        //             }
                        //         }
                        //     }
                        // }
                        // if(rule.Froms.Length == 0)
                        // {
                        //     foreach(var from in rule.DisregardedFroms)
                        //     {
                        //         mgt.RelevantObjectIds.Add(from.Object.Id);
                        //     }
                        // }
                        // foreach(var to in rule.Tos)
                        // {
                        //     mgt.RelevantObjectIds.Add(to.Object.Id);
                        //     mgt.HighlightedObjectIds.Add(to.Object.Id);
                        //     if(to.Object.Type.Name == ObjectType.Group)
                        //     {
                        //         foreach(var grpobj in to.Object.ObjectGroupFlats)
                                {
                                    foreach(var grpobj in from.Object.ObjectGroupFlats)
                                    {
                                        if(grpobj.Object != null && CheckObj(grpobj.Object))
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
                                        if(grpobj.Object != null && CheckObj(grpobj.Object))
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
            }
            mgt.RelevantObjectIds = mgt.RelevantObjectIds.Distinct().ToList();
            mgt.HighlightedObjectIds = mgt.HighlightedObjectIds.Distinct().ToList();
        }

        private static void PrepareRsbOutput(ManagementReport mgt)
        {
            foreach(var obj in mgt.ReportObjects)
            {
                obj.Highlighted = mgt.HighlightedObjectIds.Contains(obj.Id) || obj.IsAnyObject();
                if(obj.Type.Name == ObjectType.Group)
                {
                    foreach(var grpobj in obj.ObjectGroupFlats)
                    {
                        if (grpobj.Object != null)
                        {
                            grpobj.Object.Highlighted = mgt.HighlightedObjectIds.Contains(grpobj.Object.Id) || grpobj.Object.IsAnyObject();
                        }
                    }
                    foreach(var grpobj in obj.ObjectGroups)
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
