using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter;
using FWO.Ui.Display;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    public abstract class ReportDevicesBase : ReportBase
    {
        private readonly DebugConfig _debugConfig;

        protected ReportDevicesBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType) : base(query, UserConfig, reportType)
        {
            if (userConfig.GlobalConfig is GlobalConfig globalConfig && !string.IsNullOrEmpty(globalConfig.DebugConfig))
            {
                _debugConfig = System.Text.Json.JsonSerializer.Deserialize<DebugConfig>(globalConfig.DebugConfig) ?? new();
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
                if (mgm.SuperManagerId != null)
                {
                    var superMgmImportId = managementReports.FirstOrDefault(m => m.Id == mgm.SuperManagerId)?.RelevantImportId ?? 0;
                    if (mgm.RelevantImportId < superMgmImportId)
                    {
                        mgm.RelevantImportId = superMgmImportId;
                        mgm.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = superMgmImportId; //TODO: resolve redundancy
                    }
                }
            }
            // filter out super managements
            managementReports = [.. managementReports.Where(m => m.IsSuperManager == false)];

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
                        ManagementReport? subMgm = managementReports.FirstOrDefault(r => r.Id == s.Id);
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async Task<bool> UsageDataAvailable(ApiConnection apiConnection, int devId)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                /* NOSONAR - temporarily disabled
                // TODO: the following only deals with first rulebase of a gateway:
                // return (await apiConnection.SendQueryAsync<List<AggregateCountLastHit>>(ReportQueries.getUsageDataCount, new { devId })
                //     ) NOSONAR[0].RulebasesOnGateway[0].Rulebase.RulesWithHits.Aggregate.Count > 0; NOSONAR
                */
                return false;   // TODO : implement and remove pragma warning disable once done
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
            report.AppendLine($"\"device filter\": \"{string.Join(" ", ReportData.ManagementData.Where(mgt => !mgt.Ignore).Select(m => m.NameAndRulebaseNames(" ")))}\",");
            report.AppendLine($"\"other filters\": \"{Query.RawFilter}\",");
            report.AppendLine($"\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",");
            report.AppendLine($"\"data protection level\": \"For internal use only\",");
            return $"{report}";
        }

        protected string ExportToJson<T>(Func<DeviceReport, bool> hasItems, Func<DeviceReport, ManagementReport, IEnumerable<T>> getItems, Func<T, string> renderItem, string itemsPropertyName)
        {
            StringBuilder report = new("{");
            report.Append(DisplayReportHeaderJson());
            report.AppendLine("\"managements\": [");

            foreach (var management in ReportData.ManagementData.Where(m => !m.Ignore && m.Devices != null && m.Devices.Any(hasItems)))
            {
                report.AppendLine($"{{\"{management.Name}\": {{");
                report.AppendLine("\"gateways\": [");

                foreach (var gateway in management.Devices.Where(hasItems))
                {
                    report.Append($"{{\"{gateway.Name}\": {{\n\"{itemsPropertyName}\": [");

                    var items = getItems(gateway, management).ToList();
                    if (items.Any())
                    {
                        foreach (var item in items)
                        {
                            report.Append(renderItem(item));
                        }
                        report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last comma
                    }

                    report.Append("]}},");
                }

                report = RuleDisplayBase.RemoveLastChars(report, 1);
                report.Append("]}},");
            }

            report = RuleDisplayBase.RemoveLastChars(report, 1);
            report.Append("]}");

            dynamic? json = JsonConvert.DeserializeObject(report.ToString());
            return JsonConvert.SerializeObject(json, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            });
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
            report.AppendLine($"# device filter: {string.Join(" ", ReportData.ManagementData.Where(mgt => !mgt.Ignore).Select(m => m.NameAndRulebaseNames(" ")))}");
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
            string deviceFilter = string.Join("; ", Array.ConvertAll(ReportData.ManagementData.Where(mgt => !mgt.Ignore).ToArray(), m => m.NameAndDeviceNames()));
            return GenerateHtmlFrameBase(title, filter, date, htmlReport, deviceFilter, Query.SelectedOwner?.Name, timefilter);
        }
    }
}
