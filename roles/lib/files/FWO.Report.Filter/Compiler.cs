using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter.Ast;

namespace FWO.Report.Filter
{
    public static class Compiler
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

        public static DynGraphqlQuery Compile(ReportTemplate template)
        {
            Log.WriteDebug("Filter", $"Input: \"{template.Filter}\", Report Type: \"${template.ReportParams.ReportType}\", Device Filter: \"{template.ReportParams.DeviceFilter}\"");
            return DynGraphqlQuery.GenerateQuery(template, CompileToAst(template.Filter));
        }
    }
}
