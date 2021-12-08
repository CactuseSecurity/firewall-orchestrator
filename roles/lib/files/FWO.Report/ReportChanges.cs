﻿using FWO.Api.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.Report.Filter;
using FWO.ApiClient.Queries;
using FWO.Ui.Display;
using FWO.Config.Api;
using FWO.Logging;

namespace FWO.Report
{
    public class ReportChanges : ReportBase
    {
        public ReportChanges(DynGraphqlQuery query, UserConfig userConfig) : base(query, userConfig) { }

        public override async Task GetObjectsInReport(int objectsPerFetch, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            await callback(Managements);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
        }

        public override Task GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, byte objects, int maxFetchCycles, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            throw new NotImplementedException();
        }


        public override async Task Generate(int changesPerFetch, APIConnection apiConnection, Func<Management[], Task> callback, CancellationToken ct)
        {
            Query.QueryVariables["limit"] = changesPerFetch;
            Query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;
            Managements = Array.Empty<Management>();

            // save selected device state
            Management[] tempDeviceFilter = await apiConnection.SendQueryAsync<Management[]>(DeviceQueries.getDevicesByManagements);
            DeviceFilter.syncFilterLineToLSBFilter(Query.RawFilter, tempDeviceFilter);

            Managements = await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables);
            DeviceFilter.restoreSelectedState(tempDeviceFilter, Managements);
            while (gotNewObjects)
            {
                if (ct.IsCancellationRequested)
                {
                    Log.WriteDebug("Generate Changes Report", "Task cancelled");
                    ct.ThrowIfCancellationRequested();
                }
                Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + changesPerFetch;
                gotNewObjects = Managements.Merge(await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables));
                await callback(Managements);
            }
        }

        public override string ExportToCsv()
        {
            StringBuilder csvBuilder = new StringBuilder();

            foreach (Management management in Managements.Where(mgt => !mgt.Ignore))
            {
                //foreach (var item in collection)
                //{

                //}
            }

            throw new NotImplementedException();
        }

        private const int ColumnCount = 13;

        public override string ExportToHtml()
        {
            StringBuilder report = new StringBuilder();
            RuleChangeDisplay ruleChangeDisplay = new RuleChangeDisplay(userConfig);

            foreach (Management management in Managements.Where(mgt => !mgt.Ignore))
            {
                report.AppendLine($"<h3>{management.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (Device device in management.Devices)
                {
                    report.AppendLine($"<h4>{device.Name}</h4>");
                    report.AppendLine("<hr>");

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

                    if (device.RuleChanges.Length > 0)
                    {
                        foreach (RuleChange ruleChange in device.RuleChanges)
                        {
                            report.AppendLine("<tr>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayChangeTime(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayChangeAction(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayName(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplaySourceZone(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplaySource(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayDestinationZone(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayDestination(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayService(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayAction(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayTrack(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayEnabled(ruleChange, export: true)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayUid(ruleChange)}</td>");
                            report.AppendLine($"<td>{ruleChangeDisplay.DisplayComment(ruleChange)}</td>");
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
                }
            }

            return GenerateHtmlFrame(title: userConfig.GetText("changes_report"), Query.RawFilter, DateTime.Now, report);
        }
    }
}
