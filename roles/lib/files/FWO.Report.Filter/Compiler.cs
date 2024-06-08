using FWO.Report.Filter.Ast;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Logging;

namespace FWO.Report.Filter
{
    public class Compiler
    {
        public static AstNode? CompileToAst(string input)
        {
            Scanner scanner = new(input);
            List<Token> tokens = scanner.Scan();
            if(tokens.Count > 0)
            {
                Parser parser = new(tokens);
                return parser.Parse();
            }
            else return null;
        }

        public static DynGraphqlQuery Compile(ReportTemplate filter)
        {
            Log.WriteDebug("Filter", $"Input: \"{filter.Filter}\", Report Type: \"${filter.ReportParams.ReportType}\", Device Filter: \"{filter.ReportParams.DeviceFilter}\"");
            return DynGraphqlQuery.GenerateQuery(filter, CompileToAst(filter.Filter));
        }
    }
}
