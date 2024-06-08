using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using NetTools;
using System.Net;

namespace FWO.Report
{
    public class ReportAppRules : ReportRules
    {
        private List<IPAddressRange> ownerIps = [];

        public ReportAppRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await base.Generate(rulesPerFetch, apiConnection, callback, ct);
            await PrepareAppRulesReport(apiConnection);
        }

        private async Task PrepareAppRulesReport(ApiConnection apiConnection)
        {
            await GetAppServers(apiConnection);
            List<ManagementReport> relevantData = [];
            foreach(var mgt in ReportData.ManagementData)
            {
                ManagementReport relevantMgt = new(){Name = mgt.Name};
                foreach(var dev in mgt.Devices)
                {
                    DeviceReport relevantDevice = new(){Name = dev.Name};
                    if(dev.Rules != null)
                    {
                        relevantDevice.Rules = [];
                        foreach(var rule in dev.Rules)
                        {
                            (List<NetworkLocation> relevantFroms, List<NetworkLocation> disregardedFroms) = CheckNetworkObjects(rule.Froms);
                            (List<NetworkLocation> relevantTos, List<NetworkLocation> disregardedTos) = CheckNetworkObjects(rule.Tos);

                            if(relevantFroms.Count > 0 || relevantTos.Count > 0)
                            {
                                rule.Froms = [.. relevantFroms];
                                rule.Tos = [.. relevantTos];
                                rule.DisregardedFroms = [.. disregardedFroms];
                                rule.DisregardedTos = [.. disregardedTos];
                                relevantDevice.Rules = [.. relevantDevice.Rules, rule];
                            }
                        }
                        if(relevantDevice.Rules.Length > 0)
                        {
                            relevantMgt.Devices = [.. relevantMgt.Devices, relevantDevice];
                        }
                    }
                }
                if(relevantMgt.Devices.Length > 0)
                {
                    relevantData.Add(relevantMgt);
                }
            }
            ReportData.ManagementData = relevantData;
        }

        private async Task GetAppServers(ApiConnection apiConnection)
        {
            List<ModellingAppServer> appServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, 
                new { appId = Query.SelectedOwner?.Id });
            ownerIps = [.. appServers.ConvertAll(s => new IPAddressRange(IPAddress.Parse(DisplayBase.StripOffNetmask(s.Ip)),
                IPAddress.Parse(DisplayBase.StripOffNetmask(s.IpEnd != "" ? s.IpEnd : s.Ip))))];
        }

        private (List<NetworkLocation>, List<NetworkLocation>) CheckNetworkObjects(NetworkLocation[] objList)
        {
            List<NetworkLocation> relevantObjects = [];
            List<NetworkLocation> disregardedObjects = [];
            foreach(var obj in objList.Where(o => o.Object.IP != null))
            {
                bool found = false;
                foreach(var ownerIpRange in ownerIps)
                {
                    if(ComplianceNetworkZone.OverlapExists(new IPAddressRange(IPAddress.Parse(DisplayBase.StripOffNetmask(obj.Object.IP)),
                        IPAddress.Parse(DisplayBase.StripOffNetmask(obj.Object.IpEnd != "" ? obj.Object.IpEnd : obj.Object.IP))), ownerIpRange))
                    {
                        relevantObjects.Add(obj);
                        found = true;
                        break;
                    }
                }
                if(!found)
                {
                    disregardedObjects.Add(obj);
                }
            }
            return (relevantObjects, disregardedObjects);
        }

        // public override string SetDescription()
        // {
        //     return "";
        // }

        // public override string ExportToJson()
        // {
        //     return "";
        // }

        // public override string ExportToHtml()
        // {
        //     return "";
        // }

        // public override string ExportToCsv()
        // {
        //     return "";
        // }
    }
}
