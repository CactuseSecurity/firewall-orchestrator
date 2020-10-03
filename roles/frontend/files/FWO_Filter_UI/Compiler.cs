using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
{
    public class Compiler
    {
        public static void Compile(string Input)
        {
            Parser parser = new Parser(Scanner.Scan(Input));
            parser.Parse();
        }
    }
}
