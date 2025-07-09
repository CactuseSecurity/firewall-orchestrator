using FWO.Basics;
using System.Text;
using FWO.Report.Filter;
using FWO.Ui.Display;
using FWO.Config.Api;
using FWO.Data;

namespace FWO.Report
{
    public class ReportCompliancePoc : ReportRules
    {
        public ReportCompliancePoc(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        public override string ExportToCsv()
        {
            return "";
        }
    }
}
