using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Ui.Display;
using System.Text;

namespace FWO.Report
{
    public class ReportVariances(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportConnections(query, userConfig, reportType)
    {
        private RuleDifferenceDisplayHtml? ruleDiffDisplay;

        public override string ExportToHtml()
        {
            StringBuilder report = new ();
            ruleDiffDisplay = new (userConfig);
            int chapterNumber = 0;
            report.AppendLine($"{userConfig.GetText("U1003")}<br>");
            foreach (var ownerReport in ReportData.OwnerData)
            {
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{ownerReport.Name}</h3>");
                if(ownerReport.ImplementationState != "")
                {
                    report.AppendLine($"{ownerReport.ImplementationState}<br>");
                }
                AppendStats(ref report, ownerReport);
                report.AppendLine("<hr>");
                AppendMissingAppRoles(ref report, ownerReport);
                AppendAppRoleDiffs(ref report, ownerReport);
                AppendMissingConns(ref report, ownerReport, chapterNumber);
                AppendConnDiffs(ref report, ownerReport, chapterNumber);
                AppendObjects(ref report, ownerReport, chapterNumber);
                AppendRemainingRules(ref report, ownerReport, chapterNumber);
                report.AppendLine("<hr>");
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public static OwnerReport CollectObjectsInReport(OwnerReport ownerReport)
        {
            OwnerReport modifiedOwnerReport = new(){ Connections = [.. ownerReport.Connections] };
            modifiedOwnerReport.Connections.AddRange(ownerReport.RuleDifferences.ConvertAll(o => o.ModelledConnection));
            if(ownerReport.MissingAppRoles.Count > 0 || ownerReport.DifferingAppRoles.Count > 0)
            {
                ModellingConnection diffConn = new();
                foreach(var mgt in ownerReport.MissingAppRoles.Keys)
                {
                    diffConn.SourceAppRoles.AddRange(ownerReport.MissingAppRoles[mgt].ConvertAll(a => new ModellingAppRoleWrapper(){ Content = a }));
                    diffConn.DestinationAppRoles.AddRange(ownerReport.DifferingAppRoles[mgt].ConvertAll(a => new ModellingAppRoleWrapper(){ Content = a }));
                }
                modifiedOwnerReport.Connections.Add(diffConn);
            }
            return modifiedOwnerReport;
        }

        public override string SetDescription()
        {
            int missConnCounter = 0;
            int diffConnCounter = 0;
            int missARCounter = 0;
            int diffARCounter = 0;
            foreach(var owner in ReportData.OwnerData)
            {
                missConnCounter += owner.Connections.Count;
                diffConnCounter += owner.RuleDifferences.Count;
                missARCounter += owner.AppRoleStats.AppRolesMissingCount;
                diffARCounter += owner.AppRoleStats.AppRolesDifferenceCount;
            }
            string appRoles = $"{userConfig.GetText("app_roles")}: {missARCounter} {userConfig.GetText("not_implemented")}, {diffARCounter} {userConfig.GetText("with_diffs")}, ";
            return $"{appRoles}{userConfig.GetText("connections")}.: {missConnCounter} {userConfig.GetText("not_implemented")}, {diffConnCounter} {userConfig.GetText("with_diffs")}";
        }

        private void AppendStats(ref StringBuilder report, OwnerReport ownerReport)
        {
            report.AppendLine("<table>");
            report.AppendLine("<tr>");
            report.AppendLine($"<th></th>");
            report.AppendLine($"<th>{userConfig.GetText("fully_modelled")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("implemented")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("not_implemented")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("with_diffs")}</th>");
            report.AppendLine("</tr>");

            if(ownerReport.AppRoleStats.ModelledAppRolesCount > 0)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{userConfig.GetText("app_roles")}</td>");
                report.AppendLine($"<td>{ownerReport.AppRoleStats.ModelledAppRolesCount}</td>");
                report.AppendLine($"<td>{ownerReport.AppRoleStats.AppRolesOk}</td>");
                report.AppendLine($"<td>{ownerReport.AppRoleStats.AppRolesMissingCount}</td>");
                report.AppendLine($"<td>{ownerReport.AppRoleStats.AppRolesDifferenceCount}</td>");
                report.AppendLine("</tr>");
            }
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{userConfig.GetText("connections")}</td>");
            report.AppendLine($"<td>{ownerReport.ModelledConnectionsCount}</td>");
            report.AppendLine($"<td>{ownerReport.ModelledConnectionsCount - ownerReport.RuleDifferences.Count - ownerReport.Connections.Count}</td>");
            report.AppendLine($"<td>{ownerReport.Connections.Count}</td>");
            report.AppendLine($"<td>{ownerReport.RuleDifferences.Count}</td>");
            report.AppendLine("</tr>");
            report.AppendLine("</table>");
        }

        private void AppendMissingAppRoles(ref StringBuilder report, OwnerReport ownerReport)
        {
            if(ownerReport.AppRoleStats.AppRolesMissingCount > 0)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("app_roles_not_implemented")}</h4>");
                foreach(var mgt in ownerReport.MissingAppRoles.Keys)
                {
                    if(ownerReport.MissingAppRoles[mgt].Count > 0)
                    {
                        report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{ownerReport.MissingAppRoles[mgt][0].ManagementName}</h5>");
                        AppendAppRolesHtml(ownerReport.MissingAppRoles[mgt], ref report);
                    }
                }
                report.AppendLine("<hr>");
            }
        }

        private void AppendAppRoleDiffs(ref StringBuilder report, OwnerReport ownerReport)
        {
            if(ownerReport.AppRoleStats.AppRolesDifferenceCount > 0)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("app_roles_with_diffs")}</h4>");
                foreach(var mgt in ownerReport.DifferingAppRoles.Keys)
                {
                    if(ownerReport.DifferingAppRoles[mgt].Count > 0)
                    {
                        report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{ownerReport.DifferingAppRoles[mgt][0].ManagementName}</h5>");
                        AppendAppRolesHtml(ownerReport.DifferingAppRoles[mgt], ref report, true, true);
                    }
                }
                report.AppendLine("<hr>");
            }
        }

        private void AppendAppRolesHtml(List<ModellingAppRole> appRoles, ref StringBuilder report, bool diffMode = false, bool split = false)
        {
            SetObjectNumbers(appRoles);
            report.AppendLine("<table>");
            if(appRoles.Count > 0)
            {
                AppendAppRoleHeadlineHtml(ref report, split);
            }
            foreach (var appRole in appRoles)
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{appRole.Number}</td>");
                report.AppendLine($"<td>{appRole.Id}</td>");
                report.AppendLine($"<td>{appRole.Name}</td>");
                if(split)
                {
                    report.AppendLine($"<td>{ConnectionReport.ListAppServers([.. ModellingAppServerWrapper.Resolve(appRole.AppServers)], [])}</td>");
                    report.AppendLine($"<td>{ConnectionReport.ListAppServers([.. ModellingAppServerWrapper.Resolve(appRole.SurplusAppServers)], [])}</td>");
                }
                else
                {
                    report.AppendLine($"<td>{ConnectionReport.ListAppServers([.. ModellingAppServerWrapper.Resolve(appRole.AppServers)],
                        [.. ModellingAppServerWrapper.Resolve(appRole.SurplusAppServers)], diffMode, true)}</td>");
                }
            }
            report.AppendLine("</table>");
            report.AppendLine("<hr>");
        }

        private static void SetObjectNumbers(List<ModellingAppRole> appRoles)
        {
            long number = 1;
            foreach(var appRole in appRoles)
            {
                appRole.Number = number++;
            }
        }

        private void AppendAppRoleHeadlineHtml(ref StringBuilder report, bool split)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            if(split)
            {
                report.AppendLine($"<th>{userConfig.GetText("missing_app_servers")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("surplus_app_servers")}</th>");
            }
            else
            {
                report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
            }
            report.AppendLine("</tr>");
        }

        private void AppendMissingConns(ref StringBuilder report, OwnerReport ownerReport, int chapterNumber)
        {
            if(ownerReport.RegularConnections.Count > 0)
            {
                chapterNumber++;
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("connections_not_implemented")}</h4>");
                if(ownerReport.RegularConnections.Count > 0)
                {
                    report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{userConfig.GetText("connections")}</h5>");
                    AppendConnectionsGroupHtml(ownerReport.RegularConnections, ownerReport, chapterNumber, ref report, new(){ WithoutLinks = true });
                }
                if(ownerReport.CommonServices.Count > 0)
                {
                    report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{userConfig.GetText("own_common_services")}</h5>");
                    AppendConnectionsGroupHtml(ownerReport.CommonServices, ownerReport, chapterNumber, ref report, new(){ WithoutLinks = true });
                }
                report.AppendLine("<hr>");
            }
        }

        private void AppendConnDiffs(ref StringBuilder report, OwnerReport ownerReport, int chapterNumber)
        {
            if(ownerReport.RuleDifferences.Count > 0 && ruleDiffDisplay != null)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("connections_with_diffs")}</h4>");
                foreach(var difference in ownerReport.RuleDifferences)
                {
                    bool anyUnusedObjects = difference.ImplementedRules.Any(r => r.UnusedSpecialUserObjects.Count > 0 || r.UnusedUpdatableObjects.Count > 0);
                    report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{difference.ModelledConnection.Name}</h5>");
                    AppendConnectionsGroupHtml([difference.ModelledConnection], ownerReport, chapterNumber, ref report, new(){ WithoutLinks = true, WithoutNumber = true });
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("management")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("gateway")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
                    if(anyUnusedObjects)
                    {
                        report.AppendLine($"<th>{userConfig.GetText("missing_objects")}</th>");
                    }
                    report.AppendLine("</tr>");

                    foreach (var diff in difference.ImplementedRules)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{diff.ManagementName}</td>");
                        report.AppendLine($"<td>{diff.DeviceName}</td>");
                        report.AppendLine($"<td>{ruleDiffDisplay.DisplaySourceDiff(diff, OutputLocation.export, ReportType)}</td>");
                        report.AppendLine($"<td>{ruleDiffDisplay.DisplayServiceDiff(diff, OutputLocation.export, ReportType)}</td>");
                        report.AppendLine($"<td>{ruleDiffDisplay.DisplayDestinationDiff(diff, OutputLocation.export, ReportType)}</td>");
                        if(anyUnusedObjects)
                        {
                            List<string> unusedObjects = [.. diff.UnusedSpecialUserObjects, .. diff.UnusedUpdatableObjects];
                            report.AppendLine($"<td style=\"{GlobalConst.kStyleHighlightedRed}\">{string.Join(", ", unusedObjects)}</td>");
                        }
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }
                report.AppendLine("<hr>");
            }
        }

        private void AppendObjects(ref StringBuilder report, OwnerReport ownerReport, int chapterNumber)
        {
            List<ModellingConnection> relevantConns = CollectObjectsInReport(ownerReport).Connections;
            ownerReport.AllObjects = ConnectionReport.GetAllNetworkObjects(relevantConns, true, userConfig.ResolveNetworkAreas);
            ConnectionReport.SetObjectNumbers(ownerReport.AllObjects);
            ownerReport.AllServices = ConnectionReport.GetAllServices(relevantConns, true);
            ConnectionReport.SetSvcNumbers(ownerReport.AllServices);
            AppendNetworkObjectsHtml(ownerReport.AllObjects, chapterNumber, ref report);
            AppendNetworkServicesHtml(ownerReport.AllServices, chapterNumber, ref report);
        }
     
        private void AppendRemainingRules(ref StringBuilder report, OwnerReport ownerReport, int chapterNumber)
        {
            if(ownerReport.ManagementData.Count > 0)
            {
                ReportRules rulesReport = new(new(""), userConfig, ReportType.AppRules);
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("remaining_rules")}</h4>");
                rulesReport.ConstructHtmlReport(ref report, ownerReport.ManagementData, chapterNumber, true);
                report.AppendLine("<hr>");
            }
        }
    }
}
