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
            List<FwoOwner> overdueOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertOverdue)];
            List<FwoOwner> upcomingOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertUpcoming)];
            List<FwoOwner> furtherOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => !ow.RecertOverdue && !ow.RecertUpcoming)];

            StringBuilder report = new();
            if (overdueOwners.Count > 0)
            {
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{userConfig.GetText("U4003")}</h3>");
                AppendOwnerTable(ref report, overdueOwners);
            }
            else
            {
                report.AppendLine(userConfig.GetText("U4004"));
            }
            report.AppendLine("<hr>");
            if (upcomingOwners.Count > 0)
            {
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{userConfig.GetText("U4005").Replace(Placeholder.DAYS, ReportData.RecertificationDisplayPeriod.ToString())}</h3>");
                AppendOwnerTable(ref report, upcomingOwners);
            }
            else if (ReportData.RecertificationDisplayPeriod > 0)
            {
                report.AppendLine(userConfig.GetText("U4006").Replace(Placeholder.DAYS, ReportData.RecertificationDisplayPeriod.ToString()));
            }
            report.AppendLine("<hr>");
            if (furtherOwners.Count > 0)
            {
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{userConfig.GetText("U4007")}</h3>");
                AppendOwnerTable(ref report, furtherOwners);
            }
           
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendOwnerTable(ref StringBuilder report, List<FwoOwner> owners)
        {
            report.AppendLine("<table>");
            AppendOwnerDataHeadlineHtml(ref report);
            foreach (var owner in owners)
            {
                AppendOwnerDataHtml(ref report, owner);
            }
            report.AppendLine("</table>");
        }

        private static void AppendOwnerDataHtml(ref StringBuilder report, FwoOwner owner)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{owner.NextRecertDate?.ToString("dd.MM.yyyy")}</td>");
            report.AppendLine($"<td>{owner.Id}</td>");
            report.AppendLine($"<td>{owner.Name}</td>");
            report.AppendLine($"<td>{owner.LastRecertified}</td>");
            report.AppendLine($"<td>{new DistName(owner.LastRecertifierDn).UserName}</td>");
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
