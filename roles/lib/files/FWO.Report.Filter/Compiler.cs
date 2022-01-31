using FWO.Report.Filter.Ast;
using FWO.Api.Data;

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

        public static DynGraphqlQuery Compile(string input, ReportType? reportType = null, DeviceFilter? deviceFilter = null, TimeFilter? timeFilter = null, bool detailed = false)
        {
            return DynGraphqlQuery.GenerateQuery(input, CompileToAst(input), deviceFilter, timeFilter, reportType, detailed);
        }
    }
}
