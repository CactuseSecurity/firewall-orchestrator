using FWO.Report.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

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

        public static DynGraphqlQuery Compile(string input, bool detailed = false)
        {
            return DynGraphqlQuery.Generate(input, CompileToAst(input), detailed);
        }
    }
}
