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

            while (Position < Tokens.Count)
            {               
                Connector();
                Bracket();
            }        
        }

        private AstNode Bracket()
        {
            if (GetNextToken().Kind == TokenKind.BL)
            {
                CheckToken(TokenKind.BL);
                Expression();
                CheckToken(TokenKind.BR);
            }

            else
            {
                Expression();
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
            if (Position >= Tokens.Count)
            {
                throw new SyntaxErrorException(); // No token but one was expected
            }

            for (int i = 0; i < Matches.Length; i++)
            {
                if (Tokens[Position].Kind == Matches[i])
                {
                    Position++;
                    return true;
                }                   
            }

            throw new SyntaxErrorException(); // Wrong token
        }

        private Token GetNextToken()
        {
            if (Position >= Tokens.Count)
            {
                throw new SyntaxErrorException(); // No token but one was expected
            }

            else
            {
                return Tokens[Position];
            }
        }
    }
}
