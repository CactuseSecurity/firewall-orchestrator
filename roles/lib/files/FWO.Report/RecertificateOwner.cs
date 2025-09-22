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

        public void AppendOwnerData(ref StringBuilder report, List<OwnerConnectionReport> ownerReports, int chapterNumber)
        {
            foreach (var ownerReport in ownerReports)
            {
                chapterNumber++;
                AppendRecertData(ref report, ownerReport);
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{ownerReport.Name}</h3>");
                AppendConnDataForOwner(ref report, ownerReport, chapterNumber);
                report.AppendLine("<hr>");
            }
        }

        private void AppendRecertData(ref StringBuilder report, OwnerConnectionReport ownerReport)
        {
            string recertText = $"{userConfig.GetText("recertified_by")} {new DistName(ownerReport.Owner.LastRecertifierDn).UserName}: {ownerReport.Owner.LastRecertified?.ToString("dd.MM.yyyy HH:mm") ?? "-"}";
            report.AppendLine($"<h4>{recertText}</h4>");
        }
    }
}
