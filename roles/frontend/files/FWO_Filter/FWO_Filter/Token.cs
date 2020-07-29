using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Filter
{
    public class Token
    {
        public int Position { get; set; }
        
        public string Text { get; set; }

        public TokenKind Kind { get; set; }
    }
}
