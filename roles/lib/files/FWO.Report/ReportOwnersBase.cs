using FWO.Report.Filter;
using FWO.Config.Api;
using FWO.Basics;
using System.Text.Json;
using System.Text;

namespace FWO.Report
{
    public abstract class ReportOwnersBase : ReportBase
    {
        protected ReportOwnersBase(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
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
            string? ownerFilter = ReportType == ReportType.OwnerRecertification ? null : string.Join("; ", ReportData.OwnerData.ConvertAll(o => o.Name));
            return GenerateHtmlFrameBase(title, filter, date, htmlReport, null, ownerFilter);
        }
    }
}
