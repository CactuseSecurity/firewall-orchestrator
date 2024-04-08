﻿using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text;

namespace FWO.Report
{
    public abstract class ReportDevicesBase : ReportBase
    {
        public ReportDevicesBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType) : base (query, UserConfig, reportType)
        {}

        public async Task<List<ManagementReport>> getRelevantImportIds(ApiConnection apiConnection)
        {
            Dictionary<string, object> ImpIdQueryVariables = new ();
            ImpIdQueryVariables["time"] = Query.ReportTimeString != "" ? Query.ReportTimeString : DateTime.Now.ToString(DynGraphqlQuery.fullTimeFormat);
            ImpIdQueryVariables["mgmIds"] = Query.RelevantManagementIds;
            return await apiConnection.SendQueryAsync<List<ManagementReport>>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);
        }

        public static async Task<(List<string> unsupportedList, DeviceFilter reducedDeviceFilter)> GetUsageDataUnsupportedDevices(ApiConnection apiConnection, DeviceFilter deviceFilter)
        {
            List<string> unsupportedList = new ();
            DeviceFilter reducedDeviceFilter = new (deviceFilter);
            foreach (ManagementSelect management in reducedDeviceFilter.Managements)
            {
                foreach (DeviceSelect device in management.Devices)
                {
                    if (device.Selected && !await UsageDataAvailable(apiConnection, device.Id))
                    {
                        unsupportedList.Add(device.Name ?? "?");
                        device.Selected = false;
                    }
                }
                if(!DeviceFilter.IsSelectedManagement(management))
                {
                    management.Selected = false;
                }
            }
            return (unsupportedList, reducedDeviceFilter);
        }
        
        private static async Task<bool> UsageDataAvailable(ApiConnection apiConnection, int devId)
        {
            try
            {
                return (await apiConnection.SendQueryAsync<AggregateCount>(ReportQueries.getUsageDataCount, new {devId = devId})).Aggregate.Count > 0;
            }
            catch(Exception)
            {
                return false;
            }
        }

        public override bool NoRuleFound()
        {
            foreach(var mgmt in ReportData.ManagementData)
            {
                foreach(var dev in mgmt.Devices)
                {
                    if(dev.Rules != null && dev.Rules.Count() > 0)
                    {
                        return false;
                    }
                    if(dev.RuleChanges != null && dev.RuleChanges.Count() > 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public override string SetDescription()
        {
            int managementCounter = 0;
            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                managementCounter++;
            }
            return $"{managementCounter} {userConfig.GetText("managements")}";
        }

        public string DisplayReportHeaderJson()
        {
            StringBuilder report = new ();
            report.AppendLine($"\"report type\": \"{userConfig.GetText(ReportType.ToString())}\",");
            report.AppendLine($"\"report generation date\": \"{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
            if(!ReportType.IsChangeReport())
            {
                report.AppendLine($"\"date of configuration shown\": \"{DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
            }
            report.AppendLine($"\"device filter\": \"{string.Join("; ", Array.ConvertAll(ReportData.ManagementData.ToArray(), m => m.NameAndDeviceNames()))}\",");
            report.AppendLine($"\"other filters\": \"{Query.RawFilter}\",");
            report.AppendLine($"\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",");
            report.AppendLine($"\"data protection level\": \"For internal use only\",");
            return $"{report}";
        }

        public string DisplayReportHeaderCsv()
        {
            StringBuilder report = new ();
            report.AppendLine($"# report type: {userConfig.GetText(ReportType.ToString())}");
            report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
            if(!ReportType.IsChangeReport())
            {
                report.AppendLine($"# date of configuration shown: {DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
            }
            report.AppendLine($"# device filter: {string.Join(" ", Array.ConvertAll(ReportData.ManagementData.Where(mgt => !mgt.Ignore).ToArray(), m => m.NameAndDeviceNames(" ")))}");
            report.AppendLine($"# other filters: {Query.RawFilter}");
            report.AppendLine($"# report generator: Firewall Orchestrator - https://fwo.cactus.de/en");
            report.AppendLine($"# data protection level: For internal use only");
            report.AppendLine($"#");
            return $"{report}";
        }

        public static string ConstructLink(string type, string symbol, long id, string name, OutputLocation location, int mgmtId, string style)
        {
            return ConstructLink(type, symbol, id, name, location, $"m{mgmtId}", style);
        }

        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport)
        {
            return GenerateHtmlFrame(title, filter, date, htmlReport, string.Join("; ", Array.ConvertAll(ReportData.ManagementData.Where(mgt => !mgt.Ignore).ToArray(), m => m.NameAndDeviceNames())));
        }
    }
}
