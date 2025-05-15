using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Services;
using System.Text;

namespace FWO.Report
{
    public class ReportConnections(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportOwnersBase(query, userConfig, reportType)
    {
        public override async Task Generate(int connectionsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            List<ModellingConnection> conns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);
            ReportData reportData = new() { OwnerData = [new(){ Connections = conns }] };
            await callback(reportData);
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

        public override string ExportToHtml()
        {
            StringBuilder report = new ();
            int chapterNumber = 0;
            foreach (var ownerReport in ReportData.OwnerData)
            {
                chapterNumber++;
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{ownerReport.Name}</h3>");
                AppendConnDataForOwner(ref report, ownerReport, chapterNumber);
                report.AppendLine("<hr>");
            }

            if(ReportData.GlobalComSvc.Count > 0 && ReportData.GlobalComSvc.First().GlobalComSvcs.Count > 0)
            {
                chapterNumber++;
                ReportData.GlobalComSvc.First().PrepareObjectData(userConfig.ResolveNetworkAreas);
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{userConfig.GetText("global_common_services")}</h3>");
                AppendConnectionsGroupHtml(ReportData.GlobalComSvc.First().GlobalComSvcs, ReportData.GlobalComSvc.First(), chapterNumber, ref report, false, true);
                report.AppendLine("<hr>");
                AppendNetworkObjectsHtml(ReportData.GlobalComSvc.First().AllObjects, chapterNumber, ref report);
                AppendNetworkServicesHtml(ReportData.GlobalComSvc.First().AllServices, chapterNumber, ref report);
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public void AppendConnDataForOwner(ref StringBuilder report, OwnerReport ownerReport, int chapterNumber)
        {
            ownerReport.PrepareObjectData(userConfig.ResolveNetworkAreas);
            if(ownerReport.RegularConnections.Count > 0)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("connections")}</h4>");
                AppendConnectionsGroupHtml(ownerReport.RegularConnections, ownerReport, chapterNumber, ref report);
                report.AppendLine("<hr>");
                
            }
            if(ownerReport.Interfaces.Count > 0)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("interfaces")}</h4>");
                ownerReport.Interfaces.Sort((ModellingConnection a, ModellingConnection b) => a.CompareTo(b));
                AppendConnectionsGroupHtml(ownerReport.Interfaces, ownerReport, chapterNumber, ref report, true);
                report.AppendLine("<hr>");
            }
            if(ownerReport.CommonServices.Count > 0)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("own_common_services")}</h4>");
                AppendConnectionsGroupHtml(ownerReport.CommonServices, ownerReport, chapterNumber, ref report);
                report.AppendLine("<hr>");
            }
            AppendNetworkObjectsHtml(ownerReport.AllObjects, chapterNumber, ref report);
            AppendNetworkServicesHtml(ownerReport.AllServices, chapterNumber, ref report);
        }

        protected void AppendConnectionsGroupHtml(List<ModellingConnection> connections, ConnectionReport connReport, int chapterNumber,
            ref StringBuilder report, bool isInterface = false, bool isGlobalComSvc = false, bool withoutLinks = false)
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
                else if(withoutLinks)
                {
                    report.AppendLine($"<td>{string.Join("<br>", GetPlainSrcNames(connReport, connection))}</td>");
                }
                else
                {
                    report.AppendLine($"<td>{string.Join("<br>", GetLinkedSrcNames(connReport, connection, chapterNumber))}</td>");
                }
                if(!isGlobalComSvc && (connection.InterfaceIsRequested || connection.IsRequested))
                {
                    report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested,
                        connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                }
                else if(withoutLinks)
                {
                    report.AppendLine($"<td>{string.Join("<br>", GetPlainSvcNames(connReport, connection))}</td>");
                }
                else
                {
                    report.AppendLine($"<td>{string.Join("<br>", GetLinkedSvcNames(connReport, connection, chapterNumber))}</td>");
                }
                if(!isGlobalComSvc && ((connection.InterfaceIsRequested && connection.DstFromInterface) || (connection.IsRequested && connection.DestinationFilled())))
                {
                    report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested,
                        connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
                }
                else if(withoutLinks)
                {
                    report.AppendLine($"<td>{string.Join("<br>", GetPlainDstNames(connReport, connection))}</td>");
                }
                else
                {
                    report.AppendLine($"<td>{string.Join("<br>", GetLinkedDstNames(connReport, connection, chapterNumber))}</td>");
                }
            }
            report.AppendLine("</table>");
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

        protected void AppendNetworkObjectsHtml(List<NetworkObject> networkObjects, int chapterNumber, ref StringBuilder report)
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

        protected void AppendNetworkServicesHtml(List<NetworkService> networkServices, int chapterNumber, ref StringBuilder report)
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

        private static List<string> GetPlainSrcNames(ConnectionReport connReport, ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => s.Display());
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => s.Display()));
            return names;
        }

        private static List<string> GetPlainDstNames(ConnectionReport connReport, ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => s.Display());
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => s.Display()));
            return names;
        }

        private static List<string> GetPlainSvcNames(ConnectionReport connReport, ModellingConnection conn)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => s.Display());
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.Display()));
            return names;
        }

        private static List<string> GetLinkedSrcNames(ConnectionReport connReport, ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s))));
            return names;
        }
        
        private static List<string> GetLinkedDstNames(ConnectionReport connReport, ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s))));
            return names;
        }

        private static List<string> GetLinkedSvcNames(ConnectionReport connReport, ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, chapterNumber, connReport.ResolveSvcId(s)));
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, chapterNumber, connReport.ResolveSvcId(s))));
            return names;
        }

        private static string ConstructOutput(ModellingObject inputObj, string type, int chapterNumber, long objId)
        {
            return ConstructLink(type, "", chapterNumber, objId, inputObj.Display(), OutputLocation.export, $"a{inputObj.AppId}", "");
        }
    }
}
