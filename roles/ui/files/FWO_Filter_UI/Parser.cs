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
            AstNode root = ParseTime();

            if (NextTokenExists() == true)
            {
                throw new SyntaxException($"Unexpected token ({GetNextToken()}). Expected token: none.", tokens[position].Position); // Unexpected token
            }

            else
            {
                return root;
            }
        }

        private AstNode ParseTime()
        {
            if (NextTokenExists() == false)
            {
                return new AstNodeFilter()
                {
                    Name = TokenKind.Time,
                    Operator = TokenKind.EQ,
                    Value = "true" //DateTime.Now.ToString()
                };
            }

            else if (GetNextToken().Kind != TokenKind.Time)
            {
                return new AstNodeConnector
                {
                    Left = new AstNodeFilter()
                    {
                        Name = TokenKind.Time,
                        Operator = TokenKind.EQ,
                        Value = "true" //DateTime.Now.ToString()
                    },

                    ConnectorType = TokenKind.And,

                    Right = ParseStart()
                };
            }

            else
            {
                AstNodeConnector root = new AstNodeConnector
                {
                    Left = new AstNodeFilter()
                    {
                        Name = CheckToken(TokenKind.Time).Kind,
                        Operator = CheckToken(TokenKind.EQ).Kind,
                        Value = ParseValue()
                    }
                };

                if (NextTokenExists() && GetNextToken().Kind == TokenKind.And)
                {
                    root.ConnectorType = CheckToken(TokenKind.And).Kind;
                    root.Right = ParseStart();
                    return root;
                }

                else
                {
                    return root.Left;
                }
            }
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
                ConnectorType = TokenKind.Or
            };

            while (NextTokenExists() && GetNextToken().Kind == TokenKind.Or)
            {
                CheckToken(TokenKind.Or);
                rootNode.Right = ParseAnd();
                rootNode = new AstNodeConnector()
                {
                    Left = rootNode,
                    ConnectorType = TokenKind.Or
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
                ConnectorType = TokenKind.And
            };

            while (NextTokenExists() && GetNextToken().Kind == TokenKind.And)
            {
                CheckToken(TokenKind.And);
                rootNode.Right = ParseNot();
                rootNode = new AstNodeConnector()
                {
                    Left = rootNode,
                    ConnectorType = TokenKind.And
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
            AstNodeUnary rootNode = new AstNodeUnary();

            if (GetNextToken().Kind == TokenKind.Not)
            {
                CheckToken(TokenKind.Not);
                rootNode.Kind = TokenKind.Not;
                rootNode.Value = ParseNot();
                return rootNode;
            }

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
            return new AstNodeFilter
            {
                Name = ParseFilterName(),
                Operator = ParseOperator(),
                Value = ParseValue()
            };
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
            return CheckToken(TokenKind.Destination, TokenKind.Source, TokenKind.Service, TokenKind.Action).Kind;
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
                throw new SyntaxException("No token but one was expected", tokens[tokens.Count - 1].Position); // No token but one was expected
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
