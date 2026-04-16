using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter.Ast;
using FWO.Basics;

namespace FWO.Report.Filter
{
    public static class Compiler
    {
        public static AstNode? CompileToAst(string input)
        {
            Scanner scanner = new(input);
            List<Token> tokens = scanner.Scan();
            if (tokens.Count > 0)
            {
                Parser parser = new(tokens);
                return parser.Parse();
            }
            else return null;
        }

        public static DynGraphqlQuery Compile(ReportTemplate template)
        {
            ReportType reportType = (ReportType)template.ReportParams.ReportType;
            string deviceFilterLogPart = reportType.IsDeviceRelatedReport()
                ? $", Device Filter: \"{template.ReportParams.DeviceFilter}\""
                : "";
            Log.WriteDebug("Filter", $"Input: \"{template.Filter}\", Report Type: \"${template.ReportParams.ReportType}\"{deviceFilterLogPart}");
            return DynGraphqlQuery.GenerateQuery(template, CompileToAst(template.Filter));
        }
    }
}
