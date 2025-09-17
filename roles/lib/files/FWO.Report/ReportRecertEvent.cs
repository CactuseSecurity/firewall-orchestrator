using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using System.Text;

namespace FWO.Report
{
    public class ReportRecertEvent(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportConnections(query, userConfig, reportType)
    {
        public override string ExportToHtml()
        {
            StringBuilder report = new();
            int chapterNumber = 0;
            foreach (var ownerReport in ReportData.OwnerData)
            {
                chapterNumber++;
                AppendRecertData(ref report, ownerReport);
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{ownerReport.Name}</h3>");
                AppendConnDataForOwner(ref report, ownerReport, chapterNumber);
                report.AppendLine("<hr>");
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendRecertData(ref StringBuilder report, OwnerConnectionReport ownerReport)
        {
            string recertText = $"{userConfig.GetText("recertified_by")} {new DistName(ownerReport.Owner.LastRecertifierDn).UserName}: {ownerReport.Owner.LastRecertified?.ToString("dd.MM.yyyy HH:mm") ?? "-"}";
            report.AppendLine($"<h4>{recertText}</h4>");
        }
    }
}
