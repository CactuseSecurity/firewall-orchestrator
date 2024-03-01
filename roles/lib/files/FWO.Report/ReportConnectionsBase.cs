using FWO.Api.Data;
using FWO.Report.Filter;
using FWO.Config.Api;

namespace FWO.Report
{
    public abstract class ReportConnectionsBase : ReportBase
    {
        public List<ModellingConnection> Connections = new ();

        public ReportConnectionsBase(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {}

        public override string SetDescription()
        {
            return $"{Connections.Count} {userConfig.GetText("connections")}";
        }
    }
}
