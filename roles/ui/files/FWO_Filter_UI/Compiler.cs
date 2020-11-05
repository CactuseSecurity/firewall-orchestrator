using FWO.Ui.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace FWO.Ui.Filter
{
    public class Compiler
    {
        public static (string query, MemoryStream queryVariables) Compile(string input)
        {
            Scanner scanner = new Scanner(input);
            List<Token> tokens = scanner.Scan();
            Parser parser = new Parser(tokens);
            AstNode root = parser.Parse();
            return QueryGenerator.ToGraphQl(root);
        }
    }
}
