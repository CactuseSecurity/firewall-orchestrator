using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;

namespace FWO.Report
{
    public abstract class ReportOwnersBase : ReportBase
    {
        public List<OwnerReport> OwnersReport = new();

        public ReportOwnersBase(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {}

        public override string SetDescription()
        {
            return $"{OwnersReport.Count} {userConfig.GetText("owners")}";
        }
    }
}
