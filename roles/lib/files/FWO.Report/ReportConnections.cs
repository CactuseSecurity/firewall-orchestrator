using FWO.Basics;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text;
using FWO.Services;

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

            ReportData reportData = new() { OwnerData = [new(){ Connections = conns }] };
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
            int chapterNumber = 0;
            foreach (var ownerReport in ReportData.OwnerData)
            {
                chapterNumber++;
                ownerReport.PrepareObjectData();
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{ownerReport.Name}</h3>");
                if(ownerReport.RegularConnections.Count > 0)
                {
                    report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("connections")}</h4>");
                    AppendConnectionsGroupHtml(ownerReport.RegularConnections, ownerReport, chapterNumber, ref report);
                }
                if(ownerReport.Interfaces.Count > 0)
                {
                    report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("interfaces")}</h4>");
                    ownerReport.Interfaces.Sort((ModellingConnection a, ModellingConnection b) => a.CompareTo(b));
                    AppendConnectionsGroupHtml(ownerReport.Interfaces, ownerReport, chapterNumber, ref report, true);
                }
                if(ownerReport.CommonServices.Count > 0)
                {
                    report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("own_common_services")}</h4>");
                    AppendConnectionsGroupHtml(ownerReport.CommonServices, ownerReport, chapterNumber, ref report);
                }
                AppendNetworkObjectsHtml(ownerReport.AllObjects, chapterNumber, ref report);
                AppendNetworkServicesHtml(ownerReport.AllServices, chapterNumber, ref report);
            }
            if(ReportData.GlobalComSvc.Count > 0 && ReportData.GlobalComSvc.First().GlobalComSvcs.Count > 0)
            {
                chapterNumber++;
                ReportData.GlobalComSvc.First().PrepareObjectData();
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{userConfig.GetText("global_common_services")}</h3>");
                AppendConnectionsGroupHtml(ReportData.GlobalComSvc.First().GlobalComSvcs, ReportData.GlobalComSvc.First(), chapterNumber, ref report, false, true);
                AppendNetworkObjectsHtml(ReportData.GlobalComSvc.First().AllObjects, chapterNumber, ref report);
                AppendNetworkServicesHtml(ReportData.GlobalComSvc.First().AllServices, chapterNumber, ref report);
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendConnectionsGroupHtml(List<ModellingConnection> connections, ConnectionReport connReport, int chapterNumber, ref StringBuilder report, bool isInterface = false, bool isGlobalComSvc = false)
        {
            ConnectionReport.AssignConnectionNumbers(connections);
            report.AppendLine("<table>");
            AppendConnectionHeadlineHtml(ref report, isGlobalComSvc, isInterface);
            foreach (var connection in connections)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{connection.OrderNumber}</td>");
                report.AppendLine($"<td>{connection.Id}</td>");
                if(isInterface)
                {
                    report.AppendLine($"<td>{connection.IsPublished.ShowAsHtml()}</td>");
                }
                if(isGlobalComSvc)
                {
                    report.AppendLine($"<td>{connection.App.Name}</td>");
                }
                report.AppendLine($"<td>{connection.Name}</td>");
                report.AppendLine($"<td>{connection.Reason}</td>");
                if(!isGlobalComSvc && ((connection.InterfaceIsRequested && connection.SrcFromInterface) || (connection.IsRequested && connection.SourceFilled())))
                {
                    report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested, 
                        connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                }
                else
                {
                    report.AppendLine($"<td>{string.Join("<br>", connReport.GetLinkedSrcNames(connection, chapterNumber))}</td>");
                }
                if(!isGlobalComSvc && (connection.InterfaceIsRequested || connection.IsRequested))
                {
                    report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested,
                        connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                }
                else
                {
                    report.AppendLine($"<td>{string.Join("<br>", connReport.GetLinkedSvcNames(connection, chapterNumber))}</td>");
                }
                if(!isGlobalComSvc && ((connection.InterfaceIsRequested && connection.DstFromInterface) || (connection.IsRequested && connection.DestinationFilled())))
                {
                    report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested,
                        connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                }
                else
                {
                    report.AppendLine($"<td>{string.Join("<br>", connReport.GetLinkedDstNames(connection, chapterNumber))}</td>");
                }
            }
            report.AppendLine("</table>");
            report.AppendLine("<hr>");
        }

        private void AppendConnectionHeadlineHtml(ref StringBuilder report, bool showOwnerName, bool isInterface = false)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            if(isInterface)
            {
                report.AppendLine($"<th>{userConfig.GetText("published")}</th>");
            }
            if(showOwnerName)
            {
                report.AppendLine($"<th>{userConfig.GetText("owner")}</th>");
            }
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{(isInterface ? userConfig.GetText("interface_description") : userConfig.GetText("func_reason"))}</th>");
            report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
            report.AppendLine("</tr>");
        }

        private void AppendNetworkObjectsHtml(List<NetworkObject> networkObjects, int chapterNumber, ref StringBuilder report)
        {
            report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("network_objects")}</h4>");
            report.AppendLine("<table>");
            if(networkObjects.Count > 0)
            {
                AppendNWObjHeadlineHtml(ref report);
            }
            foreach (var nwObj in networkObjects)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{nwObj.Number}</td>");
                report.AppendLine($"<td>{nwObj.Id}</td>");
                report.AppendLine($"<td><a name={ObjCatString.NwObj}{chapterNumber}x{nwObj.Id}>{nwObj.Name}</a></td>");
                report.AppendLine($"<td>{nwObj.IP}</td>");
                report.AppendLine(nwObj.MemberNamesAsHtml());
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
            report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
            report.AppendLine("</tr>");
        }

        private void AppendNetworkServicesHtml(List<NetworkService> networkServices, int chapterNumber, ref StringBuilder report)
        {
            report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("network_services")}</h4>");
            report.AppendLine("<table>");
            if(networkServices.Count > 0)
            {
                AppendNWSvcHeadlineHtml(ref report);
            }
            foreach (var svc in networkServices)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{svc.Number}</td>");
                report.AppendLine($"<td>{svc.Id}</td>");
                report.AppendLine($"<td><a name={ObjCatString.Svc}{chapterNumber}x{svc.Id}>{svc.Name}</a></td>");
                report.AppendLine($"<td>{svc.Protocol.Name}</td>");
                report.AppendLine($"<td>{svc.DestinationPort}</td>");
                report.AppendLine(svc.MemberNamesAsHtml());
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
            report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
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
