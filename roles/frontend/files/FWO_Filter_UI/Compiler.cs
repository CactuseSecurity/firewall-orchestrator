using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
{
    public class Compiler
    {
        public static void Compile(string input)
        {
            Scanner scanner = new Scanner(input);
            List<Token> tokens = scanner.Scan();
            Parser parser = new Parser(tokens);
            parser.Parse();
        }
    }
}
