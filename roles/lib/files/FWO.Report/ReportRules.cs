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

namespace FWO.Report
{
    public class ReportRules : ReportBase
    {
        public override async Task Generate(int rulesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput);
            query.QueryVariables["limit"] = rulesPerFetch;
            query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            // get the filter line
            string TimeFilter = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            if (query.ReportTime != "")
                TimeFilter = query.ReportTime;

            // get relevant import ids for report time
            ImpIdQueryVariables["time"] = TimeFilter;
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);

            Managements = new Management[managementsWithRelevantImportId.Length];
            int i;

            for (i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                    query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                else    // managment was not yet imported at that time
                    query.QueryVariables["relevantImportId"] = -1;
                Managements[i] = (await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables))[0];
            }
            while (gotNewObjects)
            {
                query.QueryVariables["offset"] = (int)query.QueryVariables["offset"] + rulesPerFetch;
                for (i = 0; i < managementsWithRelevantImportId.Length; i++)
                {
                    if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                        query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                    else
                        query.QueryVariables["relevantImportId"] = -1; // managment was not yet imported at that time
                    query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                    gotNewObjects = Managements[i].Merge((await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables))[0]);
                }
                await callback(Managements);
            }
        }

        public override string ToCsv()
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

        public override string ToHtml()
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
                    report.AppendLine("<th>Source</th>");
                    report.AppendLine("<th>Destination</th>");
                    report.AppendLine("<th>Services</th>");
                    report.AppendLine("<th>Action</th>");
                    report.AppendLine("<th>Track</th>");
                    report.AppendLine("<th>Disabled</th>");
                    report.AppendLine("<th>UID</th>");
                    report.AppendLine("<th>Comment</th>");
                    report.AppendLine("</tr>");

                    foreach (Rule rule in device.Rules)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{rule.DisplayNumber(device.Rules)}</td>");
                        report.AppendLine($"<td>{rule.DisplayName()}</td>");
                        report.AppendLine($"<td>{rule.DisplaySource()}</td>");
                        report.AppendLine($"<td>{rule.DisplayDestination()}</td>");
                        report.AppendLine($"<td>{rule.DisplayService()}</td>");
                        report.AppendLine($"<td>{rule.DisplayAction()}</td>");
                        report.AppendLine($"<td>{rule.DisplayTrack()}</td>");
                        report.AppendLine($"<td>{rule.DisplayDisabled()}</td>");
                        report.AppendLine($"<td>{rule.DisplayUid()}</td>");
                        report.AppendLine($"<td>{rule.DisplayComment()}</td>");
                        report.AppendLine("</tr>");
                    }

                    report.AppendLine("</table>");
                }
            }

            return HtmlTemplate.Replace("##Body##", report.ToString());
        }
    }
}
