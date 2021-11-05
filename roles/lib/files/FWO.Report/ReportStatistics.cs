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

namespace FWO.Report
{
    public class ReportStatistics : ReportBase
    {
        // TODO: Currently generated in Report.razor as well as here, because of export. Remove dupliacte.
        private Management globalStatisticsManagament = new Management();

        public ReportStatistics(DynGraphqlQuery query) : base(query) { }

        public override async Task GetObjectsInReport(int objectsPerFetch, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            await callback(Managements);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
        }

        public override Task GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, byte objects, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            return Task.CompletedTask;
        }

        public override async Task Generate(int _, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            string TimeFilter = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            if (Query.ReportTime != null && Query.ReportTime != "" && Query.ReportTime != "now")
                TimeFilter = Query.ReportTime;

            // get relevant import ids for report time
            ImpIdQueryVariables["time"] = TimeFilter;
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);

            // save selected device state
            Management[] tempDeviceFilter = await apiConnection.SendQueryAsync<Management[]>(DeviceQueries.getDevicesByManagements);
            DeviceFilter.syncFilterLineToLSBFilter(Query.RawFilter, tempDeviceFilter);

            List<Management> resultList = new List<Management>();
            int i;

            for (i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                Query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                    Query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId;
                else    // managment was not yet imported at that time
                    Query.QueryVariables["relevantImportId"] = -1;
                resultList.Add((await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables))[0]);
            }
            Managements = resultList.ToArray();
            await callback(Managements);

            foreach (Management mgm in Managements.Where(mgt => !mgt.Ignore))
            {
                globalStatisticsManagament.RuleStatistics.ObjectAggregate.ObjectCount += mgm.RuleStatistics.ObjectAggregate.ObjectCount;
                globalStatisticsManagament.NetworkObjectStatistics.ObjectAggregate.ObjectCount += mgm.NetworkObjectStatistics.ObjectAggregate.ObjectCount;
                globalStatisticsManagament.ServiceObjectStatistics.ObjectAggregate.ObjectCount += mgm.ServiceObjectStatistics.ObjectAggregate.ObjectCount;
                globalStatisticsManagament.UserObjectStatistics.ObjectAggregate.ObjectCount += mgm.UserObjectStatistics.ObjectAggregate.ObjectCount;
            }
            DeviceFilter.restoreSelectedState(tempDeviceFilter, Managements);
        }

        public override string ExportToJson()
        {
            globalStatisticsManagament.Name = "global statistics";
            Management[] combinedManagements = (new Management[] { globalStatisticsManagament }).Concat(Managements.Where(mgt => !mgt.Ignore)).ToArray();
            return JsonSerializer.Serialize(combinedManagements, new JsonSerializerOptions { WriteIndented = true });
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

        public override string ExportToHtml()
        {
            StringBuilder report = new StringBuilder();

            report.AppendLine($"<h3>Global number of Objects</h3>");
            report.AppendLine("<table>");
            report.AppendLine("<tr>");
            report.AppendLine("<th>Network objects</th>");
            report.AppendLine("<th>Service objects</th>");
            report.AppendLine("<th>User objects</th>");
            report.AppendLine("<th>Rules</th>");
            report.AppendLine("</tr>");
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{globalStatisticsManagament.NetworkObjectStatistics.ObjectAggregate.ObjectCount}</td>");
            report.AppendLine($"<td>{globalStatisticsManagament.ServiceObjectStatistics.ObjectAggregate.ObjectCount}</td>");
            report.AppendLine($"<td>{globalStatisticsManagament.UserObjectStatistics.ObjectAggregate.ObjectCount}</td>");
            report.AppendLine($"<td>{globalStatisticsManagament.RuleStatistics.ObjectAggregate.ObjectCount }</td>");
            report.AppendLine("</tr>");
            report.AppendLine("</table>");
            report.AppendLine("<hr>");

            foreach (Management management in Managements.Where(mgt => !mgt.Ignore))
            {
                report.AppendLine($"<h4>Number of Objects - {management.Name}</h4>");
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine("<th>Network objects</th>");
                report.AppendLine("<th>Service objects</th>");
                report.AppendLine("<th>User objects</th>");
                report.AppendLine("<th>Rules</th>");
                report.AppendLine("</tr>");
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{management.NetworkObjectStatistics.ObjectAggregate.ObjectCount}</td>");
                report.AppendLine($"<td>{management.ServiceObjectStatistics.ObjectAggregate.ObjectCount}</td>");
                report.AppendLine($"<td>{management.UserObjectStatistics.ObjectAggregate.ObjectCount}</td>");
                report.AppendLine($"<td>{management.RuleStatistics.ObjectAggregate.ObjectCount }</td>");
                report.AppendLine("</tr>");
                report.AppendLine("</table>");
                report.AppendLine("<br>");

                report.AppendLine($"<h4>Number of Rules per Gateway</h4>");
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine("<th>Gateway</th>");
                report.AppendLine("<th>Rules</th>");
                report.AppendLine("</tr>");
                foreach (Device device in management.Devices)
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{device.Name}</td>");
                    report.AppendLine($"<td>{device.RuleStatistics.ObjectAggregate.ObjectCount}</td>");
                    report.AppendLine("</tr>");
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }

            return GenerateHtmlFrame(title: "Statistic Report", Query.RawFilter, DateTime.Now, report);
        }
    }
}
