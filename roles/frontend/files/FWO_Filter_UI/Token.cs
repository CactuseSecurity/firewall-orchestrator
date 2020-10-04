using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
{
    public class Token
    {
        public readonly int Position;
        public readonly string Text;
        public readonly TokenKind Kind;

        public override string ToString()
        {
            return $"Text: \"{Text}\" Kind: \"{Kind}\"";
        }

        public Token(int position, string text, TokenKind kind)
        {
            Position = position;
            Text = text;
            Kind = kind;
        }
    }
}
