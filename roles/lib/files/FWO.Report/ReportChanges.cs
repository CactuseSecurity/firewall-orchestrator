using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter;
using FWO.Ui.Display;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    public class ReportChanges : ReportDevicesBase
    {
        private const int ColumnCount = 13;

        private readonly TimeFilter timeFilter;

        public ReportChanges(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, TimeFilter timeFilter) : base(query, userConfig, reportType)
        {
            this.timeFilter = timeFilter;
        }

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            Query.QueryVariables[QueryVar.Limit] = elementsPerFetch;
            Query.QueryVariables[QueryVar.Offset] = 0;

            (string startTime, string stopTime) = DynGraphqlQuery.ResolveTimeRange(timeFilter);
            Dictionary<int, List<long>> managementImportIds = [];
            int queriesNeeded = 0;

            queriesNeeded += await FetchInitialManagementData(apiConnection, startTime, stopTime, managementImportIds);

            queriesNeeded += await FetchAdditionalChanges(apiConnection, managementImportIds, elementsPerFetch, callback, queriesNeeded, ct);

            Log.WriteDebug("Generate Changes Report", $"Finished generating changes report with {queriesNeeded} queries.");
        }

        private async Task<int> FetchInitialManagementData(ApiConnection apiConnection, string startTime, string stopTime, Dictionary<int, List<long>> managementImportIds)
        {
            int queriesNeeded = 0;
            List<ManagementReport> managementsWithRelevantImportId = await GetRelevantImportIds(apiConnection, startTime);
            List<ManagementReport> managementsWithImportIds = await GetImportIdsInTimeRange(apiConnection, startTime, stopTime, ruleChangeRequired: true);
            foreach (var management in managementsWithRelevantImportId)
            {
                List<long> importIdLastBeforeRange = [management.RelevantImportId ?? -1];
                List<long> importIdsInRange = [.. managementsWithImportIds.Where(m => m.Id == management.Id).SelectMany(m => m.ImportControls).Select(ic => ic.ControlId)];
                List<long> relevantImportIds = [.. importIdLastBeforeRange, .. importIdsInRange];

                SetMgtQueryVars(management.Id, relevantImportIds[0], relevantImportIds[1]);
                ManagementReport managementReport = (await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables)).First();

                queriesNeeded += 1;
                ReportData.ManagementData.Add(managementReport);
                managementImportIds.Add(management.Id, relevantImportIds);
            }
            return queriesNeeded;
        }

        private async Task<int> FetchAdditionalChanges(ApiConnection apiConnection, Dictionary<int, List<long>> managementImportIds, int elementsPerFetch,
            Func<ReportData, Task> callback, int queriesNeeded, CancellationToken ct)
        {
            Query.QueryVariables[QueryVar.Offset] = elementsPerFetch;
            int maxImports = managementImportIds.Values.Select(v => v.Count).Max();

            for (int i = 1; i < maxImports; i++)
            {
                bool continueFetching = true;
                while (continueFetching)
                {
                    if (ct.IsCancellationRequested)
                    {
                        Log.WriteDebug("Generate Changes Report", "Task cancelled");
                        ct.ThrowIfCancellationRequested();
                    }

                    (bool anyContinue, int queries) = await ProcessManagementsForAdditionalChanges(apiConnection, managementImportIds, i, elementsPerFetch);
                    queriesNeeded += queries;
                    continueFetching = anyContinue;

                    await callback(ReportData);
                    Query.QueryVariables[QueryVar.Offset] = (int)Query.QueryVariables[QueryVar.Offset] + elementsPerFetch;
                }
                Query.QueryVariables[QueryVar.Offset] = 0;
            }
            return queriesNeeded;
        }

        private async Task<(bool anyContinue, int queries)> ProcessManagementsForAdditionalChanges(ApiConnection apiConnection, Dictionary<int, List<long>> managementImportIds, int i, int elementsPerFetch)
        {
            bool anyContinue = false;
            int queries = 0;

            foreach (var management in ReportData.ManagementData)
            {
                if (managementImportIds.ContainsKey(management.Id) && managementImportIds[management.Id].Count > i) // Null check
                {
                    long importIdOld = managementImportIds[management.Id][i - 1];
                    long importIdNew = managementImportIds[management.Id][i];
                    SetMgtQueryVars(management.Id, importIdOld, importIdNew);

                    ManagementReport newData = (await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables)).First(); // Error
                    (bool newObjects, Dictionary<string, int> maxAddedCounts) = management.Merge(newData);
                    queries++;

                    if (newObjects && maxAddedCounts.Values.Any(v => v >= elementsPerFetch))
                    { 
                        anyContinue = true; 
                    }
                }
            }
            return (anyContinue, queries);
        }

        private void SetMgtQueryVars(int mgmId, long importIdOld, long importIdNew)
        {
            Query.QueryVariables[QueryVar.MgmId] = mgmId;
            Query.QueryVariables[QueryVar.ImportIdOld] = importIdOld;
            Query.QueryVariables[QueryVar.ImportIdNew] = importIdNew;
        }

        public override Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            throw new NotImplementedException();
        }

        public override string SetDescription()
        {
            int managementCounter = 0;
            int deviceCounter = 0;
            int ruleChangeCounter = 0;
            foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.RuleChanges != null && device.RuleChanges.Length > 0)))
            {
                managementCounter++;
                foreach (var device in management.Devices.Where(dev => dev.RuleChanges != null && dev.RuleChanges.Length > 0))
                {
                    deviceCounter++;
                    ruleChangeCounter += device.RuleChanges!.Length;
                }
            }
            return $"{managementCounter} {userConfig.GetText("managements")}, {deviceCounter} {userConfig.GetText("gateways")}, {ruleChangeCounter} {userConfig.GetText("changes")}";
        }

        public override string ExportToCsv()
        {
            if (ReportType.IsResolvedReport())
            {
                StringBuilder report = new();
                RuleChangeDisplayCsv ruleChangeDisplayCsv = new(userConfig);

                report.Append(DisplayReportHeaderCsv());
                report.AppendLine($"\"management-name\",\"device-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"");

                foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                        Array.Exists(mgt.Devices, device => device.RuleChanges != null && device.RuleChanges.Length > 0)))
                {
                    foreach (var gateway in management.Devices.Where(g => g.RuleChanges != null && g.RuleChanges.Length > 0))
                    {
                        foreach (var ruleChange in gateway.RuleChanges!)
                        {
                            report.Append(ruleChangeDisplayCsv.OutputCsv(management.Name));
                            report.Append(ruleChangeDisplayCsv.OutputCsv(gateway.Name));
                            report.Append(ruleChangeDisplayCsv.DisplayChangeTime(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayChangeAction(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayName(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplaySourceZone(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplaySource(ruleChange, ReportType));
                            report.Append(ruleChangeDisplayCsv.DisplayDestinationZone(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayDestination(ruleChange, ReportType));
                            report.Append(ruleChangeDisplayCsv.DisplayServices(ruleChange, ReportType));
                            report.Append(ruleChangeDisplayCsv.DisplayAction(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayTrack(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayEnabled(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayUid(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayComment(ruleChange));
                            report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                            report.AppendLine("");
                        }
                    }
                }
                return report.ToString();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new();
            RuleChangeDisplayHtml ruleChangeDisplayHtml = new(userConfig);

            foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.RuleChanges != null && device.RuleChanges.Length > 0)))
            {
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{management.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (var device in management.Devices)
                {
                    report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{device.Name}</h4>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("change_time")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("change_type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("source_zone")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("destination_zone")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("action")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("track")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("enabled")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                    report.AppendLine("</tr>");

                    if (device.RuleChanges != null)
                    {
                        foreach (var ruleChange in device.RuleChanges)
                        {
                            report.AppendLine("<tr>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeTime(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeAction(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayName(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplaySourceZone(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplaySource(ruleChange, OutputLocation.export, ReportType)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayDestinationZone(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayDestination(ruleChange, OutputLocation.export, ReportType)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayServices(ruleChange, OutputLocation.export, ReportType)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayAction(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayTrack(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayEnabled(ruleChange, OutputLocation.export)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayUid(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayComment(ruleChange)}</td>");
                            report.AppendLine("</tr>");
                        }
                    }
                    else
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td colspan=\"{ColumnCount}\">{userConfig.GetText("no_changes_found")}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                    report.AppendLine("<hr>");
                }
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report, timeFilter);
        }

        public override string ExportToJson()
        {
            if (ReportType.IsResolvedReport())
            {
                return ExportResolvedChangesToJson();
            }
            else if (ReportType.IsChangeReport())
            {
                return System.Text.Json.JsonSerializer.Serialize(ReportData.ManagementData.Where(mgt => !mgt.Ignore), new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                return "";
            }
        }

        private string ExportResolvedChangesToJson()
        {
            RuleChangeDisplayJson ruleChangeDisplayJson = new(userConfig);

            return ExportToJson(
                hasItems: dev => dev.RuleChanges != null && dev.RuleChanges.Length > 0,
                getItems: (dev, mgmt) => dev.RuleChanges!,
                renderItem: ruleChange =>
                {
                    var sb = new StringBuilder("{");
                    sb.Append(ruleChangeDisplayJson.DisplayChangeTime(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayChangeAction(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayName(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplaySourceZones(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplaySourceNegated(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplaySource(ruleChange, ReportType));
                    sb.Append(ruleChangeDisplayJson.DisplayDestinationZones(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayDestinationNegated(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayDestination(ruleChange, ReportType));
                    sb.Append(ruleChangeDisplayJson.DisplayServiceNegated(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayServices(ruleChange, ReportType));
                    sb.Append(ruleChangeDisplayJson.DisplayAction(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayTrack(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayEnabled(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayUid(ruleChange));
                    sb.Append(ruleChangeDisplayJson.DisplayComment(ruleChange));
                    RuleDisplayBase.RemoveLastChars(sb, 1);
                    sb.Append("},");
                    return sb.ToString();
                },
                itemsPropertyName: "rule changes"
            );
        }
    }
}
