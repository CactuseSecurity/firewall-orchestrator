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

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await base.Generate(elementsPerFetch, apiConnection, callback, ct);
            ReportData.ManagementData = await PrepareAppRulesReport(ReportData.ManagementData, modellingFilter, apiConnection, Query.SelectedOwner?.Id);
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

        public static async Task<List<ManagementReport>> PrepareAppRulesReport(List<ManagementReport> managementData, ModellingFilter modellingFilter, ApiConnection apiConnection, int? ownerId)
        {
            List<IPAddressRange> ownerIps = await GetAppServers(apiConnection, ownerId);
            List<ManagementReport> relevantData = [];
            foreach (var mgt in managementData)
            {
                ManagementReport relevantMgt = new() { Name = mgt.Name, Id = mgt.Id, Import = mgt.Import };
                foreach (var dev in mgt.Devices)
                {
                    PrepareDevice(dev, modellingFilter, relevantMgt, ownerIps);
                }
                if (relevantMgt.Devices.Length > 0)
                {
                    relevantMgt.ReportedRuleIds = [.. relevantMgt.ReportedRuleIds.Distinct()];
                    relevantData.Add(relevantMgt);
                }
            }
            return relevantData;
        }

        private static void PrepareDevice(DeviceReport dev, ModellingFilter modellingFilter, ManagementReport relevantMgt, List<IPAddressRange> ownerIps)
        {
            DeviceReport relevantDevice = new() { Name = dev.Name, Id = dev.Id };
            if (dev.Rules != null)
            {
                relevantDevice.Rules = [];
                foreach (var rule in dev.Rules)
                {
                    PrepareRule(rule, modellingFilter, relevantMgt, relevantDevice, ownerIps);
                }
                if (relevantDevice.Rules.Length > 0)
                {
                    relevantMgt.Devices = [.. relevantMgt.Devices, relevantDevice];
                }
            }
        }

        private static void PrepareRule(Rule rule, ModellingFilter modellingFilter, ManagementReport relevantMgt, DeviceReport relevantDevice, List<IPAddressRange> ownerIps)
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
                    relevantDevice.Rules = [.. relevantDevice.Rules!, rule];
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
            if (negated)
            {
                bool isMatch = CheckNegatedOverlap(objList, ownerIps);
                if (isMatch)
                {
                    relevantObjects.AddRange(objList);
                }
                else
                {
                    disregardedObjects.AddRange(objList);
                }
            }
            else
            {
                foreach (var obj in objList)
                {
                    if (obj.Object.IsAnyObject())
                    {
                        if (!negated && modellingFilter.ShowAnyMatch)
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
                        CheckSpecificObj(obj, ownerIps, relevantObjects, disregardedObjects);
                    }
                }
            }
            return (relevantObjects, disregardedObjects);
        }

        private static bool CheckNegatedOverlap(NetworkLocation[] objList, List<IPAddressRange> ownerNetworks)
        {
            // match iff ip space covered by objList \cap ip space covered by ownerNetworks != \emptyset
            List<(uint, uint)> ownerIpRanges = [.. ownerNetworks.Select(o => (IpOperations.IpToUint(o.Begin), IpOperations.IpToUint(o.End)))];
            ownerIpRanges.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            for (int i = 0; i < ownerIpRanges.Count - 1; i++)
            {
                if (ownerIpRanges[i].Item2 >= ownerIpRanges[i + 1].Item1)
                {
                    // merge overlapping ranges
                    ownerIpRanges[i] = (ownerIpRanges[i].Item1,
                        Math.Max(ownerIpRanges[i].Item2, ownerIpRanges[i + 1].Item2));
                    ownerIpRanges.RemoveAt(i + 1);
                    i--;
                }
            }
            // subtract ip ranges of objects from owner ip ranges
            // for negated object list to match, at least one ip must not be contained in owner ranges
            foreach (var obj in objList)
            {
                List<NetworkObject> flatObjects = obj.Object.Type.Name == ObjectType.Group
                    ? [.. obj.Object.ObjectGroupFlats.Select(o => o.Object).Where(o => o != null).Cast<NetworkObject>()]
                    : [obj.Object];
                foreach (var flatObj in flatObjects)
                {
                    if (flatObj.IP == null || flatObj.IP == "")
                    {
                        continue; // skip objects without IP
                    }
                    uint ipStart = IpOperations.IpToUint(IPAddress.Parse(flatObj.IP.StripOffNetmask()));
                    uint ipEnd = IpOperations.IpToUint(IPAddress.Parse((flatObj.IpEnd != null && flatObj.IpEnd != "" ? flatObj.IpEnd : flatObj.IP).StripOffNetmask()));
                    for (int i = 0; i < ownerIpRanges.Count; i++)
                    {
                        (uint ownerIpStart, uint ownerIpEnd) = ownerIpRanges[i];
                        if (ownerIpStart <= ipEnd && ownerIpEnd >= ipStart)
                        {
                            // overlap exists, remove object ip range from owner ip ranges
                            if (ownerIpStart < ipStart)
                            {
                                // adjust start of range
                                ownerIpRanges[i] = (ownerIpStart, ipStart - 1);
                            }
                            else if (ownerIpEnd > ipEnd)
                            {
                                // adjust end of range
                                ownerIpRanges[i] = (ipEnd + 1, ownerIpEnd);
                            }
                            else
                            {
                                // remove this range completely
                                ownerIpRanges.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
            }
            // if there are any owner ip ranges left, then the negated objects do not cover the whole owner ip space
            return ownerIpRanges.Count > 0;
        }

        private static void CheckSpecificObj(NetworkLocation obj, List<IPAddressRange> ownerIps, List<NetworkLocation> relevantObjects, List<NetworkLocation> disregardedObjects)
        {
            if (obj.Object.Type.Name == ObjectType.Group)
            {
                foreach (var grpobj in obj.Object.ObjectGroupFlats.Select(o => o.Object))
                {
                    if (grpobj != null && CheckObj(grpobj, ownerIps))
                    {
                        relevantObjects.Add(obj);
                        break;
                    }
                }
            }
            else if (CheckObj(obj.Object, ownerIps))
            {
                relevantObjects.Add(obj);
            }
            else
            {
                disregardedObjects.Add(obj);
            }
        }

        private static bool CheckObj(NetworkObject obj, List<IPAddressRange> ownerIps, bool negated = false)
        {
            if (obj.IP == null)
            {
                return false;
            }

            if (obj.IsAnyObject())
            {
                return !negated;
            }

            foreach (var ownerIpRange in ownerIps)
                {
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
            foreach (var dev in mgt.Devices.Where(d => d.Rules != null))
            {
                foreach (var rule in dev.Rules!)
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
            if (negated)
            {
                if (CheckNegatedOverlap(networkLocations, ownerIps))
                {
                    foreach (var nwLoc in networkLocations)
                    {
                        mgt.RelevantObjectIds.Add(nwLoc.Object.Id);
                        mgt.HighlightedObjectIds.Add(nwLoc.Object.Id);
                    }
                }
                else
                {
                    foreach (var nwLoc in disregardedLocations)
                    {
                        mgt.RelevantObjectIds.Add(nwLoc.Object.Id);
                    }
                }
            }
            else
            {
                foreach (var nwObj in networkLocations.Select(nl => nl.Object))
                    {
                        mgt.RelevantObjectIds.Add(nwObj.Id);
                        mgt.HighlightedObjectIds.Add(nwObj.Id);
                        if (nwObj.Type.Name == ObjectType.Group)
                        {
                            if (nwObj.ObjectGroupFlats.Any(g => g.Object != null && CheckObj(g.Object, ownerIps)))
                            {
                                mgt.HighlightedObjectIds.Add(nwObj.Id);
                            }
                        }
                    }
                if (networkLocations.Length == 0)
                {
                    foreach (var nwLoc in disregardedLocations)
                    {
                        mgt.RelevantObjectIds.Add(nwLoc.Object.Id);
                    }
                }
            }
        }

        private static void PrepareRsbOutput(ManagementReport mgt)
        {
            foreach (var obj in mgt.ReportObjects)
            {
                obj.Highlighted = mgt.HighlightedObjectIds.Contains(obj.Id);
                if (obj.Type.Name == ObjectType.Group)
                {
                    foreach (var grpobj in obj.ObjectGroupFlats.Select(g => g.Object).Where(g => g != null))
                    {
                        grpobj!.Highlighted = mgt.HighlightedObjectIds.Contains(grpobj.Id);
                    }
                    foreach (var grpobj in obj.ObjectGroups.Select(g => g.Object).Where(g => g != null))
                    {
                        grpobj!.Highlighted = mgt.HighlightedObjectIds.Contains(grpobj.Id);
                    }
                }
            }
        }
    }
}
