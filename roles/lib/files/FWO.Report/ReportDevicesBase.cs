﻿using FWO.Api.Client;
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
                [QueryVar.Time] = timestamp ?? (Query.ReportTimeString != "" ? Query.ReportTimeString : DateTime.Now.ToString(DynGraphqlQuery.fullTimeFormat)),
                [QueryVar.MgmIds] = Query.RelevantManagementIds
            };
            List<ManagementReport> managementReports = await apiConnection.SendQueryAsync<List<ManagementReport>>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);
            // set max import id as relevant import id
            managementReports.ForEach(mgm => mgm.RelevantImportId = mgm.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1);

            // handle management imported as sub-management as well as part of super management
            foreach (var mgm in managementReports)
            {
                if (mgm.SubManagements.Count > 0)
                {
                    foreach (var s in mgm.SubManagements)
                    {
                        ManagementReport? subMgm = managementReports.Where(r => r.Id == s.Id).FirstOrDefault();
                        if (subMgm == null)
                            continue;
                        long subMgmImportId = subMgm.RelevantImportId ?? -1;
                        long superMgmImportId = mgm.RelevantImportId ?? -1;
                        if (subMgmImportId < superMgmImportId)
                        {
                            subMgm.RelevantImportId = superMgmImportId;
                        }
                    }
                }
            }
            managementReports = [.. managementReports.Where(r => r.SubManagements.Count == 0)]; // filter out super managements

            return managementReports;
        }

        public async Task<List<ManagementReport>> GetImportIdsInTimeRange(ApiConnection apiConnection, string startTime, string stopTime, bool? ruleChangeRequired = null)
        {
            var queryVariables = new
            {
                start_time = startTime,
                end_time = stopTime,
                mgmIds = Query.RelevantManagementIds,
                ruleChangesFound = ruleChangeRequired
            };
            List<ManagementReport> managementReports = await apiConnection.SendQueryAsync<List<ManagementReport>>(ReportQueries.getRelevantImportIdsInTimeRange, queryVariables);

            foreach (var mgm in managementReports)
            {
                mgm.RelevantImportId = mgm.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1;
                if (mgm.SubManagements.Count > 0)
                {
                    foreach (var s in mgm.SubManagements)
                    {
                        ManagementReport? subMgm = managementReports.Where(r => r.Id == s.Id).FirstOrDefault();
                        if (subMgm == null)
                            continue;
                        subMgm.ImportControls = [.. subMgm.ImportControls, .. mgm.ImportControls];
                        subMgm.ImportControls.Sort((ic1, ic2) => ic1.ControlId.CompareTo(ic2.ControlId));
                    }
                }
            }
            managementReports = [.. managementReports.Where(r => r.SubManagements.Count == 0)]; // filter out super managements

            return managementReports;
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
            Log.TryWriteLog(LogType.Info, "Device Report", "Checking if rules were found in device report.", _debugConfig.ExtendedLogReportGeneration);

            foreach (ManagementReport mgmt in ReportData.ManagementData)
            {
                Log.TryWriteLog(LogType.Info, "Device Report", $"Checking if rules were found in management {mgmt.Id} ({mgmt.Name}).", _debugConfig.ExtendedLogReportGeneration);

                foreach (DeviceReport dev in mgmt.Devices)
                {
                    if (!CheckDeviceHasNoRules(mgmt, dev))
                    {
                        return false;
                    }
                }
            }

            Log.TryWriteLog(LogType.Info, "Device Report", "No rules found in any device.", _debugConfig.ExtendedLogReportGeneration);

            return true;
        }

        private bool CheckDeviceHasNoRules(ManagementReport mgmt, DeviceReport dev)
        {
            Log.TryWriteLog(LogType.Info, "Device Report", $"Checking if rules were found in device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogReportGeneration);

            if (dev.RulebaseLinks.Length > 0)
            {
                int? nextRulebaseId = dev.RulebaseLinks.FirstOrDefault(_ => _.IsInitialRulebase())?.NextRulebaseId;
                if (nextRulebaseId != null)
                {
                    Log.TryWriteLog(LogType.Info, "Device Report", "Found initial rulebase", _debugConfig.ExtendedLogReportGeneration);

                    foreach (RulebaseLink link in dev.RulebaseLinks)
                    {
                        if (mgmt.Rulebases.FirstOrDefault(rulebase => rulebase.Id == link.NextRulebaseId) is RulebaseReport rulebase && rulebase.Rules.Length > 0)
                        {
                            Log.TryWriteLog(LogType.Info, "Device Report", $"Found rules in rulebase {rulebase.Id} ({rulebase.Name}) of device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogReportGeneration);
                            return false;
                        }
                    }
                }
                else
                {
                    Log.TryWriteLog(LogType.Info, "Device Report", "No initial rulebase found.", _debugConfig.ExtendedLogReportGeneration);
                }

                Log.TryWriteLog(LogType.Info, "Device Report", $"No rules found in device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogReportGeneration);
            }
            else
            {
                Log.TryWriteLog(LogType.Info, "Device Report", $"No rulebase links found in device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogReportGeneration);
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


    }
}
