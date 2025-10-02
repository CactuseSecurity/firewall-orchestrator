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
        public struct ConnDisplayFlags
        {
            public bool IsInterface { get; set; } = false;
            public bool IsGlobalComSvc { get; set; } = false;
            public bool WithoutLinks { get; set; } = false;
            public bool WithoutNumber { get; set; } = false;

            public ConnDisplayFlags()
            { }
        }

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            List<ModellingConnection> conns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(Query.FullQuery, Query.QueryVariables);
            ReportData reportData = new() { OwnerData = [new() { Connections = conns }] };
            await callback(reportData);

            foreach (var owner in ReportData.OwnerData)
            {
                ReportData.ElementsCount += owner.Connections.Count;
            }
        }

        public override string SetDescription()
        {
            int counter = 0;
            foreach (var owner in ReportData.OwnerData)
            {
                counter += owner.Connections.Count;
            }
            return $"{counter} {userConfig.GetText("connections")}";
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new();
            int chapterNumber = 0;
            foreach (var ownerReport in ReportData.OwnerData)
            {
                chapterNumber++;
                AppendConnDataForOwner(ref report, ownerReport, chapterNumber);
                report.AppendLine("<hr>");
            }

            if (ReportData.GlobalComSvc.Count > 0 && ReportData.GlobalComSvc[0].GlobalComSvcs.Count > 0)
            {
                chapterNumber++;
                ReportData.GlobalComSvc[0].PrepareObjectData(userConfig.ResolveNetworkAreas);
                report.AppendLine(Headline(userConfig.GetText("global_common_services"), 3));
                AppendConnectionsGroupHtml(ReportData.GlobalComSvc[0].GlobalComSvcs, ReportData.GlobalComSvc[0], chapterNumber, ref report, new() { IsGlobalComSvc = true });
                report.AppendLine("<hr>");
                AppendNetworkObjectsHtml(ReportData.GlobalComSvc[0].AllObjects, chapterNumber, ref report);
                AppendNetworkServicesHtml(ReportData.GlobalComSvc[0].AllServices, chapterNumber, ref report);
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public void AppendConnDataForOwner(ref StringBuilder report, OwnerConnectionReport ownerReport, int chapterNumber)
        {
            report.AppendLine(Headline(ownerReport.Owner.Display(userConfig.GetText("common_service")), 3));
            ownerReport.PrepareObjectData(userConfig.ResolveNetworkAreas);
            if (ownerReport.RegularConnections.Count > 0)
            {
                report.AppendLine(Headline(userConfig.GetText("connections"), 4));
                AppendConnectionsGroupHtml(ownerReport.RegularConnections, ownerReport, chapterNumber, ref report, new());
                report.AppendLine("<hr>");
            }
            if (ownerReport.Interfaces.Count > 0)
            {
                report.AppendLine(Headline(userConfig.GetText("interfaces"), 4));
                ownerReport.Interfaces.Sort((ModellingConnection a, ModellingConnection b) => a.CompareTo(b));
                AppendConnectionsGroupHtml(ownerReport.Interfaces, ownerReport, chapterNumber, ref report, new() { IsInterface = true });
                report.AppendLine("<hr>");
            }
            if (ownerReport.CommonServices.Count > 0)
            {
                report.AppendLine(Headline(userConfig.GetText("own_common_services"), 4));
                AppendConnectionsGroupHtml(ownerReport.CommonServices, ownerReport, chapterNumber, ref report, new());
                report.AppendLine("<hr>");
            }
            AppendNetworkObjectsHtml(ownerReport.AllObjects, chapterNumber, ref report);
            AppendNetworkServicesHtml(ownerReport.AllServices, chapterNumber, ref report);
        }

        protected void AppendConnectionsGroupHtml(List<ModellingConnection> connections, ConnectionReport connReport, int chapterNumber,
            ref StringBuilder report, ConnDisplayFlags flags)
        {
            ConnectionReport.AssignConnectionNumbers(connections);
            report.AppendLine("<table>");
            AppendConnectionHeadlineHtml(ref report, flags);
            foreach (var connection in connections)
            {
                AppendConnectionHtml(connection, connReport, chapterNumber, ref report, flags);
            }
            report.AppendLine("</table>");
        }

        protected void AppendConnectionHtml(ModellingConnection connection, ConnectionReport connReport, int chapterNumber,
            ref StringBuilder report, ConnDisplayFlags flags)
        {
            report.AppendLine("<tr>");
            if (!flags.WithoutNumber)
            {
                report.AppendLine($"<td>{connection.OrderNumber}</td>");
            }
            report.AppendLine($"<td>{connection.Id}</td>");
            if (flags.IsInterface)
            {
                report.AppendLine($"<td>{connection.IsPublished.ShowAsHtml()}</td>");
            }
            if (flags.IsGlobalComSvc)
            {
                report.AppendLine($"<td>{connection.App.Name}</td>");
            }
            report.AppendLine($"<td>{connection.Name}</td>");
            report.AppendLine($"<td>{connection.Reason}</td>");
            AppendSourcesHtml(connection, connReport, chapterNumber, ref report, flags);
            AppendServicesHtml(connection, connReport, chapterNumber, ref report, flags);
            AppendDestinationsHtml(connection, connReport, chapterNumber, ref report, flags);
        }

        protected void AppendSourcesHtml(ModellingConnection connection, ConnectionReport connReport, int chapterNumber,
            ref StringBuilder report, ConnDisplayFlags flags)
        {
            if (!flags.IsGlobalComSvc && ((connection.InterfaceIsRequested && connection.SrcFromInterface) || (connection.IsRequested && connection.SourceFilled())))
            {
                report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested,
                    connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
            }
            else if (flags.WithoutLinks)
            {
                report.AppendLine($"<td>{string.Join("<br>", GetPlainSrcNames(connection))}</td>");
            }
            else
            {
                report.AppendLine($"<td>{string.Join("<br>", GetLinkedSrcNames(connReport, connection, chapterNumber))}</td>");
            }
        }

        protected void AppendServicesHtml(ModellingConnection connection, ConnectionReport connReport, int chapterNumber,
            ref StringBuilder report, ConnDisplayFlags flags)
        {
            if (!flags.IsGlobalComSvc && (connection.InterfaceIsRequested || connection.IsRequested))
            {
                report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested,
                    connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
            }
            else if (flags.WithoutLinks)
            {
                report.AppendLine($"<td>{string.Join("<br>", GetPlainSvcNames(connection))}</td>");
            }
            else
            {
                report.AppendLine($"<td>{string.Join("<br>", GetLinkedSvcNames(connReport, connection, chapterNumber))}</td>");
            }
        }

        protected void AppendDestinationsHtml(ModellingConnection connection, ConnectionReport connReport, int chapterNumber,
            ref StringBuilder report, ConnDisplayFlags flags)
        {
            if (!flags.IsGlobalComSvc && ((connection.InterfaceIsRequested && connection.DstFromInterface) || (connection.IsRequested && connection.DestinationFilled())))
            {
                report.AppendLine($"<td>{ModellingHandlerBase.DisplayReqInt(userConfig, connection.TicketId, connection.InterfaceIsRequested,
                    connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.GetBoolProperty(ConState.InterfaceRejected.ToString()))}</td>");
            }
            else if (flags.WithoutLinks)
            {
                report.AppendLine($"<td>{string.Join("<br>", GetPlainDstNames(connection))}</td>");
            }
            else
            {
                report.AppendLine($"<td>{string.Join("<br>", GetLinkedDstNames(connReport, connection, chapterNumber))}</td>");
            }
        }

        private void AppendConnectionHeadlineHtml(ref StringBuilder report, ConnDisplayFlags flags)
        {
            report.AppendLine("<tr>");
            if (!flags.WithoutNumber)
            {
                report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            }
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            if (flags.IsInterface)
            {
                report.AppendLine($"<th>{userConfig.GetText("published")}</th>");
            }
            if (flags.IsGlobalComSvc)
            {
                report.AppendLine($"<th>{userConfig.GetText("owner")}</th>");
            }
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{(flags.IsInterface ? userConfig.GetText("interface_description") : userConfig.GetText("func_reason"))}</th>");
            report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
            report.AppendLine("</tr>");
        }

        protected void AppendNetworkObjectsHtml(List<NetworkObject> networkObjects, int chapterNumber, ref StringBuilder report)
        {
            if (networkObjects.Count > 0)
            {
                report.AppendLine(Headline(userConfig.GetText("network_objects"), 4));
                report.AppendLine("<table>");
                AppendNWObjHeadlineHtml(ref report);
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
            if (networkServices.Count > 0)
            {
                report.AppendLine(Headline(userConfig.GetText("network_services"), 4));
                report.AppendLine("<table>");
                AppendNWSvcHeadlineHtml(ref report);
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

        private static List<string> GetPlainSrcNames(ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => s.Display());
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => s.Display()));
            return names;
        }

        private static List<string> GetPlainDstNames(ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => s.Display());
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => s.Display()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => s.Display()));
            return names;
        }

        private static List<string> GetPlainSvcNames(ModellingConnection conn)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => s.Display());
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.Display()));
            return names;
        }

        private static List<string> GetLinkedSrcNames(ConnectionReport connReport, ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, true)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, true))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, true))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, true))));
            return names;
        }

        private static List<string> GetLinkedDstNames(ConnectionReport connReport, ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, false)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, false))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, false))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, connReport.ResolveObjId(s), GetStyle(conn, false))));
            return names;
        }

        private static List<string> GetLinkedSvcNames(ConnectionReport connReport, ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, chapterNumber, connReport.ResolveSvcId(s), GetStyle(conn)));
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, chapterNumber, connReport.ResolveSvcId(s), GetStyle(conn))));
            return names;
        }

        private static string ConstructOutput(ModellingObject inputObj, string type, int chapterNumber, long objId, string style)
        {
            string linkAddress = GetLinkAddress(OutputLocation.export, $"a{inputObj.AppId}", type, chapterNumber, objId, ReportType.Connections);
            return ConstructLink("", inputObj.Display(), style, linkAddress);
        }

        private static string GetStyle(ModellingConnection connection, bool? source = null)
        {
            if (connection.InterfaceIsDecommissioned && (source == null || ((bool)source && connection.SrcFromInterface) || (!(bool)source && connection.DstFromInterface)))
            {
                return "color: red";
            }
            return "";
        }
    }
}
