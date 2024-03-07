using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text;

namespace FWO.Report
{
    public abstract class ReportDevicesBase : ReportBase
    {
        public ManagementReport[] ManagementReports = new ManagementReport[] { };

        public ReportDevicesBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType) : base (query, UserConfig, reportType)
        {}

        public async Task<ManagementReport[]> getRelevantImportIds(ApiConnection apiConnection)
        {
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            ImpIdQueryVariables["time"] = Query.ReportTimeString != "" ? Query.ReportTimeString : DateTime.Now.ToString(DynGraphqlQuery.fullTimeFormat);
            ImpIdQueryVariables["mgmIds"] = Query.RelevantManagementIds;
            return await apiConnection.SendQueryAsync<ManagementReport[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);
        }

        public static async Task<(List<string> unsupportedList, DeviceFilter reducedDeviceFilter)> GetUsageDataUnsupportedDevices(ApiConnection apiConnection, DeviceFilter deviceFilter)
        {
            List<string> unsupportedList = new List<string>();
            DeviceFilter reducedDeviceFilter = new DeviceFilter(deviceFilter);
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
            foreach(var mgmt in ManagementReports)
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
            foreach (var managementReport in ManagementReports.Where(mgt => !mgt.Ignore))
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
            report.AppendLine($"\"device filter\": \"{string.Join("; ", Array.ConvertAll(ManagementReports, m => m.NameAndDeviceNames()))}\",");
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
            report.AppendLine($"# device filter: {string.Join(" ", Array.ConvertAll(ManagementReports.Where(mgt => !mgt.Ignore).ToArray(), m => m.NameAndDeviceNames(" ")))}");
            report.AppendLine($"# other filters: {Query.RawFilter}");
            report.AppendLine($"# report generator: Firewall Orchestrator - https://fwo.cactus.de/en");
            report.AppendLine($"# data protection level: For internal use only");
            report.AppendLine($"#");
            return $"{report}";
        }

        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport)
        {
            if (string.IsNullOrEmpty(htmlExport))
            {
                HtmlTemplate = HtmlTemplate.Replace("##Title##", title);
                HtmlTemplate = HtmlTemplate.Replace("##Filter##", filter);
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedOn##", userConfig.GetText("generated_on"));
                HtmlTemplate = HtmlTemplate.Replace("##Date##", date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                if(ReportType.IsChangeReport())
                {
                    string timeRange = $"{userConfig.GetText("change_time")}: " +
                        $"{userConfig.GetText("from")}: {ToUtcString(Query.QueryVariables["start"]?.ToString())}, " +
                        $"{userConfig.GetText("until")}: {ToUtcString(Query.QueryVariables["stop"]?.ToString())}";
                    HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##: ##GeneratedFor##", timeRange);
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##", userConfig.GetText("date_of_config"));
                    HtmlTemplate = HtmlTemplate.Replace("##GeneratedFor##", ToUtcString(Query.ReportTimeString));
                }
                HtmlTemplate = HtmlTemplate.Replace("##DeviceFilter##", string.Join("; ", Array.ConvertAll(ManagementReports.Where(mgt => !mgt.Ignore).ToArray(), m => m.NameAndDeviceNames())));
                HtmlTemplate = HtmlTemplate.Replace("##Body##", htmlReport.ToString());
                htmlExport = HtmlTemplate.ToString();
            }
            return htmlExport;
        }
    }
}
