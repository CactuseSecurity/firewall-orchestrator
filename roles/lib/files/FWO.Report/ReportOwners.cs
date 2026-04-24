using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using System.Net;
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
            foreach (OwnerResponsibleType type in ownerResponsibleTypes)
            {
                report.AppendLine($"<th>{Encode(type.Name)}</th>");
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
            report.AppendLine($"<td>{Encode(owner.ExtAppId)}</td>");
            report.AppendLine($"<td>{Encode(owner.Name)}</td>");
            report.AppendLine($"<td>{Encode(owner.Criticality)}</td>");
            report.AppendLine($"<td>{Encode(GetOwnerState(owner))}</td>");
            foreach (OwnerResponsibleType type in ownerResponsibleTypes)
            {
                report.AppendLine($"<td>{FormatHtmlCell(GetResponsibles(owner, type.Id))}</td>");
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
            foreach (OwnerResponsibleType type in ownerResponsibleTypes)
            {
                report.Append(OutputCsv(GetResponsibles(owner, type.Id, "; ")));
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

        private static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        private static string FormatHtmlCell(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : Encode(value)
                    .Replace("\r\n", "<br>")
                    .Replace("\n", "<br>")
                    .Replace("\r", "<br>");
        }

        private static string GetAdditionalInfo(FwoOwner owner, string separator = "\n")
        {
            return owner.AdditionalInfo == null
                ? ""
                : string.Join(separator, owner.AdditionalInfo.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}: {entry.Value}"));
        }

        private static string GetResponsibles(FwoOwner owner, int responsibleTypeId, string separator = "\n")
        {
            return string.Join(separator, owner.GetOwnerResponsiblesByType(responsibleTypeId)
                .Where(dn => !string.IsNullOrWhiteSpace(dn))
                .OrderBy(dn => dn)
                .Select(FormatResponsible));
        }

        private static string FormatResponsible(string dn)
        {
            DistName distName = new(dn);
            string display = !string.IsNullOrWhiteSpace(distName.UserName) ? distName.UserName : distName.Group;
            return string.IsNullOrWhiteSpace(display) ? dn : display;
        }
    }
}
