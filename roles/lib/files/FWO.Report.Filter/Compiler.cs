using FWO.Report.Filter.Ast;
using FWO.Api.Data;

namespace FWO.Report.Filter
{
    public class Compiler
    {
        public static AstNode CompileToAst(string input)
        {
            Scanner scanner = new Scanner(input);
            List<Token> tokens = scanner.Scan();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        public static DynGraphqlQuery Compile(string input, ReportType? reportType = null, DeviceFilter? deviceFilter = null, bool detailed = false)
        {
            return DynGraphqlQuery.GenerateQuery(input, CompileToAst(input), deviceFilter, reportType, detailed);
        }
    }
}
