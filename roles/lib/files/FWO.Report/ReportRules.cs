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
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);

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

            return GenerateHtmlFrame(title:"Rules Report", Query.RawFilter, DateTime.Now, report);
        }
    }
}
