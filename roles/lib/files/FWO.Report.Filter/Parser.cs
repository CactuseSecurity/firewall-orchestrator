using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;

namespace FWO.Report.Filter
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
            AstNode root = ParseReportType();

            if (NextTokenExists())
            {
                throw new SyntaxException($"Unexpected token ({GetNextToken()}). Expected token: none.", tokens[position].Position); // Unexpected token
            }
            else
            {
                return root;
            }
        }

        private AstNode ParseReportType()
        {
            if (NextTokenExists() == false || GetNextToken().Kind != TokenKind.ReportType)
            {
                return new AstNodeConnector()
                {
                    Left = new AstNodeFilter<ReportType>()
                    {
                        Name = new Token(new Range(0, 0), "", TokenKind.ReportType),
                        Operator = new Token(new Range(0, 0), "", TokenKind.EQ),
                        Value = new Token(new Range(0, 0), "rules", TokenKind.Value)
                    },
                    Connector = new Token(new Range(0, 0), "", TokenKind.And),

                    Right = ParseTime()
                };
            }
            else
            {
                AstNodeConnector root = new AstNodeConnector
                {
                    Left = new AstNodeFilter<ReportType>()
                    {
                        Name = CheckToken(TokenKind.ReportType),
                        Operator = CheckToken(TokenKind.EQ),
                        Value = CheckToken(TokenKind.Value)
                    }
                };

                if (NextTokenExists())
                {
                    root.Connector = CheckToken(TokenKind.And);
                    root.Right = ParseTime();
                    return root;
                }

                else
                {
                    return root.Left;
                }
            }
        }

        private AstNode ParseTime()
        {
            if (NextTokenExists() == false || GetNextToken().Kind != TokenKind.Time)
            {
                AstNodeConnector root = new AstNodeConnector
                {
                    Left = new AstNodeFilter<DateTimeRange>()
                    {
                        Name = new Token(new Range(0, 0), "", TokenKind.Time),
                        Operator = new Token(new Range(0, 0), "", TokenKind.EQ),
                        Value = new Token(new Range(0, 0), "now", TokenKind.Value) //DateTime.Now.ToString()
                    }
                };

                if (NextTokenExists())
                {
                    root.Connector = new Token(new Range(0, 0), "", TokenKind.And);
                    root.Right = ParseStart();
                    return root;
                }
                else
                {
                    return root.Left;
                }
            }

            else // TokenKinde == Time
            {
                AstNodeConnector root = new AstNodeConnector
                {
                    Left = new AstNodeFilter<DateTimeRange>()
                    {
                        Name = CheckToken(TokenKind.Time),
                        Operator = ParseOperator(),
                        Value = CheckToken(TokenKind.Value)
                    }
                };

                if (NextTokenExists() && GetNextToken().Kind == TokenKind.And)
                {
                    root.Connector = CheckToken(TokenKind.And);
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
                return new AstNodeFilter<string>
                {
                    Name = new Token(new Range(0, 0), "", TokenKind.Value),
                    Operator = new Token(new Range(0, 0), "", TokenKind.EQ),
                    Value = CheckToken(TokenKind.Value)
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
            };

            while (NextTokenExists() && GetNextToken().Kind == TokenKind.Or)
            {
                rootNode.Connector = CheckToken(TokenKind.Or);
                rootNode.Right = ParseAnd();
                rootNode = new AstNodeConnector()
                {
                    Left = rootNode
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
            };

            while (NextTokenExists() && GetNextToken().Kind == TokenKind.And)
            {
                rootNode.Connector = CheckToken(TokenKind.And);
                rootNode.Right = ParseNot();
                rootNode = new AstNodeConnector()
                {
                    Left = rootNode,
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
                rootNode.Operator = CheckToken(TokenKind.Not);
                rootNode.Value = ParseNot();
                return rootNode;
            }

            return ParseAtom();
        }

        private AstNode ParseAtom()
        {
            switch (GetNextToken().Kind)
            {
                case TokenKind.BL:
                    return ParseBracket();
                default:
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
            Token Name = ParseFilterName();
            Token Operator = ParseOperator();
            Token Value = CheckToken(TokenKind.Value);
            return Name.Kind switch
            {            
                TokenKind.Value or TokenKind.Source or TokenKind.Destination or TokenKind.Service or TokenKind.Action or
                TokenKind.Management or TokenKind.Gateway or TokenKind.FullText or TokenKind.Protocol or TokenKind.RecertDisplay
                => new AstNodeFilter<string>() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Disabled or TokenKind.SourceNegated or TokenKind.DestinationNegated or TokenKind.ServiceNegated or TokenKind.Remove
                => new AstNodeFilter<bool>() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Time => new AstNodeFilter<DateTimeRange>() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.ReportType => new AstNodeFilter<ReportType>() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.DestinationPort => new AstNodeFilter<ushort>() { Name = Name, Operator = Operator, Value = Value },

                _ => throw new NotSupportedException($"No type found for filter with token kind: {Name.Kind}"),
            };
        }

        private Token ParseOperator()
        {
            return CheckToken(TokenKind.EQ, TokenKind.NEQ, TokenKind.LSS, TokenKind.GRT);
        }

        private Token ParseFilterName()
        {
            return CheckToken(
                TokenKind.Destination, TokenKind.Source, TokenKind.Service, TokenKind.Protocol,
                TokenKind.DestinationPort, TokenKind.Action, TokenKind.FullText, TokenKind.Gateway,
                TokenKind.Management, TokenKind.Remove, TokenKind.RecertDisplay);
        }

        private Token CheckToken(params TokenKind[] expectedTokenKinds)
        {
            Token tokenToCheck = GetNextToken();

            if (Array.IndexOf(expectedTokenKinds, tokenToCheck.Kind) != -1)
            {
                position++;
                return tokenToCheck;
            }

            throw new SyntaxException($"Unexpected token ({tokenToCheck}). Expected token of type: {string.Join(" / ", expectedTokenKinds)}.", tokenToCheck.Position); // Wrong token
        }

        private Token GetNextToken()
        {
            if (NextTokenExists())
            {
                return tokens[position];
            }
            else
            {
                throw new SyntaxException("No token but one was expected", tokens[^1].Position); // No token but one was expected
            }
        }

        private bool NextTokenExists()
        {
            return position < tokens.Count;
        }
    }
}
