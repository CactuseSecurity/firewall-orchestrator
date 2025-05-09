using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
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
             foreach (var ownerReport in ReportData.OwnerData)
            {
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{ownerReport.Name}</h3>");
                report.AppendLine($"{DisplayAppRoleStat(ownerReport, userConfig)}<br>");
                report.AppendLine($"{DisplayConnStat(ownerReport, userConfig)}<br>");
                report.AppendLine("<hr>");
                AppendMissingAppRoles(ref report, ownerReport, chapterNumber);
                AppendAppRoleDiffs(ref report, ownerReport, chapterNumber);
                AppendMissingConns(ref report, ownerReport, chapterNumber);
                AppendConnDiffs(ref report, ownerReport, chapterNumber);
                AppendObjects(ref report, ownerReport, chapterNumber);
                AppendRemainingRules(ref report, ownerReport, chapterNumber);
                report.AppendLine("<hr>");
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public static string DisplayConnStat(OwnerReport ownerReport, UserConfig userConfig)
        {
            return $"{userConfig.GetText("connections")}: {ownerReport.ModelledConnectionsCount}, " +
                $"{userConfig.GetText("implemented")}: {ownerReport.ModelledConnectionsCount - ownerReport.RuleDifferences.Count - ownerReport.Connections.Count}, " +
                $"{userConfig.GetText("not_implemented")}: {ownerReport.Connections.Count}, " +
                $"{userConfig.GetText("with_diffs")}: {ownerReport.RuleDifferences.Count}";
        }

        public static string DisplayAppRoleStat(OwnerReport ownerReport, UserConfig userConfig)
        {
            return $"{userConfig.GetText("app_roles")}: {ownerReport.AppRoleStats.ModelledAppRolesCount}, " +
                $"{userConfig.GetText("implemented")}: {ownerReport.AppRoleStats.AppRolesOk}, " +
                $"{userConfig.GetText("not_implemented")}: {ownerReport.AppRoleStats.AppRolesMissingCount}, " +
                $"{userConfig.GetText("with_diffs")}: {ownerReport.AppRoleStats.AppRolesDifferenceCount}.";
        }

        public static OwnerReport CollectObjectsInReport(OwnerReport ownerReport)
        {
            OwnerReport modifiedOwnerReport = new(){ Connections = new(ownerReport.Connections) };
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

        private void AppendMissingAppRoles(ref StringBuilder report, OwnerReport ownerReport, int chapterNumber)
        {
            if(ownerReport.MissingAppRoles.Count > 0)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("app_roles_not_implemented")}</h4>");
                foreach(var mgt in ownerReport.MissingAppRoles.Keys)
                {
                    if(ownerReport.MissingAppRoles[mgt].Count > 0)
                    {
                        report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{ownerReport.MissingAppRoles[mgt].First().ManagementName}</h5>");
                        AppendAppRolesHtml(ownerReport.MissingAppRoles[mgt], chapterNumber, ref report);
                    }
                }
                report.AppendLine("<hr>");
            }
        }

        private void AppendAppRoleDiffs(ref StringBuilder report, OwnerReport ownerReport, int chapterNumber)
        {
            if(ownerReport.DifferingAppRoles.Count > 0)
            {
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("app_roles_with_diffs")}</h4>");
                foreach(var mgt in ownerReport.DifferingAppRoles.Keys)
                {
                    if(ownerReport.DifferingAppRoles[mgt].Count > 0)
                    {
                        report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{ownerReport.DifferingAppRoles[mgt].First().ManagementName}</h5>");
                        AppendAppRolesHtml(ownerReport.DifferingAppRoles[mgt], chapterNumber, ref report, true, true);
                    }
                }
                report.AppendLine("<hr>");
            }
        }

        private void AppendAppRolesHtml(List<ModellingAppRole> appRoles, int chapterNumber, ref StringBuilder report, bool diffMode = false, bool split = false)
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
                    AppendConnectionsGroupHtml(ownerReport.RegularConnections, ownerReport, chapterNumber, ref report, false, false, true);
                }
                if(ownerReport.CommonServices.Count > 0)
                {
                    report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{userConfig.GetText("own_common_services")}</h5>");
                    AppendConnectionsGroupHtml(ownerReport.CommonServices, ownerReport, chapterNumber, ref report, false, false, true);
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
                    bool anyUnusedSpecialUsers = difference.ImplementedRules.Any(r => r.UnusedSpecialUserObjects.Count > 0);
                    report.AppendLine($"<h5 id=\"{Guid.NewGuid()}\">{difference.ModelledConnection.Name}</h5>");
                    AppendConnectionsGroupHtml([difference.ModelledConnection], ownerReport, chapterNumber, ref report, false, false, true);
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("management")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("gateway")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
                    if(anyUnusedSpecialUsers)
                    {
                        report.AppendLine($"<th>{userConfig.GetText("missing_objects")}</th>");
                    }
                    report.AppendLine("</tr>");

                    Rule modelledRule = difference.ModelledConnection.ToRule();
                    foreach (var diff in difference.ImplementedRules)
                    {
                        RuleChange ruleChange = new (){ OldRule = modelledRule, NewRule = diff, ChangeAction = 'C' };
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{diff.ManagementName}</td>");
                        report.AppendLine($"<td>{diff.DeviceName}</td>");
                        report.AppendLine($"<td>{ruleDiffDisplay.DisplaySourceDiff(diff, OutputLocation.export, ReportType)}</td>");
                        report.AppendLine($"<td>{ruleDiffDisplay.DisplayServiceDiff(diff, OutputLocation.export, ReportType)}</td>");
                        report.AppendLine($"<td>{ruleDiffDisplay.DisplayDestinationDiff(diff, OutputLocation.export, ReportType)}</td>");
                        if(anyUnusedSpecialUsers)
                        {
                            report.AppendLine($"<td style=\"{GlobalConst.kStyleHighlightedRed}\">{string.Join(", ", diff.UnusedSpecialUserObjects)}</td>");
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
            ConnectionReport.SetObjectNumbers(ref ownerReport.AllObjects);
            ownerReport.AllServices = ConnectionReport.GetAllServices(relevantConns, true);
            ConnectionReport.SetSvcNumbers(ref ownerReport.AllServices);
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
