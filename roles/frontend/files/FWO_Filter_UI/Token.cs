using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
{
    public class Token
    {
        public int Position { get; set; }
        
        public string Text { get; set; }

        public TokenKind Kind { get; set; }

        public override string ToString()
        {
            return $"Text: \"{Text}\" Kind: \"{Kind}\"";
        }
    }
}
