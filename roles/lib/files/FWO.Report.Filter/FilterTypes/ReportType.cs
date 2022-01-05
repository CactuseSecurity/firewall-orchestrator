using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter
{
    public enum ReportType
    {
        None,
        Rules,
        Changes,
        Statistics,
        NatRules
    }

    public static class ReportTypeUtil
    {
        public static ReportType ToReportType(string reportType)
        {
            query.ReportType = Value.Text switch
            {
                "rules" or "rule" => ReportType.Rules,
                "statistics" or "statistic" => ReportType.Statistics,
                "changes" or "change" => ReportType.Changes,
                "natrules" or "nat_rules" => ReportType.NatRules,
                _ => throw new SemanticException($"Unexpected report type found", Value.Position)
            };
        }
    }
}
