using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using System.Text;

namespace FWO.Report
{
    public class ReportOwnerRecerts(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportOwnersBase(query, userConfig, reportType)
    {
        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(Query.FullQuery, Query.QueryVariables);
            if (owners.Count > 0)
            {
                ReportData reportData = new() { OwnerData = [.. owners.ConvertAll(o => new OwnerConnectionReport() { Owner = o })] };
                await callback(reportData);
            }
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new();
            report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{userConfig.GetText("U4003")}</h3>");
            report.AppendLine("<table>");
            AppendOwnerDataHeadlineHtml(ref report);
            foreach (var ownerReport in ReportData.OwnerData)
            {
                AppendOwnerDataHtml(ref report, ownerReport);
            }
            report.AppendLine("</table>");
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public static void AppendOwnerDataHtml(ref StringBuilder report, OwnerConnectionReport ownerReport)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{ownerReport.Owner.NextRecertDate?.ToString("dd.MM.yyyy")}</td>");
            report.AppendLine($"<td>{ownerReport.Owner.Id}</td>");
            report.AppendLine($"<td>{ownerReport.Owner.Name}</td>");
            report.AppendLine($"<td>{ownerReport.Owner.LastRecertified}</td>");
            report.AppendLine($"<td>{new DistName(ownerReport.Owner.LastRecertifierDn).UserName}</td>");
            report.AppendLine("</tr>");
        }

        private void AppendOwnerDataHeadlineHtml(ref StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("next_recert_date")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("last_recertified")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("last_recertifier")}</th>");
            report.AppendLine("</tr>");
        }
    }
}
