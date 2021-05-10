using FWO.Api.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.Report.Filter;
using FWO.ApiClient.Queries;
using System.Text.Json;
using PdfSharpCore.Pdf;
using FWO.Ui.Display;
using VetCV.HtmlRendererCore.PdfSharpCore;
using PdfSharpCore;
using System.Text.Json.Serialization;

namespace FWO.Report
{
    public class ReportRules : ReportBase
    {
        public ReportRules(DynGraphqlQuery query) : base(query) { }

        public override async Task GetObjectsInReport(int objectsPerFetch, APIConnection apiConnection, Func<Management[], Task> callback) // to be called when exporting
        {
            List<int> relevantDevIds = DeviceFilter.ExtractSelectedDevIds(Managements);
            Management filteredObjectsPerManagement;

            for (int i = 0; i < Managements.Length; i++)
            {
                // get rule ids per import (= management)
                Dictionary<string, object> ruleQueryVariables = new Dictionary<string, object>();
                if (Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                {
                    ruleQueryVariables["importId"] = Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                    ruleQueryVariables["devIds"] = relevantDevIds;
                    Rule[] rules = await apiConnection.SendQueryAsync<Rule[]>(RuleQueries.getRuleIdsOfImport, ruleQueryVariables);
                    Managements[i].ReportedRuleIds = rules.Select(x => Convert.ToInt64(x.Id)).Distinct().ToList();

                    // set query variables for object query
                    Dictionary<string, object> objQueryVariables = new Dictionary<string, object>
                    {
                        {"ruleIds", Managements[i].ReportedRuleIds },
                        {"mgmIds", Managements[i].Id }
                    };

                    // get objects for this management in the current report
                    filteredObjectsPerManagement = (await apiConnection.SendQueryAsync<Management[]>(ObjectQueries.getReportFilteredObjectDetails, objQueryVariables))[0];
                    Managements[i].ReportObjects = filteredObjectsPerManagement.ReportObjects;
                    Managements[i].ReportServices = filteredObjectsPerManagement.ReportServices;
                    Managements[i].ReportUsers = filteredObjectsPerManagement.ReportUsers;
                }
            }
            await callback(Managements);
        }

        public override async Task Generate(int rulesPerFetch, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            Query.QueryVariables["limit"] = rulesPerFetch;
            Query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            // get the filter line
            string TimeFilter = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            if (Query.ReportTime != "")
                TimeFilter = Query.ReportTime;

            // get relevant import ids for report time
            ImpIdQueryVariables["time"] = TimeFilter;
            // todo: only get relevant importIds for devices in the filter
            //    need to convert string gateway filter (gateway="checkPoint_demo") into list of mgmIds
            //    ImpIdQueryVariables["mgmIds"] = mgmIds;
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);

            // save selected device state
            Management[] tempDeviceFilter = await apiConnection.SendQueryAsync<Management[]>(DeviceQueries.getDevicesByManagements);
            DeviceFilter.syncFilterLineToLSBFilter(Query.RawFilter, tempDeviceFilter, false);

            Managements = new Management[managementsWithRelevantImportId.Length];
            int i;
            for (i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                Query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                    Query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                else    // managment was not yet imported at that time
                    Query.QueryVariables["relevantImportId"] = -1;
                Managements[i] = (await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables))[0];
                Managements[i].Import = managementsWithRelevantImportId[i].Import;
            }
            while (gotNewObjects)
            {
                gotNewObjects = false;
                Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + rulesPerFetch;
                for (i = 0; i < managementsWithRelevantImportId.Length; i++)
                {
                    if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                        Query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                    else
                        Query.QueryVariables["relevantImportId"] = -1; // managment was not yet imported at that time
                    Query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                    gotNewObjects = gotNewObjects | Managements[i].Merge((await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables))[0]);
                }
                await callback(Managements);
            }
            DeviceFilter.restoreSelectedState(tempDeviceFilter, Managements);
        }

        public override string ExportToCsv()
        {
            StringBuilder csvBuilder = new StringBuilder();

            foreach (Management management in Managements)
            {
                //foreach (var item in collection)
                //{

                //}
            }

            throw new NotImplementedException();
        }

        //public override string ToJson()
        //{
        //    return JsonSerializer.Serialize(Managements, new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = ReferenceHandler.Preserve });
        //}

        private const int ColumnCount = 12;

        public override string ExportToHtml()
        {
            StringBuilder report = new StringBuilder();

            foreach (Management management in Managements)
            {
                report.AppendLine($"<h3>{management.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (Device device in management.Devices)
                {
                    if (device.Rules.Length > 0)
                    {
                        report.AppendLine($"<h4>{device.Name}</h4>");
                        report.AppendLine("<hr>");

                        report.AppendLine("<table>");
                        report.AppendLine("<tr>");
                        report.AppendLine("<th>Number</th>");
                        report.AppendLine("<th>Name</th>");
                        report.AppendLine("<th>Source Zone</th>");
                        report.AppendLine("<th>Source</th>");
                        report.AppendLine("<th>Destination Zone</th>");
                        report.AppendLine("<th>Destination</th>");
                        report.AppendLine("<th>Services</th>");
                        report.AppendLine("<th>Action</th>");
                        report.AppendLine("<th>Track</th>");
                        report.AppendLine("<th>Enabled</th>");
                        report.AppendLine("<th>UID</th>");
                        report.AppendLine("<th>Comment</th>");
                        report.AppendLine("</tr>");

                        foreach (Rule rule in device.Rules)
                        {
                            if (string.IsNullOrEmpty(rule.SectionHeader))
                            {
                                report.AppendLine("<tr>");
                                report.AppendLine($"<td>{rule.DisplayNumber(device.Rules)}</td>");
                                report.AppendLine($"<td>{rule.DisplayName()}</td>");
                                report.AppendLine($"<td>{rule.DisplaySourceZone()}</td>");
                                report.AppendLine($"<td>{rule.DisplaySource()}</td>");
                                report.AppendLine($"<td>{rule.DisplayDestinationZone()}</td>");
                                report.AppendLine($"<td>{rule.DisplayDestination()}</td>");
                                report.AppendLine($"<td>{rule.DisplayService()}</td>");
                                report.AppendLine($"<td>{rule.DisplayAction()}</td>");
                                report.AppendLine($"<td>{rule.DisplayTrack()}</td>");
                                report.AppendLine($"<td>{rule.DisplayEnabled(export: true)}</td>");
                                report.AppendLine($"<td>{rule.DisplayUid()}</td>");
                                report.AppendLine($"<td>{rule.DisplayComment()}</td>");
                                report.AppendLine("</tr>");
                            }
                            else
                            {
                                report.AppendLine("<tr>");
                                report.AppendLine($"<td style=\"background-color: #f0f0f0;\" colspan=\"{ColumnCount}\">{rule.SectionHeader}</td>");
                                report.AppendLine("</tr>");
                            }
                        }

                        report.AppendLine("</table>");
                    }
                }

                // show all objects used in this management's rules

                int objNumber = 1;
                if (management.ReportObjects != null)
                {
                    report.AppendLine($"<h4>Network Objects</h4>");
                    report.AppendLine("<hr>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine("<th>Number</th>");
                    report.AppendLine("<th>Name</th>");
                    report.AppendLine("<th>UID</th>");
                    report.AppendLine("<th>Comment</th>");
                    report.AppendLine("</tr>");
                    foreach (NetworkObjectWrapper nwobj in management.ReportObjects)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{nwobj.Content.Name}</td>");
                        report.AppendLine($"<td>{nwobj.Content.Uid}</td>");
                        report.AppendLine($"<td>{nwobj.Content.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                if (management.ReportServices != null)
                {
                    report.AppendLine($"<h4>Network Services</h4>");
                    report.AppendLine("<hr>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine("<th>Number</th>");
                    report.AppendLine("<th>Name</th>");
                    report.AppendLine("<th>UID</th>");
                    report.AppendLine("<th>Comment</th>");
                    report.AppendLine("</tr>");
                    objNumber = 1;
                    foreach (ServiceWrapper svcobj in management.ReportServices)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{svcobj.Content.Name}</td>");
                        report.AppendLine($"<td>{svcobj.Content.Uid}</td>");
                        report.AppendLine($"<td>{svcobj.Content.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                if (management.ReportUsers != null)
                {
                    report.AppendLine($"<h4>Users</h4>");
                    report.AppendLine("<hr>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine("<th>Number</th>");
                    report.AppendLine("<th>Name</th>");
                    report.AppendLine("<th>UID</th>");
                    report.AppendLine("<th>Comment</th>");
                    report.AppendLine("</tr>");
                    objNumber = 1;
                    foreach (UserWrapper userobj in management.ReportUsers)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{userobj.Content.Name}</td>");
                        report.AppendLine($"<td>{userobj.Content.Uid}</td>");
                        report.AppendLine($"<td>{userobj.Content.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                report.AppendLine("</table>");
            }

            return GenerateHtmlFrame(title: "Rules Report", Query.RawFilter, DateTime.Now, report);
        }
    }
}
