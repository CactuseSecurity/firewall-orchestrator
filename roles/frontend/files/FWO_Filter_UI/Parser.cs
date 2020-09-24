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

        public AstNode Parse()
        {
            Start();
        }

        private AstNode Start()
        {
            Bracket();        
        }

        private AstNode Bracket()
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

        private AstNode Connector()
        {
            CheckToken(TokenKind.And, TokenKind.Or);
        }

        private AstNode Expression()
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

        private AstNode Operator()
        {
            CheckToken(TokenKind.EQ, TokenKind.NEQ);
        }

        private AstNode Text()
        {
            CheckToken(TokenKind.Text);
        }

        private AstNode Filter()
        {
            CheckToken(TokenKind.Destination, TokenKind.Source);
        }

        private bool CheckToken(params TokenKind[] Matches)
        {
            Token TokenToCheck = GetNextToken();

            for (int i = 0; i < Matches.Length; i++)
            {
                if (TokenToCheck.Kind == Matches[i])
                {
                    Position++;
                    return true;
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
