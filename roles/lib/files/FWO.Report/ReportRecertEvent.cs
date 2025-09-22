using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    public class ReportRecertEvent(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportRules(query, userConfig, reportType)
    {
        public override string ExportToHtml()
        {
            StringBuilder report = new();
            int chapterNumber = 0;
            RecertificateOwner recertOwner = new(query, userConfig, reportType);
            recertOwner.AppendOwnerData(ref report, ReportData.OwnerData, chapterNumber);

            ConstructHtmlReport(ref report, ReportData.ManagementData, chapterNumber);
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public static async Task<List<OwnerConnectionReport>> GetRecertification(long? reportId, ApiConnection apiConnection)
        {
            try
            {
                ReportFile reportFile = (await apiConnection.SendQueryAsync<List<ReportFile>>(ReportQueries.getGeneratedReport, new { report_id = reportId }))[0];
                if (reportFile.Json != null)
                {
                    return JsonSerializer.Deserialize<List<OwnerConnectionReport>>(reportFile.Json) ?? [];
                }
            }
            catch (Exception exception)
            {
                Log.WriteError("Report Recertification Event", "Fetch generated recertification failed", exception);
            }
            return [];
        }
    }
}
