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

        public override string ExportToCsv()
        {
            List<FwoOwner> overdueOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertOverdue)];
            List<FwoOwner> upcomingOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertUpcoming)];
            List<FwoOwner> furtherOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertActive && !ow.RecertOverdue && !ow.RecertUpcoming)];
            List<FwoOwner> inactiveOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => !ow.RecertActive).OrderBy(ow => ow.Id)];

            StringBuilder report = new();
            report.AppendLine($"# report type: {userConfig.GetText(ReportType.ToString())}");
            report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime():yyyy-MM-ddTHH:mm:ssK} (UTC)");
            if (!string.IsNullOrWhiteSpace(Query.RawFilter))
            {
                report.AppendLine($"# other filters: {Query.RawFilter}");
            }
            report.AppendLine($"# {userConfig.GetText("statistics")}");
            report.AppendLine($"# {GetOverdueHeadline()}: {overdueOwners.Count}");
            if (ReportData.RecertificationDisplayPeriod > 0)
            {
                report.AppendLine($"# {GetUpcomingHeadline()}: {upcomingOwners.Count}");
            }
            if (furtherOwners.Count > 0)
            {
                report.AppendLine($"# {GetFurtherHeadline(furtherOwners)}: {furtherOwners.Count}");
            }
            if (inactiveOwners.Count > 0)
            {
                report.AppendLine($"# {GetInactiveHeadline()}: {inactiveOwners.Count}");
            }
            report.AppendLine("#");

            AppendOwnerTableCsv(ref report, GetOverdueHeadline(), overdueOwners, true);
            if (ReportData.RecertificationDisplayPeriod > 0)
            {
                AppendOwnerTableCsv(ref report, GetUpcomingHeadline(), upcomingOwners, true);
            }
            if (furtherOwners.Count > 0)
            {
                AppendOwnerTableCsv(ref report, GetFurtherHeadline(furtherOwners), furtherOwners, true);
            }
            if (inactiveOwners.Count > 0)
            {
                AppendOwnerTableCsv(ref report, GetInactiveHeadline(), inactiveOwners, false);
            }

            return report.ToString();
        }

        public override string ExportToHtml()
        {
            List<FwoOwner> overdueOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertOverdue)];
            List<FwoOwner> upcomingOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertUpcoming)];
            List<FwoOwner> furtherOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => ow.RecertActive && !ow.RecertOverdue && !ow.RecertUpcoming)];
            List<FwoOwner> inactiveOwners = [.. ReportData.OwnerData.Select(o => o.Owner).Where(ow => !ow.RecertActive).OrderBy(ow => ow.Id)];

            StringBuilder report = new();
            report.AppendLine(Headline(userConfig.GetText("statistics"), 3));
            report.AppendLine("<ul>");
            report.AppendLine($"<li>{GetOverdueHeadline()}: {overdueOwners.Count}</li>");
            if (ReportData.RecertificationDisplayPeriod > 0)
            {
                report.AppendLine($"<li>{GetUpcomingHeadline()}: {upcomingOwners.Count}</li>");
            }
            if (furtherOwners.Count > 0)
            {
                report.AppendLine($"<li>{GetFurtherHeadline(furtherOwners)}: {furtherOwners.Count}</li>");
            }
            if (inactiveOwners.Count > 0)
            {
                report.AppendLine($"<li>{GetInactiveHeadline()}: {inactiveOwners.Count}</li>");
            }
            report.AppendLine("</ul>");
            report.AppendLine("<hr>");
            if (overdueOwners.Count > 0)
            {
                report.AppendLine(Headline(GetOverdueHeadline(), 3));
                AppendOwnerTable(ref report, overdueOwners, true);
            }
            else
            {
                report.AppendLine(userConfig.GetText("U4004"));
            }
            report.AppendLine("<hr>");
            if (upcomingOwners.Count > 0)
            {
                report.AppendLine(Headline(GetUpcomingHeadline(), 3));
                AppendOwnerTable(ref report, upcomingOwners, true);
            }
            else if (ReportData.RecertificationDisplayPeriod > 0)
            {
                report.AppendLine(userConfig.GetText("U4006").Replace(Placeholder.DAYS, ReportData.RecertificationDisplayPeriod.ToString()));
            }
            report.AppendLine("<hr>");
            if (furtherOwners.Count > 0)
            {
                report.AppendLine(Headline(GetFurtherHeadline(furtherOwners), 3));
                AppendOwnerTable(ref report, furtherOwners, true);
            }
            if (inactiveOwners.Count > 0)
            {
                report.AppendLine("<hr>");
                report.AppendLine(Headline(GetInactiveHeadline(), 3));
                AppendOwnerTable(ref report, inactiveOwners, false);
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendOwnerTable(ref StringBuilder report, List<FwoOwner> owners, bool includeRecertData)
        {
            report.AppendLine("<table>");
            AppendOwnerDataHeadlineHtml(ref report, includeRecertData);
            foreach (var owner in owners)
            {
                AppendOwnerDataHtml(ref report, owner, includeRecertData);
            }
            report.AppendLine("</table>");
        }

        private void AppendOwnerTableCsv(ref StringBuilder report, string headline, List<FwoOwner> owners, bool includeRecertData)
        {
            report.AppendLine($"# {headline}");
            AppendOwnerDataHeadlineCsv(ref report, includeRecertData);
            foreach (var owner in owners)
            {
                AppendOwnerDataCsv(ref report, owner, includeRecertData);
            }
            report.AppendLine("#");
        }

        private static void AppendOwnerDataHtml(ref StringBuilder report, FwoOwner owner, bool includeRecertData)
        {
            report.AppendLine("<tr>");
            if (includeRecertData)
            {
                report.AppendLine($"<td>{owner.NextRecertDate?.ToString("dd.MM.yyyy")}</td>");
            }
            report.AppendLine($"<td>{owner.ExtAppId}</td>");
            report.AppendLine($"<td>{owner.Name}</td>");
            if (includeRecertData)
            {
                report.AppendLine($"<td>{owner.LastRecertified}</td>");
                report.AppendLine($"<td>{new DistName(owner.LastRecertifierDn).UserName}</td>");
            }
            report.AppendLine("</tr>");
        }

        private void AppendOwnerDataHeadlineCsv(ref StringBuilder report, bool includeRecertData)
        {
            if (includeRecertData)
            {
                report.Append(OutputCsv(userConfig.GetText("next_recert_date")));
            }
            report.Append(OutputCsv(userConfig.GetText("id")));
            report.Append(OutputCsv(userConfig.GetText("name")));
            if (includeRecertData)
            {
                report.Append(OutputCsv(userConfig.GetText("last_recertified")));
                report.Append(OutputCsv(userConfig.GetText("last_recertifier")));
            }
            report.AppendLine();
        }

        private static void AppendOwnerDataCsv(ref StringBuilder report, FwoOwner owner, bool includeRecertData)
        {
            if (includeRecertData)
            {
                report.Append(OutputCsv(owner.NextRecertDate?.ToString("dd.MM.yyyy")));
            }
            report.Append(OutputCsv(owner.ExtAppId));
            report.Append(OutputCsv(owner.Name));
            if (includeRecertData)
            {
                report.Append(OutputCsv(owner.LastRecertified?.ToString("dd.MM.yyyy")));
                report.Append(OutputCsv(new DistName(owner.LastRecertifierDn).UserName));
            }
            report.AppendLine();
        }

        private void AppendOwnerDataHeadlineHtml(ref StringBuilder report, bool includeRecertData)
        {
            report.AppendLine("<tr>");
            if (includeRecertData)
            {
                report.AppendLine($"<th>{userConfig.GetText("next_recert_date")}</th>");
            }
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            if (includeRecertData)
            {
                report.AppendLine($"<th>{userConfig.GetText("last_recertified")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("last_recertifier")}</th>");
            }
            report.AppendLine("</tr>");
        }

        private string GetOverdueHeadline()
        {
            return userConfig.GetText("U4003");
        }

        private string GetUpcomingHeadline()
        {
            return userConfig.GetText("U4005").Replace(Placeholder.DAYS, ReportData.RecertificationDisplayPeriod.ToString());
        }

        private string GetFurtherHeadline(List<FwoOwner> furtherOwners)
        {
            return userConfig.GetText(!furtherOwners.Any(o => o.NextRecertDate == null) ? "U4007" : "U4008");
        }

        private string GetInactiveHeadline()
        {
            return userConfig.GetText("U4009");
        }

    }
}
