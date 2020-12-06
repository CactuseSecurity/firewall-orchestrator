using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Report.Filter
{
    public class Token
    {
        public readonly Range Position;
        public readonly string Text;
        public readonly TokenKind Kind;

        public override string ToString()
        {
            return $"Text: \"{Text}\" Kind: \"{Kind}\"";
        }

        public Token(Range position, string text, TokenKind kind)
        {
            Position = position;
            Text = text;
            Kind = kind;
        }
    }
}
