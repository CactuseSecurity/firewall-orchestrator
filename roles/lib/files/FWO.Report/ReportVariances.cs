using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report.Filter;
using System.Text;

namespace FWO.Report
{
    public class ReportVariances : ReportConnections
    {
        public ReportVariances(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        public override string ExportToHtml()
        {
            StringBuilder report = new ();
            int chapterNumber = 0;
            foreach (var ownerReport in ReportData.OwnerData)
            {
                report.AppendLine(DisplayConnStat(ownerReport, userConfig));
                report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{userConfig.GetText("connections_not_implemented")}</h4>");
                AppendConnDataForOwner(ref report, ownerReport, chapterNumber);
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

    }
}
