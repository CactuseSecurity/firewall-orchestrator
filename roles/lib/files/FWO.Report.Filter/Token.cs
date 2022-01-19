using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Report.Filter
{
    public class Token
    {
        public readonly Range Position;
        public readonly string Text;
        public TokenKind Kind { get; private set; }

        public override string ToString()
        {
            return $"Position: \"{Position}\" Text: \"{Text}\" Kind: \"{Kind}\"";
        }

        public Token(Range position, string text, TokenKind kind)
        {
            Position = position;
            Text = text;
            Kind = kind;
        }

        public void EqualsIsExactEquals()
        {
            if (Kind == TokenKind.EQ)
            {
                Kind = TokenKind.EEQ;
            }
        }
    }
}
