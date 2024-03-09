using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text;
using FWO.Logging;

namespace FWO.Report
{
    public class ReportConnections : ReportOwnersBase
    {
        public ReportConnections(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        public override async Task GenerateCon(int connectionsPerFetch, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback, CancellationToken ct)
        {
            List<ModellingConnection> conns = new();
            // Query.QueryVariables["limit"] = connectionsPerFetch;
            // Query.QueryVariables["offset"] = 0;
            // bool gotNewObjects = true;

            conns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);

            // while (gotNewObjects)
            // {
            //     if (ct.IsCancellationRequested)
            //     {
            //         Log.WriteDebug("Generate Connections Report", "Task cancelled");
            //         ct.ThrowIfCancellationRequested();
            //     }
            //     Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + connectionsPerFetch;
            //     List<ModellingConnection> newConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);
            //     gotNewObjects = newConnections.Count > 0;
            //     ReportData.OwnerData.Connections.AddRange(newConnections);

            await callback(conns);

            // }
            //ReportData.OwnerData.Add(new(){ Connections = conns });
        }

        public override async Task<bool> GetConObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<List<ModellingConnection>, Task> callback)
        {
            await callback (ReportData.OwnerData[0].Connections);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new ();
            foreach (var ownerReport in ReportData.OwnerData)
            {
                ownerReport.AssignConnectionNumbers();


                report.AppendLine($"<h3>{ownerReport.Name}</h3>");
                report.AppendLine("<hr>");
                report.AppendLine("<table>");
                AppendConnectionHeadlineHtml(ref report);
                foreach (var connection in ownerReport.Connections)
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{connection.OrderNumber}</td>");
                    report.AppendLine($"<td>{connection.Id}</td>");
                    report.AppendLine($"<td>{connection.Name}</td>");
                    report.AppendLine($"<td>{connection.Reason}</td>");


                }

                // show all objects used in this management's rules
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendConnectionHeadlineHtml(ref StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("func_reason")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
            report.AppendLine("</tr>");
        }

        public override string SetDescription()
        {
            int counter = 0;
            foreach(var owner in ReportData.OwnerData)
            {
                counter += owner.Connections.Count;
            }
            return $"{counter} {userConfig.GetText("connections")}";
        }
    }
}
