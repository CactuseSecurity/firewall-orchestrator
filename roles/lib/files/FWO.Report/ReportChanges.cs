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
using Org.BouncyCastle.Crypto;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace FWO.Report
{
    public class ReportChanges : ReportDevicesBase
    {
        private const int ColumnCount = 13;

        private readonly TimeFilter timeFilter;
        private readonly bool IncludeObjectsInReportChanges; 

        public ReportChanges(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, TimeFilter timeFilter, bool includeObjectsInReportChanges, bool IncludeObjectsInReportChangesUiPresesed) : base(query, userConfig, reportType)
        {
            this.timeFilter = timeFilter;

            if (IncludeObjectsInReportChangesUiPresesed)
            {

                this.IncludeObjectsInReportChanges = includeObjectsInReportChanges;
            }
            else
            {
                this.IncludeObjectsInReportChanges = userConfig.ImpChangeIncludeObjectChanges;
            }
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
                List<long> importIdsInRange = [.. managementsWithImportIds.Where(m => m.Id == management.Id).SelectMany(m => m.ImportControls).Select(ic => ic.ControlId).DefaultIfEmpty(0)];
                List<long> relevantImportIds = [.. importIdLastBeforeRange, .. importIdsInRange];

                SetMgtQueryVars(management.Id, relevantImportIds[0], relevantImportIds[1], IncludeObjectsInReportChanges);
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
                    SetMgtQueryVars(management.Id, importIdOld, importIdNew, IncludeObjectsInReportChanges);

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

        private void SetMgtQueryVars(int mgmId, long importIdOld, long importIdNew, bool includeObjectsInChangesReport)
        {
            Query.QueryVariables[QueryVar.MgmId] = mgmId;
            Query.QueryVariables[QueryVar.ImportIdOld] = importIdOld;
            Query.QueryVariables[QueryVar.ImportIdNew] = importIdNew;
            Query.QueryVariables[QueryVar.IncludeObjectsInChangesReport] = includeObjectsInChangesReport;
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
                report.AppendLine("\"Rules\"");
                report.AppendLine($"\"management-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"enforcing_device\",\"rule-uid\",\"rule-comment\""); 

                foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
                {
                    if (management.RuleChanges != null && management.RuleChanges?.Any() == true)
                    {
                        foreach (var ruleChange in management.RuleChanges)
                        {
                            report.Append(ruleChangeDisplayCsv.OutputCsv(management.Name));
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
                            report.Append(ruleChangeDisplayCsv.DisplayenforcingDevice(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayUid(ruleChange));
                            report.Append(ruleChangeDisplayCsv.DisplayComment(ruleChange));
                            report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                            report.AppendLine("");
                        }
                    }

                    if (IncludeObjectsInReportChanges)
                    {
                        report.AppendLine($"#");
                        report.AppendLine("\"Network objects\"");
                        report.AppendLine($"\"management-name\",\"change-time\",\"change-type\",\"object-name\",\"type\",\"ip_address\",\"members\",\"object-uid\",\"object-comment\"");

                        if (management.ObjectChanges != null && management.ObjectChanges?.Any() == true)
                        {
                            foreach (var objectChange in management.ObjectChanges)
                            {
                                report.Append(ruleChangeDisplayCsv.OutputCsv(management.Name));
                                report.Append(ruleChangeDisplayCsv.DisplayChangeTime(objectChange));
                                report.Append(ruleChangeDisplayCsv.DisplayChangeAction(objectChange));
                                report.Append(ruleChangeDisplayCsv.DisplayName(objectChange));
                                report.Append(ruleChangeDisplayCsv.DisplayObjectType(objectChange));
                                report.Append(ruleChangeDisplayCsv.DisplayObjectIp(objectChange));
                                report.Append(ruleChangeDisplayCsv.DisplayObjectMemberNames(objectChange));
                                report.Append(ruleChangeDisplayCsv.DisplayUid(objectChange));
                                report.Append(ruleChangeDisplayCsv.DisplayComment(objectChange));
                                report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                                report.AppendLine("");
                            }
                        }

                        #region Serviceobjects
                        report.AppendLine($"#");
                        report.AppendLine("\"Service objects\"");
                        report.AppendLine($"\"management-name\",\"change-time\",\"change-type\",\"service-name\",\"type\",\"protocol\",\"port\",\"members\",\"service-uid\",\"service-comment\"");

                        if (management.ServiceChanges != null && management.ServiceChanges?.Any() == true)
                        {
                            foreach (var serviceChange in management.ServiceChanges)
                            {
                                report.Append(ruleChangeDisplayCsv.OutputCsv(management.Name));
                                report.Append(ruleChangeDisplayCsv.DisplayChangeTime(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayChangeAction(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayName(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayServiceType(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayServiceProtocol(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayServicePort(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayServiceMemberNames(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayUid(serviceChange));
                                report.Append(ruleChangeDisplayCsv.DisplayComment(serviceChange));
                                report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                                report.AppendLine("");
                            }
                        }
                        #endregion

                        #region Userobjects
                        if (management.UserChanges != null && management.UserChanges?.Any() == true)
                        {
                            report.AppendLine($"#");
                            report.AppendLine("\"User objects\"");
                            report.AppendLine($"\"management-name\",\"change-time\",\"change-type\",\"user-name\",\"user-comment\"");


                            foreach (var userChange in management.UserChanges)
                            {
                                report.Append(ruleChangeDisplayCsv.OutputCsv(management.Name));
                                report.Append(ruleChangeDisplayCsv.DisplayChangeTime(userChange));
                                report.Append(ruleChangeDisplayCsv.DisplayChangeAction(userChange));
                                report.Append(ruleChangeDisplayCsv.DisplayName(userChange));
                                report.Append(ruleChangeDisplayCsv.DisplayComment(userChange));
                                report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                                report.AppendLine("");
                            }
                        }
                        #endregion
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

            foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{management.Name}</h3>");
                report.AppendLine("<hr>");
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">Rules</h4>");

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
                report.AppendLine($"<th>{userConfig.GetText("enforcing_devices")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                report.AppendLine("</tr>");

                if (management.RuleChanges != null && management.RuleChanges?.Any() == true)
                {
                    foreach (var ruleChange in management.RuleChanges)
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
                        report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayEnforcingGateways(ruleChange, OutputLocation.export, ReportType)}</td>");
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

                if (IncludeObjectsInReportChanges)
                {
                    #region Networkobjects

                    report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">Network objects</h4>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("change_time")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("change_type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("ip_address")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                    report.AppendLine("</tr>");

                    if (management.ObjectChanges != null && management.ObjectChanges?.Any() == true)
                    {
                        foreach (var objectChange in management.ObjectChanges)
                        {
                            report.AppendLine("<tr>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeTime(objectChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeAction(objectChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayName(objectChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayObjectType(objectChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayObjectIP(objectChange)}</td>");
                            report.AppendLine($"{ruleChangeDisplayHtml.DisplayObjectMemberNames(objectChange)}");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayUid(objectChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayComment(objectChange)}</td>");
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

                    #endregion

                    #region Serviceobjects

                    report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">Service objects</h4>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("change_time")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("change_type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("protocol")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("port")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                    report.AppendLine("</tr>");

                    if (management.ServiceChanges != null && management.ServiceChanges?.Any() == true)
                    {
                        foreach (var serviceChange in management.ServiceChanges)
                        {
                            report.AppendLine("<tr>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeTime(serviceChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeAction(serviceChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayName(serviceChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayServiceType(serviceChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayServiceProtocol(serviceChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayServicePort(serviceChange)}</td>");
                            report.AppendLine($"{ruleChangeDisplayHtml.DisplayServiceMemberNames(serviceChange)}");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayUid(serviceChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayComment(serviceChange)}</td>");
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

                    #endregion

                    #region Userobjects
                    if (management.UserChanges != null && management.UserChanges?.Any() == true)
                    {
                        report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">User objects</h4>");
                        report.AppendLine("<table>");
                        report.AppendLine("<tr>");
                        report.AppendLine($"<th>{userConfig.GetText("change_time")}</th>");
                        report.AppendLine($"<th>{userConfig.GetText("change_type")}</th>");
                        report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                        report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                        report.AppendLine("</tr>");

                        if (management.UserChanges != null && management.UserChanges?.Any() == true)
                        {
                            foreach (var userChange in management.UserChanges)
                            {
                                report.AppendLine("<tr>");
                                report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeTime(userChange)}</td>");
                                report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayChangeAction(userChange)}</td>");
                                report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayName(userChange)}</td>");
                                report.AppendLine($"<td>{ruleChangeDisplayHtml.DisplayComment(userChange)}</td>");
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
                    #endregion
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
            StringBuilder report = new("{");
            report.Append(DisplayReportHeaderJson());
            report.AppendLine("\"managements\": [");


            foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                report.AppendLine($"{{\"{management.Name}\":");

                report.Append($"{{\n\"rule changes\": [");
                if (management.RuleChanges != null && management.RuleChanges?.Any() == true)
                {
                    var items = management.RuleChanges!.ToList();

                    foreach (var ruleChange in items)
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
                        sb.Append(ruleChangeDisplayJson.DisplayEnforcingGateways(ruleChange));
                        sb.Append(ruleChangeDisplayJson.DisplayUid(ruleChange));
                        sb.Append(ruleChangeDisplayJson.DisplayComment(ruleChange));
                        RuleDisplayBase.RemoveLastChars(sb, 1); // letztes Komma entfernen
                        sb.Append("},");
                        report.Append(sb.ToString());
                    }
                    RuleDisplayBase.RemoveLastChars(report, 1); // letztes Komma bei Items entfernen

                }
                report.Append("],");


                if (IncludeObjectsInReportChanges)
                {
                    if (management.ObjectChanges != null && management.ObjectChanges?.Any() == true)
                    {
                        report.Append($"\n\"Network Object changes\": [");

                        var nwos = management.ObjectChanges!.ToList();
                        if (nwos.Any())
                        {
                            foreach (var objectChange in nwos)
                            {
                                var sb = new StringBuilder("{");
                                sb.Append(ruleChangeDisplayJson.DisplayChangeTime(objectChange));
                                sb.Append(ruleChangeDisplayJson.DisplayChangeAction(objectChange));
                                sb.Append(ruleChangeDisplayJson.DisplayName(objectChange));
                                sb.Append(ruleChangeDisplayJson.DisplayObjectType(objectChange));
                                sb.Append(ruleChangeDisplayJson.DisplayObjectIP(objectChange));
                                sb.Append(ruleChangeDisplayJson.DisplayObjectMemberNames(objectChange));
                                sb.Append(ruleChangeDisplayJson.DisplayUid(objectChange));
                                sb.Append(ruleChangeDisplayJson.DisplayComment(objectChange));
                                RuleDisplayBase.RemoveLastChars(sb, 1); // letztes Komma entfernen
                                sb.Append("},");
                                report.Append(sb.ToString());
                            }
                            RuleDisplayBase.RemoveLastChars(report, 1); // letztes Komma bei Items entfernen
                        }
                        report.Append("],");
                    }

                    if (management.ServiceChanges != null && management.ServiceChanges?.Any() == true)
                    {
                        report.Append($"\n\"Service Object changes\": [");

                        var svcs = management.ServiceChanges!.ToList();
                        if (svcs.Any())
                        {
                            foreach (var serviceChange in svcs)
                            {
                                var sb = new StringBuilder("{");
                                sb.Append(ruleChangeDisplayJson.DisplayChangeTime(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayChangeAction(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayName(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayObjectType(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayServiceProtocol(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayServicePort(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayObjectMemberNames(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayUid(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayComment(serviceChange));
                                RuleDisplayBase.RemoveLastChars(sb, 1); // letztes Komma entfernen
                                sb.Append("},");
                                report.Append(sb.ToString());
                            }
                            RuleDisplayBase.RemoveLastChars(report, 1); // letztes Komma bei Items entfernen
                        }
                        report.Append("]");
                    }


                    if (management.UserChanges != null && management.UserChanges?.Any() == true)
                    {
                        report.Append($"\n\"User Object changes\": [");

                        var userc = management.UserChanges!.ToList();
                        if (userc.Any())
                        {
                            foreach (var serviceChange in userc)
                            {
                                var sb = new StringBuilder("{");
                                sb.Append(ruleChangeDisplayJson.DisplayChangeTime(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayChangeAction(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayName(serviceChange));
                                sb.Append(ruleChangeDisplayJson.DisplayComment(serviceChange));
                                RuleDisplayBase.RemoveLastChars(sb, 1); // letztes Komma entfernen
                                sb.Append("},");
                                report.Append(sb.ToString());
                            }
                            RuleDisplayBase.RemoveLastChars(report, 1); // letztes Komma bei Items entfernen
                        }
                        report.Append("],");
                    }
                }
                report.Append("}},");
            }

            RuleDisplayBase.RemoveLastChars(report, 1); // letztes Komma bei Managements entfernen
            report.Append("]}");

            dynamic? json = JsonConvert.DeserializeObject(report.ToString());
            return JsonConvert.SerializeObject(json, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            });
        }
    }
}
