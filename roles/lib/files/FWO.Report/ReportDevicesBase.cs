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
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    public abstract class ReportDevicesBase : ReportBase
    {
        private readonly DebugConfig _debugConfig;

        /// <summary>
        /// Imports this report was generated for per management id. Super managers are contained as well, because
        /// the rules of their global rulebases carry the id of the super manager.
        /// </summary>
        private readonly Dictionary<int, ImportRange> importRanges = [];

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

        /// <inheritdoc/>
        public override ImportRange? GetImportRange(int managementId)
        {
            return importRanges.TryGetValue(managementId, out ImportRange importRange) ? importRange : null;
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
            StartImportRanges(managementReports);
            // filter out super managements
            managementReports = [.. managementReports.Where(m => m.IsSuperManager == false)];

            return managementReports;
        }

        public async Task<List<ManagementReport>> GetImportIdsInTimeRange(ApiConnection apiConnection, string startTime, string stopTime, bool? ruleChangeRequired = null, bool includeObjects = false)
        {
            var queryVariables = new
            {
                start_time = startTime,
                end_time = stopTime,
                mgmIds = Query.RelevantManagementIds,
                ruleChangesFound = includeObjects ? null : ruleChangeRequired
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
            ExtendImportRanges(managementReports);
            managementReports = [.. managementReports.Where(r => r.SubManagements.Count == 0)]; // filter out super managements

            return managementReports;
        }

        /// <summary>
        /// Starts the import ranges of a new report generation with the import valid at the (start) time of the report.
        /// </summary>
        private void StartImportRanges(List<ManagementReport> managements)
        {
            importRanges.Clear();
            foreach (ManagementReport management in managements)
            {
                long importId = management.RelevantImportId ?? -1;
                importRanges[management.Id] = new(importId, importId);
            }
        }

        /// <summary>
        /// Extends the import ranges to the last import found in the report period.
        /// </summary>
        private void ExtendImportRanges(List<ManagementReport> managementsWithImportsInPeriod)
        {
            foreach (ManagementReport management in managementsWithImportsInPeriod.Where(m => m.ImportControls.Count > 0))
            {
                if (importRanges.TryGetValue(management.Id, out ImportRange importRange))
                {
                    long lastImportId = management.ImportControls.Max(import => import.ControlId);
                    importRanges[management.Id] = importRange with { End = Math.Max(importRange.End, lastImportId) };
                }
            }
        }

        public static async Task<(List<string> unsupportedList, DeviceFilter reducedDeviceFilter)> GetUsageDataUnsupportedDevices(ApiConnection apiConnection, DeviceFilter deviceFilter)
        {
            List<string> unsupportedList = [];
            // a deep copy, so that deselecting devices here does not clear the selection the caller keeps showing
            DeviceFilter reducedDeviceFilter = deviceFilter.Clone();
            foreach (ManagementSelect management in reducedDeviceFilter.Managements)
            {
                foreach (DeviceSelect device in management.Devices)
                {
                    if (device.Selected)
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

        public override bool NoChangesFound()
        {
            Log.TryWriteLog(LogType.Info, "Management Report", "Checking if changes (rules or objects) were found in management report.", _debugConfig.ExtendedLogReportGeneration);

            foreach (ManagementReport mgmt in ReportData.ManagementData)
            {
                Log.TryWriteLog(LogType.Info, "Management Report", $"Checking if changes (rules or objects) were found in management {mgmt.Id} ({mgmt.Name}).", _debugConfig.ExtendedLogReportGeneration);

                if (mgmt.RuleChanges != null && mgmt.RuleChanges.Length > 0)
                {
                    return false;
                }
            }

            Log.TryWriteLog(LogType.Info, "Management Report", "No changes (rules or objects) found in any Management.", _debugConfig.ExtendedLogReportGeneration);

            return true;
        }

        private bool CheckDeviceHasNoRules(ManagementReport mgmt, DeviceReport dev)
        {
            Log.TryWriteLog(LogType.Info, "Device Report", $"Checking if rules were found in device {dev.Id} ({dev.Name}).", _debugConfig.ExtendedLogReportGeneration);

            if (dev.RulebaseLinks.Length > 0)
            {
                int? nextRulebaseId = dev.RulebaseLinks.FirstOrDefault(_ => _.IsInitial)?.NextRulebaseId;
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

        /// <summary>
        /// Label of the device/rulebase selection line in the CSV and JSON report headers.
        /// In management rulebases view the listed entries are the rule trees of the selected start rulebases.
        /// </summary>
        private string HeaderFilterLabel => Query.ManagementRulebaseView ? "rulebase filter" : "device filter";

        public string DisplayReportHeaderJson()
        {
            StringBuilder report = new();
            report.AppendLine($"\"report type\": \"{userConfig.GetText(ReportType.ToString())}\",");
            report.AppendLine($"\"report generation date\": \"{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
            if (!ReportType.IsChangeReport())
            {
                report.AppendLine($"\"date of configuration shown\": \"{DateTime.Parse(Query.ReportTimeString, CultureInfo.InvariantCulture).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
            }
            report.AppendLine($"\"{HeaderFilterLabel}\": \"{string.Join(" ", ReportData.ManagementData.Where(mgt => !mgt.Ignore).Select(m => m.NameAndRulebaseNames(" ")))}\",");
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
                    if (items.Count != 0)
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
                report.AppendLine($"# date of configuration shown: {DateTime.Parse(Query.ReportTimeString, CultureInfo.InvariantCulture).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
            }
            report.AppendLine($"# {HeaderFilterLabel}: {string.Join(" ", ReportData.ManagementData.Where(mgt => !mgt.Ignore).Select(m => m.NameAndRulebaseNames(" ")))}");
            report.AppendLine($"# other filters: {Query.RawFilter}");
            report.AppendLine($"# report generator: Firewall Orchestrator - https://fwo.cactus.de/en");
            report.AppendLine($"# data protection level: For internal use only");
            report.AppendLine($"#");
            return $"{report}";
        }

        /// <summary>
        /// Builds the link from an object shown in a report to its entry below a management in the report tab of the right sidebar.
        /// </summary>
        /// <param name="location">Output location of the report.</param>
        /// <param name="mgmtId">Id of the management the object is listed for.</param>
        /// <param name="type">Object category string.</param>
        /// <param name="chapterNumber">Chapter number.</param>
        /// <param name="id">Object id.</param>
        /// <returns>The link address.</returns>
        public static string GetReportDevicesLinkAddress(OutputLocation location, int mgmtId, string type, int chapterNumber, long id)
        {
            return GetLinkAddress(location, $"m{mgmtId}", type, chapterNumber, id);
        }


        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport, TimeFilter? timefilter = null)
        {
            string deviceFilter = string.Join("; ", Array.ConvertAll(ReportData.ManagementData.Where(mgt => !mgt.Ignore).ToArray(), m => ReportType.IsRulebaseReport() ? m.Name : m.NameAndDeviceNames()));
            return GenerateHtmlFrameBase(title, filter, date, htmlReport, new HtmlFrameOptions
            {
                OtherFilter = deviceFilter,
                // in management rulebases view the devices are the rule trees of the selected start rulebases
                OtherFilterTextKey = Query.ManagementRulebaseView ? "rulebases" : null,
                OwnerFilter = Query.SelectedOwner?.Name,
                TimeFilter = timefilter
            });
        }
    }
}
