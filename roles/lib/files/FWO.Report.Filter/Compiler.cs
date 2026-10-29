using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using FWO.Basics;

namespace FWO.Report.Filter
{
    public static class Compiler
    {
        /// <summary>
        /// Maximum length of a report filter. Parsing and query generation grow with the filter, so longer
        /// filters are rejected with a syntax error.
        /// </summary>
        public const int kMaxFilterLength = 10000;

        /// <summary>
        /// Scans and parses a report filter into its syntax tree.
        /// </summary>
        /// <param name="input">The filter text.</param>
        /// <returns>The root of the syntax tree; null for a filter without tokens.</returns>
        /// <exception cref="SyntaxException">The filter is too long, too deeply nested or otherwise invalid.</exception>
        public static AstNode? CompileToAst(string input)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (input.Length > kMaxFilterLength)
            {
                throw new SyntaxException($"Filter is too long. At most {kMaxFilterLength} characters are allowed.", kMaxFilterLength..input.Length);
            }
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
