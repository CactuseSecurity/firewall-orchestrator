using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text.Json;
using System.Text;

namespace FWO.Report
{
    public abstract class ReportOwnersBase : ReportBase
    {
        public ReportOwnersBase(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {}

        public override string ExportToJson()
        {
            return JsonSerializer.Serialize(ReportData.OwnerData, new JsonSerializerOptions { WriteIndented = true });
        }

        public override string ExportToCsv()
        {
            throw new NotImplementedException();
        }

        public override string SetDescription()
        {
            return $"{ReportData.OwnerData.Count} {userConfig.GetText("owners")}";
        }

        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport)
        {
            return GenerateHtmlFrame(title, filter, date, htmlReport, null, string.Join("; ", Array.ConvertAll(ReportData.OwnerData.ToArray(), o => o.Name)));
        }
    }
}
