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
            (AddInfoFilter ownerAddInfoFilter, List<OwnerConnectionReport> displayedOwnerData, List<FwoOwner> overdueOwners, List<FwoOwner> upcomingOwners,
                List<FwoOwner> furtherOwners, List<FwoOwner> inactiveOwners) = PrepareOwnerRecertCollections();

            StringBuilder report = new();
            report.AppendLine($"# report type: {userConfig.GetText(ReportType.ToString())}");
            report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime():yyyy-MM-ddTHH:mm:ssK} (UTC)");
            string addInfoFilterSummary = BuildOwnerAddInfoFilterSummary(ownerAddInfoFilter);
            if (!string.IsNullOrWhiteSpace(addInfoFilterSummary))
            {
                report.AppendLine($"# {userConfig.GetText("add_info")}: {addInfoFilterSummary}");
            }
            if (!string.IsNullOrWhiteSpace(Query.RawFilter))
            {
                report.AppendLine($"# {userConfig.GetText("other_filters")}: {Query.RawFilter}");
            }
            if (displayedOwnerData.Count == 0)
            {
                report.AppendLine($"# {userConfig.GetText("no_recertifiable_owners_assigned")}");
                return report.ToString();
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
                AppendOwnerTableCsv(ref report, GetMergedHeadline(), GetMergedOwners(overdueOwners, upcomingOwners, furtherOwners, inactiveOwners), true, ownerAddInfoFilter);
            }
            else
            {
                AppendOwnerTableCsv(ref report, GetOverdueHeadline(), overdueOwners, true, ownerAddInfoFilter);
                if (ReportData.RecertificationDisplayPeriod > 0)
                {
                    AppendOwnerTableCsv(ref report, GetUpcomingHeadline(), upcomingOwners, true, ownerAddInfoFilter);
                }
                if (furtherOwners.Count > 0)
                {
                    AppendOwnerTableCsv(ref report, GetFurtherHeadline(furtherOwners), furtherOwners, true, ownerAddInfoFilter);
                }
                if (inactiveOwners.Count > 0)
                {
                    AppendOwnerTableCsv(ref report, GetInactiveHeadline(), inactiveOwners, false, ownerAddInfoFilter);
                }
            }

            return report.ToString();
        }

        public override string ExportToHtml()
        {
            (AddInfoFilter ownerAddInfoFilter, List<OwnerConnectionReport> displayedOwnerData, List<FwoOwner> overdueOwners, List<FwoOwner> upcomingOwners,
                List<FwoOwner> furtherOwners, List<FwoOwner> inactiveOwners) = PrepareOwnerRecertCollections();

            StringBuilder report = new();
            string addInfoFilterSummary = BuildOwnerAddInfoFilterSummary(ownerAddInfoFilter);
            if (displayedOwnerData.Count == 0)
            {
                report.AppendLine(userConfig.GetText("no_recertifiable_owners_assigned"));
                return GenerateHtmlFrameBase(userConfig.GetText(ReportType.ToString()), addInfoFilterSummary, DateTime.Now, report, new HtmlFrameOptions
                {
                    OtherFilter = Query.RawFilter
                });
            }

            AppendOwnerRecertStatisticsHtml(ref report, overdueOwners, upcomingOwners, furtherOwners, inactiveOwners);
            report.AppendLine("<hr>");
            AppendOwnerRecertTablesHtml(ref report, overdueOwners, upcomingOwners, furtherOwners, inactiveOwners, ownerAddInfoFilter);

            return GenerateHtmlFrameBase(userConfig.GetText(ReportType.ToString()), addInfoFilterSummary, DateTime.Now, report, new HtmlFrameOptions
            {
                OtherFilter = Query.RawFilter,
                FilterTextKey = "add_info"
            });
        }

        private (AddInfoFilter OwnerAddInfoFilter, List<OwnerConnectionReport> DisplayedOwnerData, List<FwoOwner> OverdueOwners, List<FwoOwner> UpcomingOwners,
            List<FwoOwner> FurtherOwners, List<FwoOwner> InactiveOwners) PrepareOwnerRecertCollections()
        {
            AddInfoFilter ownerAddInfoFilter = GetEffectiveOwnerAddInfoFilter();
            List<OwnerConnectionReport> displayedOwnerData = GetDisplayedOwnerData(ownerAddInfoFilter);
            List<FwoOwner> overdueOwners = [.. displayedOwnerData.Select(o => o.Owner).Where(ow => ow.RecertOverdue)];
            List<FwoOwner> upcomingOwners = [.. displayedOwnerData.Select(o => o.Owner).Where(ow => ow.RecertUpcoming)];
            List<FwoOwner> furtherOwners = [.. displayedOwnerData.Select(o => o.Owner).Where(ow => ow.RecertActive && !ow.RecertOverdue && !ow.RecertUpcoming)];
            List<FwoOwner> inactiveOwners = [.. displayedOwnerData.Select(o => o.Owner).Where(ow => !ow.RecertActive).OrderBy(ow => ow.Id)];
            return (ownerAddInfoFilter, displayedOwnerData, overdueOwners, upcomingOwners, furtherOwners, inactiveOwners);
        }

        private void AppendOwnerRecertStatisticsHtml(ref StringBuilder report, List<FwoOwner> overdueOwners, List<FwoOwner> upcomingOwners,
            List<FwoOwner> furtherOwners, List<FwoOwner> inactiveOwners)
        {
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
        }

        private void AppendOwnerRecertTablesHtml(ref StringBuilder report, List<FwoOwner> overdueOwners, List<FwoOwner> upcomingOwners,
            List<FwoOwner> furtherOwners, List<FwoOwner> inactiveOwners, AddInfoFilter ownerAddInfoFilter)
        {
            if (ReportData.MergeOwnerRecertTables)
            {
                report.AppendLine(Headline(GetMergedHeadline(), 3));
                AppendOwnerTable(ref report, GetMergedOwners(overdueOwners, upcomingOwners, furtherOwners, inactiveOwners), true, ownerAddInfoFilter);
            }
            else
            {
                if (overdueOwners.Count > 0)
                {
                    report.AppendLine(Headline(GetOverdueHeadline(), 3));
                    AppendOwnerTable(ref report, overdueOwners, true, ownerAddInfoFilter);
                }
                else
                {
                    report.AppendLine(userConfig.GetText("U4004"));
                }
                report.AppendLine("<hr>");
                if (upcomingOwners.Count > 0)
                {
                    report.AppendLine(Headline(GetUpcomingHeadline(), 3));
                    AppendOwnerTable(ref report, upcomingOwners, true, ownerAddInfoFilter);
                }
                else if (ReportData.RecertificationDisplayPeriod > 0)
                {
                    report.AppendLine(userConfig.GetText("U4006").Replace(Placeholder.DAYS, ReportData.RecertificationDisplayPeriod.ToString()));
                }
                report.AppendLine("<hr>");
                if (furtherOwners.Count > 0)
                {
                    report.AppendLine(Headline(GetFurtherHeadline(furtherOwners), 3));
                    AppendOwnerTable(ref report, furtherOwners, true, ownerAddInfoFilter);
                }
                if (inactiveOwners.Count > 0)
                {
                    report.AppendLine("<hr>");
                    report.AppendLine(Headline(GetInactiveHeadline(), 3));
                    AppendOwnerTable(ref report, inactiveOwners, false, ownerAddInfoFilter);
                }
            }
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

        private void AppendOwnerTable(ref StringBuilder report, List<FwoOwner> owners, bool includeRecertData, AddInfoFilter ownerAddInfoFilter)
        {
            report.AppendLine("<table>");
            AppendOwnerDataHeadlineHtml(ref report, includeRecertData, ownerAddInfoFilter);
            foreach (var owner in owners)
            {
                AppendOwnerDataHtml(ref report, owner, includeRecertData, ownerAddInfoFilter);
            }
            report.AppendLine("</table>");
        }

        private void AppendOwnerTableCsv(ref StringBuilder report, string headline, List<FwoOwner> owners, bool includeRecertData, AddInfoFilter ownerAddInfoFilter)
        {
            report.AppendLine($"# {headline}");
            AppendOwnerDataHeadlineCsv(ref report, includeRecertData, ownerAddInfoFilter);
            foreach (var owner in owners)
            {
                AppendOwnerDataCsv(ref report, owner, includeRecertData, ownerAddInfoFilter);
            }
            report.AppendLine("#");
        }

        private void AppendOwnerDataHtml(ref StringBuilder report, FwoOwner owner, bool includeRecertData, AddInfoFilter ownerAddInfoFilter)
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
            if (HasOwnerAdditionalInfoColumn(ownerAddInfoFilter))
            {
                report.AppendLine($"<td>{FormatOwnerAdditionalInfoValueHtml(owner, ownerAddInfoFilter)}</td>");
            }
            report.AppendLine("</tr>");
        }

        private void AppendOwnerDataHeadlineCsv(ref StringBuilder report, bool includeRecertData, AddInfoFilter ownerAddInfoFilter)
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
            if (HasOwnerAdditionalInfoColumn(ownerAddInfoFilter))
            {
                report.Append(OutputCsv(GetOwnerAdditionalInfoHeadline(ownerAddInfoFilter)));
            }
            report.AppendLine();
        }

        private void AppendOwnerDataCsv(ref StringBuilder report, FwoOwner owner, bool includeRecertData, AddInfoFilter ownerAddInfoFilter)
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
            if (HasOwnerAdditionalInfoColumn(ownerAddInfoFilter))
            {
                report.Append(OutputCsv(GetOwnerAdditionalInfoValue(owner, ownerAddInfoFilter)));
            }
            report.AppendLine();
        }

        private void AppendOwnerDataHeadlineHtml(ref StringBuilder report, bool includeRecertData, AddInfoFilter ownerAddInfoFilter)
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
            if (HasOwnerAdditionalInfoColumn(ownerAddInfoFilter))
            {
                report.AppendLine($"<th>{FormatHtmlCell(GetOwnerAdditionalInfoHeadline(ownerAddInfoFilter))}</th>");
            }
            report.AppendLine("</tr>");
        }

        private static bool HasOwnerAdditionalInfoColumn(AddInfoFilter ownerAddInfoFilter)
        {
            return !string.IsNullOrWhiteSpace(ownerAddInfoFilter.Name)
                && ownerAddInfoFilter.Mode != AddInfoFilterMode.not_existing;
        }

        private List<OwnerConnectionReport> GetDisplayedOwnerData(AddInfoFilter ownerAddInfoFilter)
        {
            return [.. ReportData.OwnerData.Where(owner => OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner.Owner, ownerAddInfoFilter))];
        }

        private string BuildOwnerAddInfoFilterSummary(AddInfoFilter ownerAddInfoFilter)
        {
            if (string.IsNullOrWhiteSpace(ownerAddInfoFilter.Name))
            {
                return "";
            }

            if (ownerAddInfoFilter.Mode == AddInfoFilterMode.display_only)
            {
                return "";
            }

            if (ownerAddInfoFilter.Mode == AddInfoFilterMode.value)
            {
                return $"{ownerAddInfoFilter.Name}={ownerAddInfoFilter.Value}";
            }

            return $"{ownerAddInfoFilter.Name} ({userConfig.GetText(ownerAddInfoFilter.Mode.ToString())})";
        }

        private string GetOwnerAdditionalInfoHeadline(AddInfoFilter ownerAddInfoFilter)
        {
            return $"{userConfig.GetText("add_info")}: {ownerAddInfoFilter.Name}";
        }

        private static string GetOwnerAdditionalInfoValue(FwoOwner owner, AddInfoFilter ownerAddInfoFilter)
        {
            return OwnerRecertDisplay.FormatAdditionalInfoValue(owner, ownerAddInfoFilter.Name);
        }

        private static string FormatOwnerAdditionalInfoValueHtml(FwoOwner owner, AddInfoFilter ownerAddInfoFilter)
        {
            string value = GetOwnerAdditionalInfoValue(owner, ownerAddInfoFilter);
            return OwnerRecertDisplay.TryParseBooleanValue(value, out bool boolValue)
                ? boolValue.ShowAsHtmlWithoutBootstrap().ToString()
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
