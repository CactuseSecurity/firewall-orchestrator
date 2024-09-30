using FWO.GlobalConstants;
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
                    ownerReport.Interfaces.Sort((ModellingConnection a, ModellingConnection b) => a.CompareTo(b));
                    AppendConnectionsGroupHtml(ownerReport.Interfaces, ownerReport, ref report, true);
                }
                if(ownerReport.CommonServices.Count > 0)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("own_common_services")}</h4>");
                    AppendConnectionsGroupHtml(ownerReport.CommonServices, ownerReport, ref report);
                }

                AppendNetworkObjectsHtml(ownerReport.AllObjects, ref report);
                AppendNetworkServicesHtml(ownerReport.AllServices, ref report);
            }
            if(ReportData.GlobalComSvc.Count > 0)
            {
                report.AppendLine($"<h3>{userConfig.GetText("global_common_services")}</h3>");
                AppendConnectionsGroupHtml(ReportData.GlobalComSvc, null, ref report);
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendConnectionsGroupHtml(List<ModellingConnection> connections, OwnerReport? ownerReport, ref StringBuilder report, bool isInterface = false)
        {
            OwnerReport.AssignConnectionNumbers(connections);
            bool IsGlobalComSvc = ownerReport == null;
            report.AppendLine("<table>");
            AppendConnectionHeadlineHtml(ref report, IsGlobalComSvc, isInterface);
            foreach (var connection in connections)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{connection.OrderNumber}</td>");
                report.AppendLine($"<td>{connection.Id}</td>");
                if(isInterface)
                {
                    report.AppendLine($"<td>{GlobalFunc.ShowBool(connection.IsPublished)}</td>");
                }
                if(IsGlobalComSvc)
                {
                    report.AppendLine($"<td>{connection.App.Name}</td>");
                }
                report.AppendLine($"<td>{connection.Name}</td>");
                report.AppendLine($"<td>{connection.Reason}</td>");
                if(IsGlobalComSvc)
                {
                    report.AppendLine($"<td>{String.Join("<br>", OwnerReport.GetSrcNames(connection))}</td>");
                    report.AppendLine($"<td>{String.Join("<br>", OwnerReport.GetSvcNames(connection))}</td>");
                    report.AppendLine($"<td>{String.Join("<br>", OwnerReport.GetDstNames(connection))}</td>");
                }
                else
                {
                    if((connection.InterfaceIsRequested && connection.SrcFromInterface) || (connection.IsRequested && connection.SourceFilled()))
                    {
                        report.AppendLine($"<td>{DisplayReqInt(connection.TicketId, connection.InterfaceIsRequested, 
                            connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                    }
                    else
                    {
                        report.AppendLine($"<td>{String.Join("<br>", ownerReport!.GetLinkedSrcNames(connection))}</td>");
                    }
                    if(connection.InterfaceIsRequested || connection.IsRequested)
                    {
                        report.AppendLine($"<td>{DisplayReqInt(connection.TicketId, connection.InterfaceIsRequested,
                            connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                    }
                    else
                    {
                        report.AppendLine($"<td>{String.Join("<br>", ownerReport!.GetLinkedSvcNames(connection))}</td>");
                    }
                    if((connection.InterfaceIsRequested && connection.DstFromInterface) || (connection.IsRequested && connection.DestinationFilled()))
                    {
                        report.AppendLine($"<td>{DisplayReqInt(connection.TicketId, connection.InterfaceIsRequested,
                            connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                    }
                    else
                    {
                        report.AppendLine($"<td>{String.Join("<br>", ownerReport!.GetLinkedDstNames(connection))}</td>");
                    }
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

        private void AppendNetworkObjectsHtml(List<NetworkObject> networkObjects, ref StringBuilder report)
        {
            report.AppendLine($"<h4>{userConfig.GetText("network_objects")}</h4>");
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
                report.AppendLine($"<td><a name={ObjCatString.NwObj}{nwObj.Number}>{nwObj.Name}</a></td>");
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

        private void AppendNetworkServicesHtml(List<NetworkService> networkServices, ref StringBuilder report)
        {
            report.AppendLine($"<h4>{userConfig.GetText("network_services")}</h4>");
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
                report.AppendLine($"<td><a name={ObjCatString.Svc}{svc.Number}>{svc.Name}</a></td>");
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

        // same as in ModellingHandlerBase (not reachable from here) -> ToDo: redesign!
        private string DisplayReqInt(long? ticketId, bool otherOwner, bool rejected = false)
        {
            string tooltipKey = rejected ? "C9011": otherOwner ? "C9007" : "C9008";
            string tooltip = $"data-toggle=\"tooltip\" title=\"{userConfig.GetText(tooltipKey)}\"";
            string content = $"{userConfig.GetText(rejected ? "InterfaceRejected" : "interface_requested")}: ({userConfig.GetText("ticket")} {ticketId?.ToString()})";
            return $"<span class=\"{(rejected ? "text-danger" : "text-warning")}\" {tooltip}><i>{content}</i></span>";
        }
    }
}
