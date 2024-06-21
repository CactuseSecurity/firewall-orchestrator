using System.Text;
using FWO.Api.Client;
using FWO.Report.Filter;
using FWO.Ui.Display;
using FWO.Config.Api;
using FWO.Logging;
using System.Text.Json;
using Newtonsoft.Json;

namespace FWO.Report
{
    public class ReportChanges : ReportDevicesBase
    {
        private const int ColumnCount = 13;

        public ReportChanges(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) {}

        public override async Task Generate(int changesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            Query.QueryVariables["limit"] = changesPerFetch;
            Query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            ReportData.ManagementData = await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables);

            while (gotNewObjects)
            {
                if (ct.IsCancellationRequested)
                {
                    Log.WriteDebug("Generate Changes Report", "Task cancelled");
                    ct.ThrowIfCancellationRequested();
                }
                Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + changesPerFetch;
                gotNewObjects = ReportData.ManagementData.Merge(await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables));
                await callback(ReportData);
            }
        }

        public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            await callback(ReportData);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
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
                StringBuilder report = new ();
                RuleChangeDisplayCsv ruleChangeDisplayCsv = new (userConfig);

                report.Append(DisplayReportHeaderCsv());
                report.AppendLine($"\"management-name\",\"device-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"");

                foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                        Array.Exists(mgt.Devices, device => device.RuleChanges != null && device.RuleChanges.Length > 0)))
                {
                    foreach (var gateway in management.Devices)
                    {
                        if (gateway.RuleChanges != null && gateway.RuleChanges.Length > 0)
                        {
                            foreach (var ruleChange in gateway.RuleChanges)
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
            StringBuilder report = new ();
            RuleChangeDisplayHtml ruleChangeDisplayHtml = new (userConfig);

            foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.RuleChanges != null && device.RuleChanges.Length > 0)))
            {
                report.AppendLine($"<h3>{management.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (var device in management.Devices)
                {
                    report.AppendLine($"<h4>{device.Name}</h4>");
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
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
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
            StringBuilder report = new ("{");
            report.Append(DisplayReportHeaderJson());
            report.AppendLine("\"managements\": [");
            RuleChangeDisplayJson ruleChangeDisplayJson = new (userConfig);
            foreach (var management in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.RuleChanges != null && device.RuleChanges.Length > 0)))
            {
                report.AppendLine($"{{\"{management.Name}\": {{");
                report.AppendLine($"\"gateways\": [");
                foreach (var gateway in management.Devices)
                {
                    if (gateway.RuleChanges != null && gateway.RuleChanges.Length > 0)
                    {
                        report.Append($"{{\"{gateway.Name}\": {{\n\"rule changes\": [");
                        foreach (var ruleChange in gateway.RuleChanges)
                        {
                            report.Append('{');
                            report.Append(ruleChangeDisplayJson.DisplayChangeTime(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayChangeAction(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayName(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplaySourceZone(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplaySourceNegated(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplaySource(ruleChange, ReportType));
                            report.Append(ruleChangeDisplayJson.DisplayDestinationZone(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayDestinationNegated(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayDestination(ruleChange, ReportType));
                            report.Append(ruleChangeDisplayJson.DisplayServiceNegated(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayServices(ruleChange, ReportType));
                            report.Append(ruleChangeDisplayJson.DisplayAction(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayTrack(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayEnabled(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayUid(ruleChange));
                            report.Append(ruleChangeDisplayJson.DisplayComment(ruleChange));
                            report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                            report.Append("},");  // EO ruleChange
                        } // rules
                        report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last char (comma)
                        report.Append(']'); // EO rules
                        report.Append('}'); // EO gateway internal
                        report.Append("},"); // EO gateway external
                    }
                } // gateways
                report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last char (comma)
                report.Append(']'); // EO gateways
                report.Append('}'); // EO management internal
                report.Append("},"); // EO management external
            } // managements
            report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last char (comma)
            report.Append(']'); // EO managements
            report.Append('}'); // EO top

            dynamic? json = JsonConvert.DeserializeObject(report.ToString());
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented
            };
            return JsonConvert.SerializeObject(json, settings);            
        }
    }
}
