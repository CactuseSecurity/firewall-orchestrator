using FWO.Basics;
using System.Text;
using FWO.Report.Filter;
using FWO.Ui.Display;
using FWO.Config.Api;
using FWO.Data;

namespace FWO.Report
{
    public class ReportCompliance //: ReportRules
    {
		// Todo: move deeper into ReportData
		public List<(ComplianceNetworkZone, ComplianceNetworkZone)> Results { get; set; } = [];

        //public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

		//public override string ExportToCsv()
		public string ExportToCsv()
		{
			return "";
		}
    }
}
