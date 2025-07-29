using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Basics;
using FWO.Config.Api;
using System.Text;
using FWO.Data.Middleware;
using System.Text.Json;
using FWO.Logging;

namespace FWO.Report
{
    public abstract class ReportDevicesBase : ReportBase
    {
        private readonly DebugConfig _debugConfig;

        public ReportDevicesBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType) : base(query, UserConfig, reportType)
        {
            if (userConfig.GlobalConfig is GlobalConfig globalConfig && !string.IsNullOrEmpty(globalConfig.DebugConfig))
            {
                _debugConfig = JsonSerializer.Deserialize<DebugConfig>(globalConfig.DebugConfig) ?? new();
            }
            else
            {
                _debugConfig = new();
            }
        }

        public async Task<List<ManagementReport>> GetRelevantImportIds(ApiConnection apiConnection, string? timestamp = null)
        {
            Dictionary<string, object> ImpIdQueryVariables = new()
            {
                ["time"] = timestamp ?? (Query.ReportTimeString != "" ? Query.ReportTimeString : DateTime.Now.ToString(DynGraphqlQuery.fullTimeFormat)),
                ["mgmIds"] = Query.RelevantManagementIds
            };
            return await apiConnection.SendQueryAsync<List<ManagementReport>>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);
        }

        public static async Task<(List<string> unsupportedList, DeviceFilter reducedDeviceFilter)> GetUsageDataUnsupportedDevices(ApiConnection apiConnection, DeviceFilter deviceFilter)
        {
            List<string> unsupportedList = [];
            DeviceFilter reducedDeviceFilter = new(deviceFilter);
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
                if (!DeviceFilter.IsSelectedManagement(management))
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
                // TODO: the following only deals with first rulebase of a gateway:
                // return (await apiConnection.SendQueryAsync<List<AggregateCountLastHit>>(ReportQueries.getUsageDataCount, new { devId })
                //     )[0].RulebasesOnGateway[0].Rulebase.RulesWithHits.Aggregate.Count > 0;
                return false;   // TODO: implement
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool NoRuleFound()
        {
            TryWriteExtendedLog("Checking if rules were found in device report.", _debugConfig.ExtendedLogComplianceCheck);

            foreach (var mgmt in ReportData.ManagementData)
            {
                TryWriteExtendedLog($"Checking if rules were found in management {mgmt.Id} ({mgmt.Name}).", _debugConfig.ExtendedLogComplianceCheck);

                foreach (var dev in mgmt.Devices)
                {
                    TryWriteExtendedLog($"Checking if rules were found in device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogComplianceCheck);

                    if (dev != null && dev.RulebaseLinks != null && dev.RulebaseLinks.Length > 0)
                    {
                        int? nextRulebaseId = dev.RulebaseLinks.FirstOrDefault(_ => _.IsInitialRulebase())?.NextRulebaseId;
                        if (nextRulebaseId != null && mgmt.Rulebases.FirstOrDefault(_ => _.Id == nextRulebaseId)?.Rules.Length > 0)
                        {
                            TryWriteExtendedLog($"Found rules in device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogComplianceCheck);

                            return false;
                        }

                        TryWriteExtendedLog($"No rules found in device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogComplianceCheck);

                        return true;
                    }
                }
            }

            TryWriteExtendedLog("No rules found in any device.", _debugConfig.ExtendedLogComplianceCheck);

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
            StringBuilder report = new();
            report.AppendLine($"\"report type\": \"{userConfig.GetText(ReportType.ToString())}\",");
            report.AppendLine($"\"report generation date\": \"{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
            if (!ReportType.IsChangeReport())
            {
                report.AppendLine($"\"date of configuration shown\": \"{DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
            }
            report.AppendLine($"# device filter: {string.Join(" ", ReportData.ManagementData.Where(mgt => !mgt.Ignore).Cast<ManagementReportController>().Select(m => m.NameAndRulebaseNames(" ")))}");
            report.AppendLine($"\"other filters\": \"{Query.RawFilter}\",");
            report.AppendLine($"\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",");
            report.AppendLine($"\"data protection level\": \"For internal use only\",");
            return $"{report}";
        }

        public string DisplayReportHeaderCsv()
        {
            StringBuilder report = new();
            report.AppendLine($"# report type: {userConfig.GetText(ReportType.ToString())}");
            report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
            if (!ReportType.IsChangeReport())
            {
                report.AppendLine($"# date of configuration shown: {DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
            }
            report.AppendLine($"# device filter: {string.Join(" ", ReportData.ManagementData.Where(mgt => !mgt.Ignore).Cast<ManagementReportController>().Select(m => m.NameAndRulebaseNames(" ")))}");
            report.AppendLine($"# other filters: {Query.RawFilter}");
            report.AppendLine($"# report generator: Firewall Orchestrator - https://fwo.cactus.de/en");
            report.AppendLine($"# data protection level: For internal use only");
            report.AppendLine($"#");
            return $"{report}";
        }

        public static string GetReportDevicesLinkAddress(OutputLocation location, int mgmtId, string type, int chapterNumber, long id, ReportType reportType)
        {
            return GetLinkAddress(location, $"m{mgmtId}", type, chapterNumber, id, reportType);
        }


        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport, TimeFilter? timefilter = null)
        {
            // return GenerateHtmlFrameBase(title, filter, date, htmlReport,
            //     string.Join("; ", ReportData.ManagementData.Where(mgt => !mgt.Ignore).Select(m => new ManagementReportController(m).NameAndRulebaseNames())),
            //     Query.SelectedOwner?.Name);
            string deviceFilter = string.Join("; ", Array.ConvertAll(ReportData.ManagementData.Where(mgt => !mgt.Ignore).ToArray(), m => m.NameAndDeviceNames()));
            return GenerateHtmlFrameBase(title, filter, date, htmlReport, deviceFilter, Query.SelectedOwner?.Name, timefilter);
        }

        private void TryWriteExtendedLog(string message, bool condition)
        {
            if (condition && _debugConfig.ExtendedLogReportGeneration)
            {
                Log.WriteInfo("Device Report", message);
            }
        }
    }
}
