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

        public override async Task Generate(int connectionsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            // Query.QueryVariables["limit"] = connectionsPerFetch;
            // Query.QueryVariables["offset"] = 0;
            // bool gotNewObjects = true;

            List<ModellingConnection> conns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);

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

            ReportData reportData = new() { OwnerData = new() { new(){ Connections = conns } } };
            await callback(reportData);

            // }
            //ReportData.OwnerData.Add(new(){ Connections = conns });
        }

        public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            await callback (ReportData);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new ();
            foreach (var ownerReport in ReportData.OwnerData)
            {
                ownerReport.PrepareObjectData();
                report.AppendLine($"<h3>{ownerReport.Name}</h3>");
                if(ownerReport.RegularConnections.Count > 0)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("connections")}</h4>");
                    AppendConnectionsGroupHtml(ownerReport.RegularConnections, ownerReport, ref report);
                }
                if(ownerReport.Interfaces.Count > 0)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("interfaces")}</h4>");
                    AppendConnectionsGroupHtml(ownerReport.Interfaces, ownerReport, ref report);
                }
                if(ownerReport.CommonServices.Count > 0)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("common_services")}</h4>");
                    AppendConnectionsGroupHtml(ownerReport.CommonServices, ownerReport, ref report);
                }

                AppendNetworkObjectsHtml(ownerReport.AllObjects, ref report);
                AppendNetworkServicesHtml(ownerReport.AllServices, ref report);
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendConnectionsGroupHtml(List<ModellingConnection> connections, OwnerReport ownerReport, ref StringBuilder report)
        {
            OwnerReport.AssignConnectionNumbers(connections);
            report.AppendLine("<table>");
            AppendConnectionHeadlineHtml(ref report);
            foreach (var connection in connections)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{connection.OrderNumber}</td>");
                report.AppendLine($"<td>{connection.Id}</td>");
                report.AppendLine($"<td>{connection.Name}</td>");
                report.AppendLine($"<td>{connection.Reason}</td>");
                report.AppendLine($"<td>{String.Join("<br>", ownerReport.GetSrcNames(connection))}</td>");
                report.AppendLine($"<td>{String.Join("<br>", ownerReport.GetSvcNames(connection))}</td>");
                report.AppendLine($"<td>{String.Join("<br>", ownerReport.GetDstNames(connection))}</td>");
            }
            report.AppendLine("</table>");
            report.AppendLine("<hr>");
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

        private void AppendNetworkObjectsHtml(List<NetworkObject> networkObjects, ref StringBuilder report)
        {
            report.AppendLine($"<h4>{userConfig.GetText("network_objects")}</h4>");
            report.AppendLine("<table>");
            AppendNWObjHeadlineHtml(ref report);
            foreach (var nwObj in networkObjects)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{nwObj.Number}</td>");
                report.AppendLine($"<td>{nwObj.Id}</td>");
                report.AppendLine($"<td>{nwObj.Name}</td>");
                report.AppendLine($"<td>{nwObj.IP}</td>");
            }
            report.AppendLine("</table>");
            report.AppendLine("<hr>");
        }

        private void AppendNWObjHeadlineHtml(ref StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("ip")}</th>");
            report.AppendLine("</tr>");
        }

        private void AppendNetworkServicesHtml(List<NetworkService> networkServices, ref StringBuilder report)
        {
            report.AppendLine($"<h4>{userConfig.GetText("network_services")}</h4>");
            report.AppendLine("<table>");
            AppendNWSvcHeadlineHtml(ref report);
            foreach (var svc in networkServices)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{svc.Number}</td>");
                report.AppendLine($"<td>{svc.Id}</td>");
                report.AppendLine($"<td>{svc.Name}</td>");
                report.AppendLine($"<td>{svc.Protocol.Name}</td>");
                report.AppendLine($"<td>{svc.DestinationPort}</td>");
            }
            report.AppendLine("</table>");
            report.AppendLine("<hr>");
        }

        private void AppendNWSvcHeadlineHtml(ref StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("protocol")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("port")}</th>");
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
