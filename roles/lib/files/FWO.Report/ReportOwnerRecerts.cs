using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Ui.Display;
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

            if (ReportData.MergeOwnerRecertTables)
            {
                AppendOwnerTableCsv(ref report, GetMergedHeadline(), GetMergedOwners(overdueOwners, upcomingOwners, furtherOwners, inactiveOwners), true);
            }
            else
            {
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
            if (ReportData.MergeOwnerRecertTables)
            {
                report.AppendLine(Headline(GetMergedHeadline(), 3));
                AppendOwnerTable(ref report, GetMergedOwners(overdueOwners, upcomingOwners, furtherOwners, inactiveOwners), true);
            }
            else
            {
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
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private List<FwoOwner> GetMergedOwners(List<FwoOwner> overdueOwners, List<FwoOwner> upcomingOwners,
            List<FwoOwner> furtherOwners, List<FwoOwner> inactiveOwners)
        {
            return [.. GetDisplayedOwners(overdueOwners, upcomingOwners, furtherOwners, inactiveOwners)
                .OrderBy(owner => owner.GetEffectiveNextRecertDate(userConfig.RecertificationPeriod) ?? DateTime.MaxValue)
                .ThenBy(owner => owner.ExtAppId ?? "")
                .ThenBy(owner => owner.Name)];
        }

        private static List<FwoOwner> GetDisplayedOwners(List<FwoOwner> overdueOwners, List<FwoOwner> upcomingOwners,
            List<FwoOwner> furtherOwners, List<FwoOwner> inactiveOwners)
        {
            return [.. overdueOwners, .. upcomingOwners, .. furtherOwners, .. inactiveOwners];
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

        private void AppendOwnerDataHtml(ref StringBuilder report, FwoOwner owner, bool includeRecertData)
        {
            report.AppendLine("<tr>");
            if (includeRecertData)
            {
                report.AppendLine($"<td>{FormatHtmlCell(OwnerRecertDisplay.FormatNextRecertDate(owner, userConfig))}</td>");
            }
            report.AppendLine($"<td>{owner.ExtAppId}</td>");
            report.AppendLine($"<td>{owner.Name}</td>");
            report.AppendLine($"<td>{FormatHtmlCell(OwnerRecertDisplay.FormatMainResponsibles(owner))}</td>");
            if (includeRecertData)
            {
                report.AppendLine($"<td>{FormatHtmlCell(OwnerRecertDisplay.FormatLastRecertified(owner, userConfig))}</td>");
                report.AppendLine($"<td>{new DistName(owner.LastRecertifierDn).UserName}</td>");
            }
            if (HasOwnerAdditionalInfoColumn())
            {
                report.AppendLine($"<td>{FormatOwnerAdditionalInfoValueHtml(owner)}</td>");
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
            report.Append(OutputCsv(userConfig.GetText("main_responsible")));
            if (includeRecertData)
            {
                report.Append(OutputCsv(userConfig.GetText("last_recertified")));
                report.Append(OutputCsv(userConfig.GetText("last_recertifier")));
            }
            if (HasOwnerAdditionalInfoColumn())
            {
                report.Append(OutputCsv(GetOwnerAdditionalInfoHeadline()));
            }
            report.AppendLine();
        }

        private void AppendOwnerDataCsv(ref StringBuilder report, FwoOwner owner, bool includeRecertData)
        {
            if (includeRecertData)
            {
                report.Append(OutputCsv(OwnerRecertDisplay.FormatNextRecertDate(owner, userConfig)));
            }
            report.Append(OutputCsv(owner.ExtAppId));
            report.Append(OutputCsv(owner.Name));
            report.Append(OutputCsv(OwnerRecertDisplay.FormatMainResponsibles(owner)));
            if (includeRecertData)
            {
                report.Append(OutputCsv(OwnerRecertDisplay.FormatLastRecertified(owner, userConfig)));
                report.Append(OutputCsv(new DistName(owner.LastRecertifierDn).UserName));
            }
            if (HasOwnerAdditionalInfoColumn())
            {
                report.Append(OutputCsv(GetOwnerAdditionalInfoValue(owner)));
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
            report.AppendLine($"<th>{userConfig.GetText("main_responsible")}</th>");
            if (includeRecertData)
            {
                report.AppendLine($"<th>{userConfig.GetText("last_recertified")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("last_recertifier")}</th>");
            }
            if (HasOwnerAdditionalInfoColumn())
            {
                report.AppendLine($"<th>{FormatHtmlCell(GetOwnerAdditionalInfoHeadline())}</th>");
            }
            report.AppendLine("</tr>");
        }

        private bool HasOwnerAdditionalInfoColumn()
        {
            return !string.IsNullOrWhiteSpace(ReportData.OwnerAdditionalInfoKey);
        }

        private string GetOwnerAdditionalInfoHeadline()
        {
            return $"{userConfig.GetText("label")}: {ReportData.OwnerAdditionalInfoKey}";
        }

        private string GetOwnerAdditionalInfoValue(FwoOwner owner)
        {
            return OwnerRecertDisplay.FormatAdditionalInfoValue(owner, ReportData.OwnerAdditionalInfoKey);
        }

        private string FormatOwnerAdditionalInfoValueHtml(FwoOwner owner)
        {
            string value = GetOwnerAdditionalInfoValue(owner);
            return OwnerRecertDisplay.TryParseBooleanValue(value, out bool boolValue)
                ? boolValue.ShowAsHtml().ToString()
                : FormatHtmlCell(value);
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

        private string GetMergedHeadline()
        {
            return userConfig.GetText("owner_recert_overview");
        }

    }
}
