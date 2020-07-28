using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace FWO_Filter
{
    public class Parser
    {
        int Position;
        List<Token> Tokens;

        public Parser(int Position, List<Token> Tokens)
        {
            this.Position = Position;
            this.Tokens = Tokens;
        }

        public void Parse()
        {
            Start();
        }

        private void Start()
        {
            Bracket();

            while (Position < Tokens.Count)
            {               
                Connector();
                Bracket();
            }        
        }

        private void Bracket()
        {
            Expression();

            // OR

            CheckToken(TokenKind.BL);

            Expression();

            CheckToken(TokenKind.BR);
        }

        private void Connector()
        {
            CheckToken(TokenKind.And, TokenKind.Or);
        }

        private void Expression()
        {
            Text();

            // OR

            Filter();
            Operator();
            Text();
        }

        private void Operator()
        {
            CheckToken(TokenKind.EQ, TokenKind.NEQ);
        }

        private void Text()
        {
            CheckToken(TokenKind.Text);
        }

        private void Filter()
        {
            CheckToken(TokenKind.Destination, TokenKind.Source);
        }

        private void CheckToken(params TokenKind[] Matches)
        {
            if (Position >= Tokens.Count)
            {
                throw new SyntaxErrorException(); // No token but one was expected
            }

            for (int i = 0; i < Matches.Length; i++)
            {
                if (Tokens[Position].Kind == Matches[i])
                {
                    Position++;
                    return;
                }                   
            }

            throw new SyntaxErrorException(); // Wrong token
        }
    }
}
