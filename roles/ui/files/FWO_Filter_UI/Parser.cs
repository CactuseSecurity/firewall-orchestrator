using FWO.Ui.Filter.Ast;
using FWO.Ui.Filter.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;

namespace FWO.Ui.Filter
{
    public class Parser
    {
        int position;
        List<Token> tokens;

        public Parser(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        public AstNode Parse()
        {
            AstNode root = ParseStart();

            if (NextTokenExists() == true)
            {
                throw new SyntaxException($"Unexpected token ({GetNextToken()}). Expected token: none.", tokens[position].Position); // Wrong token
            }
            return root;
        }

        private AstNode ParseStart()
        {
            if (GetNextToken().Kind == TokenKind.Value)
            {
                return new AstNodeFilter
                {
                    Name = TokenKind.Value,
                    Operator = TokenKind.EQ,
                    Value = CheckToken(TokenKind.Value).Text
                };
            }

            else
            {
                return ParseOr();
            }
        }

        private AstNode ParseOr()
        {
            AstNodeConnector rootNode = new AstNodeConnector() 
            { 
                Left = ParseAnd(),
                ConnectorTyp = TokenKind.Or
            };

            while (NextTokenExists() && GetNextToken().Kind == TokenKind.Or)
            {
                CheckToken(TokenKind.Or);
                rootNode.Right = ParseAnd();
                rootNode = new AstNodeConnector()
                {
                    Left = rootNode,
                    ConnectorTyp = TokenKind.Or
                };
            }

            if (rootNode.Right != null)
            {
                return rootNode;
            }

            else
            {
                return rootNode.Left;
            }
        }

        private AstNode ParseAnd()
        {
            AstNodeConnector rootNode = new AstNodeConnector() 
            { 
                Left = ParseNot(),
                ConnectorTyp = TokenKind.And
            };

            while (NextTokenExists() && GetNextToken().Kind == TokenKind.And)
            {
                CheckToken(TokenKind.And);
                rootNode.Right = ParseNot();
                rootNode = new AstNodeConnector() 
                { 
                    Left = rootNode,
                    ConnectorTyp = TokenKind.And
                };
            }

            if (rootNode.Right != null)
            {
                return rootNode;
            }

            else
            {
                return rootNode.Left;
            }
        }

        private AstNode ParseNot()
        {
            //AstNodeUnary rootNode = new AstNodeUnary();

            //while (GetNextToken().Kind == TokenKind.Not)
            //{
            //    CheckToken(TokenKind.Not);
            //    rootNode.Kind = TokenKind.Not;                
            //    rootNode.Value = ParseNot();
            //}

            return ParseAtom();
        }

        private AstNode ParseAtom()
        {
            if (GetNextToken().Kind == TokenKind.BL)
            {
                return ParseBracket();
            }

            else
            {
                return ParseFilter();
            }
        }

        private AstNode ParseBracket()
        {
            CheckToken(TokenKind.BL);
            AstNode rootNode = ParseOr();
            CheckToken(TokenKind.BR);

            return rootNode;
        }

        private AstNode ParseFilter()
        {
            AstNodeFilter filterNode = new AstNodeFilter();

            filterNode.Name = ParseFilterName();
            filterNode.Operator = ParseOperator();
            filterNode.Value = ParseValue();

            return filterNode;
        }

        private TokenKind ParseOperator()
        {
            return CheckToken(TokenKind.EQ, TokenKind.NEQ).Kind;
        }

        private string ParseValue()
        {
            return CheckToken(TokenKind.Value).Text;
        }

        private TokenKind ParseFilterName()
        {
            return CheckToken(TokenKind.Destination, TokenKind.Source).Kind;
        }

        private Token CheckToken(params TokenKind[] Matches)
        {
            Token TokenToCheck = GetNextToken();

            for (int i = 0; i < Matches.Length; i++)
            {
                if (TokenToCheck.Kind == Matches[i])
                {
                    position++;
                    return TokenToCheck;
                }                   
            }

            throw new SyntaxException($"Unexpected token ({TokenToCheck}). Expected tokens of type: {string.Join(", ", Matches)}.", TokenToCheck.Position); // Wrong token
        }

        private Token GetNextToken()
        {
            if (position >= tokens.Count)
            {
                throw new SyntaxException("No token but one was expected", tokens[tokens.Count-1].Position); // No token but one was expected
            }

            else
            {
                return tokens[position];
            }
        }

        private bool NextTokenExists()
        {
            if (position >= tokens.Count)
            {
                return false;
            }

            else
            {
                return true;
            }
        }
    }
}
