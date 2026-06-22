using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Ui.Display;
using System.Text;

namespace FWO.Report
{
    /// <summary>
    /// Report listing owners and their metadata.
    /// </summary>
    public class ReportOwners(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportOwnersBase(query, userConfig, reportType)
    {
        private Dictionary<int, string> ownerLifeCycleStates = [];
        private List<OwnerResponsibleType> ownerResponsibleTypes = [];
        private string OwnerLifeCycleStateIdHeader => $"{userConfig.GetText("owner_lc_state")} {userConfig.GetText("id")}";
        private string OwnerLifeCycleStateNameHeader => $"{userConfig.GetText("owner_lc_state")} {userConfig.GetText("name")}";
        private IEnumerable<FwoOwner> OrderedOwners => ReportData.OwnerData
            .Select(ownerReport => ownerReport.Owner)
            .OrderBy(owner => owner.ExtAppId ?? "")
            .ThenBy(owner => owner.Name);

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(Query.FullQuery, Query.QueryVariables);
            List<OwnerLifeCycleState> lifeCycleStates = await apiConnection.SendQueryAsync<List<OwnerLifeCycleState>>(OwnerQueries.getOwnerLifeCycleStates);
            ownerResponsibleTypes = [.. (await apiConnection.SendQueryAsync<List<OwnerResponsibleType>>(OwnerQueries.getOwnerResponsibleTypes))
                .Where(type => type.Active)
                .OrderBy(type => type.SortOrder)
                .ThenBy(type => type.Name, StringComparer.OrdinalIgnoreCase)];
            ownerLifeCycleStates = lifeCycleStates.ToDictionary(state => state.Id, state => state.Display(userConfig.GetText("inactive")));
            ReportData reportData = new() { OwnerData = [.. owners.ConvertAll(owner => new OwnerConnectionReport() { Owner = owner, Name = owner.Display("") })] };
            await callback(reportData);
        }

        public override string ExportToCsv()
        {
            StringBuilder report = new();
            report.AppendLine($"# report type: {userConfig.GetText(ReportType.ToString())}");
            report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime():yyyy-MM-ddTHH:mm:ssK} (UTC)");
            if (!string.IsNullOrWhiteSpace(Query.RawFilter))
            {
                report.AppendLine($"# filter: {Query.RawFilter}");
            }
            AppendOwnerDataHeadlineCsv(ref report);
            foreach (FwoOwner owner in OrderedOwners)
            {
                AppendOwnerDataCsv(ref report, owner);
            }

            return report.ToString();
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new();
            report.AppendLine("<table>");
            AppendOwnerDataHeadlineHtml(ref report);
            foreach (FwoOwner owner in OrderedOwners)
            {
                AppendOwnerDataHtml(ref report, owner);
            }
            report.AppendLine("</table>");
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void AppendOwnerDataHeadlineHtml(ref StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("id")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("criticality")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("state")}</th>");
            report.AppendLine($"<th>{OwnerLifeCycleStateIdHeader}</th>");
            report.AppendLine($"<th>{OwnerLifeCycleStateNameHeader}</th>");
            foreach (OwnerResponsibleType type in ownerResponsibleTypes)
            {
                report.AppendLine($"<th>{EncodeHtml(type.Name)}</th>");
            }
            report.AppendLine($"<th>{userConfig.GetText("additional_info")}</th>");
            report.AppendLine("</tr>");
        }

        private void AppendOwnerDataHeadlineCsv(ref StringBuilder report)
        {
            report.Append(OutputCsv(userConfig.GetText("id")));
            report.Append(OutputCsv(userConfig.GetText("name")));
            report.Append(OutputCsv(userConfig.GetText("criticality")));
            report.Append(OutputCsv(userConfig.GetText("state")));
            report.Append(OutputCsv(OwnerLifeCycleStateIdHeader));
            report.Append(OutputCsv(OwnerLifeCycleStateNameHeader));
            foreach (OwnerResponsibleType type in ownerResponsibleTypes)
            {
                report.Append(OutputCsv(type.Name));
            }
            report.Append(OutputCsv(userConfig.GetText("additional_info")));
            report.AppendLine();
        }

        private void AppendOwnerDataHtml(ref StringBuilder report, FwoOwner owner)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{EncodeHtml(owner.ExtAppId)}</td>");
            report.AppendLine($"<td>{EncodeHtml(owner.Name)}</td>");
            report.AppendLine($"<td>{EncodeHtml(owner.Criticality)}</td>");
            report.AppendLine($"<td>{EncodeHtml(GetOwnerState(owner))}</td>");
            report.AppendLine($"<td>{EncodeHtml(owner.OwnerLifeCycleStateId?.ToString())}</td>");
            report.AppendLine($"<td>{EncodeHtml(GetOwnerStateName(owner))}</td>");
            foreach (OwnerResponsibleType type in ownerResponsibleTypes)
            {
                report.AppendLine($"<td>{FormatHtmlCell(OwnerRecertDisplay.FormatResponsibles(owner, type.Id, "\n"))}</td>");
            }
            report.AppendLine($"<td>{FormatHtmlCell(GetAdditionalInfo(owner))}</td>");
            report.AppendLine("</tr>");
        }

        private void AppendOwnerDataCsv(ref StringBuilder report, FwoOwner owner)
        {
            report.Append(OutputCsv(owner.ExtAppId));
            report.Append(OutputCsv(owner.Name));
            report.Append(OutputCsv(owner.Criticality));
            report.Append(OutputCsv(GetOwnerState(owner)));
            report.Append(OutputCsv(owner.OwnerLifeCycleStateId?.ToString()));
            report.Append(OutputCsv(GetOwnerStateName(owner)));
            foreach (OwnerResponsibleType type in ownerResponsibleTypes)
            {
                report.Append(OutputCsv(OwnerRecertDisplay.FormatResponsibles(owner, type.Id, "; ")));
            }
            report.Append(OutputCsv(GetAdditionalInfo(owner, "; ")));
            report.AppendLine();
        }

        private string GetOwnerState(FwoOwner owner)
        {
            if (owner.OwnerLifeCycleStateId.HasValue && ownerLifeCycleStates.TryGetValue(owner.OwnerLifeCycleStateId.Value, out string? state))
            {
                return state;
            }

            return "";
        }

        private string GetOwnerStateName(FwoOwner owner)
        {
            if (!string.IsNullOrWhiteSpace(owner.OwnerLifeCycleState?.Name))
            {
                return owner.OwnerLifeCycleState.Name;
            }
            return GetOwnerState(owner);
        }

        private static string GetAdditionalInfo(FwoOwner owner, string separator = "\n")
        {
            return owner.AdditionalInfo == null
                ? ""
                : string.Join(separator, owner.AdditionalInfo.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}: {entry.Value}"));
        }

    }
}
