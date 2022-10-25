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

        public static DynGraphqlQuery Compile(string input, ReportType? reportType = null, DeviceFilter? deviceFilter = null, TimeFilter? timeFilter = null, bool detailed = false, bool filtering = false)
        {
            bool detailedCalc = detailed || reportType == ReportType.ResolvedRules;
            Log.WriteDebug("Filter", $"Input: \"{input}\", Report Type: \"${reportType}\", Device Filter: \"{deviceFilter}\"");
            return DynGraphqlQuery.GenerateQuery(input, CompileToAst(input), deviceFilter, timeFilter, reportType, detailedCalc);
        }
    }
}
