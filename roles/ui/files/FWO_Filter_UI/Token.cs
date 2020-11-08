using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
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

        public Token(int position, string text, TokenKind kind)
        {
            Position = position..^(position + text.Length-1);
            Text = text;
            Kind = kind;
        }
    }
}
