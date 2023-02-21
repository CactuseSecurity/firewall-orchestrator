using FWO.Report.Filter.Ast;
using FWO.Api.Data;
using FWO.Logging;

namespace FWO.Report.Filter
{
    public class Compiler
    {
        public static AstNode? CompileToAst(string input)
        {
            Scanner scanner = new Scanner(input);
            List<Token> tokens = scanner.Scan();
            if(tokens.Count > 0)
            {
                Parser parser = new Parser(tokens);
                return parser.Parse();
            }
            else return null;
        }

        public static DynGraphqlQuery Compile(ReportTemplate filter)
            // string input, Dictionary<string,string> recertificationFilter, ReportType? reportType = null, DeviceFilter? deviceFilter = null, TimeFilter? timeFilter = null, bool detailed = false)
        {
            // bool detailedCalc = filter.Detailed || filter.ReportParams.ReportType == (int) ReportType.ResolvedRules || filter.ReportParams.ReportType == (int) ReportType.ResolvedRulesTech;
            Log.WriteDebug("Filter", $"Input: \"{filter.Filter}\", Report Type: \"${filter.ReportParams.ReportType}\", Device Filter: \"{filter.ReportParams.DeviceFilter}\"");
            return DynGraphqlQuery.GenerateQuery(filter, CompileToAst(filter.Filter));
        }
    }
}
