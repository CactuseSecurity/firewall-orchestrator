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

        public Parser(List<Token> Tokens)
        {
            this.Tokens = Tokens;
        }

        public void Parse()
        {
            Start();
        }

        private void Start()
        {
            Bracket();        
        }

        private void Bracket()
        {
            if (GetNextToken().Kind == TokenKind.BL)
            {
                CheckToken(TokenKind.BL);
                Bracket();
                CheckToken(TokenKind.BR);
            }

            else
            {
                Expression();
            }

            while (Position < Tokens.Count && (GetNextToken().Kind == TokenKind.And || GetNextToken().Kind == TokenKind.Or))
            {
                Connector();
                Bracket();
            }
        }

        private void Connector()
        {
            CheckToken(TokenKind.And, TokenKind.Or);
        }

        private void Expression()
        {
            if (GetNextToken().Kind == TokenKind.Text)
            {
                CheckToken(TokenKind.Text);
            }

            else
            {
                Filter();
                Operator();
                Text();
            }
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
            Token TokenToCheck = GetNextToken();

            for (int i = 0; i < Matches.Length; i++)
            {
                if (TokenToCheck.Kind == Matches[i])
                {
                    Position++;
                    return;
                }                   
            }

            throw new SyntaxErrorException("Unexpected token " + TokenToCheck.ToString()); // Wrong token
        }

        private Token GetNextToken()
        {
            if (Position >= Tokens.Count)
            {
                throw new SyntaxErrorException("No token but one was expected"); // No token but one was expected
            }

            else
            {
                return Tokens[Position];
            }
        }
    }
}
