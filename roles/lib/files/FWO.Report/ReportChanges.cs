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
using FWO.Ui.Display;

namespace FWO.Report
{
    public class ReportChanges : ReportBase
    {
        public override async Task Generate(int changesPerFetch, string filterInput, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            DynGraphqlQuery query = Compiler.Compile(filterInput);
            query.QueryVariables["limit"] = changesPerFetch;
            query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;
            Managements = Array.Empty<Management>();

            Managements = await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables);
            while (gotNewObjects)
            {
                query.QueryVariables["offset"] = (int)query.QueryVariables["offset"] + changesPerFetch;
                gotNewObjects = Managements.Merge(await apiConnection.SendQueryAsync<Management[]>(query.FullQuery, query.QueryVariables));
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
                    report.AppendLine("<th>Change Time</th>");
                    report.AppendLine("<th>Change Type</th>");
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

                    foreach (RuleChange ruleChange in device.RuleChanges)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{ruleChange.DisplayChangeTime()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayChangeAction()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayName()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplaySourceZone()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplaySource()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayDestinationZone()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayDestination()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayService()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayAction()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayTrack()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayEnabled()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayUid()}</td>");
                        report.AppendLine($"<td>{ruleChange.DisplayComment()}</td>");
                        report.AppendLine("</tr>");
                    }

                    report.AppendLine("</table>");
                }
            }

            return HtmlTemplate.Replace("##Body##", report.ToString());
        }
    }
}
