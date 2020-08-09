using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Filter
{
    class Compiler
    {
        public static void Compile(string Input)
        {
            Parser parser = new Parser(Scanner.Scan(Input));
            parser.Parse();
        }
    }
}
