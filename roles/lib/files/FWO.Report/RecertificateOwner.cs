using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using System.Text;

namespace FWO.Report
{
    public class RecertificateOwner(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportConnections(query, userConfig, reportType)
    {
        public override string ExportToHtml()
        {
            StringBuilder report = new();
            int chapterNumber = 0;
            AppendOwnerData(ref report, ReportData.OwnerData, chapterNumber);

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public void AppendOwnerData(ref StringBuilder report, List<OwnerConnectionReport> ownerReports, int chapterNumber, int levelshift = 0)
        {
            Levelshift = levelshift;
            foreach (var ownerReport in ownerReports)
            {
                chapterNumber++;
                report.AppendLine(Headline(GetRecertText(ownerReport, userConfig), 2));
                AppendConnDataForOwner(ref report, ownerReport, chapterNumber);
                report.AppendLine("<hr>");
            }
        }

        public static string GetRecertText(OwnerConnectionReport ownerReport, UserConfig userConfig)
        {
            return $"{userConfig.GetText("recertification")} {ownerReport.Owner.LastRecertified?.ToString("dd.MM.yyyy HH:mm") ?? "-"} " +
                (ownerReport.Owner.LastRecertifierDn != null ? $"{userConfig.GetText("by")} {new DistName(ownerReport.Owner.LastRecertifierDn).UserName}" : "");
        }
    }
}
