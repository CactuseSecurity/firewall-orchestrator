using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;

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

        public AstNode? Parse()
        {
            AstNode? root = ParseStart();

            if (NextTokenExists())
            {
                throw new SyntaxException($"Unexpected token ({GetNextToken()}). Expected token: none.", tokens[position].Position); // Unexpected token
            }
            else
            {
                return root;
            }
        }

        private AstNode? ParseStart()
        {
            if (GetNextToken().Kind == TokenKind.Value)
            {
                return new AstNodeFilterString
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
            return GetNextToken().Kind switch
            {
                TokenKind.BL => ParseBracket(),
                _ => ParseFilter(),
            };
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
                TokenKind.Value or TokenKind.Owner or TokenKind.Service or TokenKind.Action or TokenKind.Management or TokenKind.Gateway or TokenKind.FullText or TokenKind.Protocol
                => new AstNodeFilterString() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Disabled or TokenKind.SourceNegated or TokenKind.DestinationNegated or TokenKind.ServiceNegated or TokenKind.Remove
                => new AstNodeFilterBool() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Time or TokenKind.LastHit 
                => new AstNodeFilterDateTimeRange() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.ReportType 
                => new AstNodeFilterReportType() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.DestinationPort or TokenKind.RecertDisplay or TokenKind.Unused
                => new AstNodeFilterInt() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Source or TokenKind.Destination
                => new AstNodeFilterNetwork() { Name = Name, Operator = Operator, Value = Value},

                _ => throw new NotSupportedException($"No type found for filter with token kind: {Name.Kind}"),
            };
        }

        private Token ParseOperator()
        {
            return CheckToken(TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ, TokenKind.LSS, TokenKind.GRT);
        }

        private Token ParseFilterName()
        {
            return CheckToken(
                TokenKind.LastHit, TokenKind.Owner, TokenKind.Destination, TokenKind.Source, TokenKind.Service, TokenKind.Protocol,
                TokenKind.DestinationPort, TokenKind.Action, TokenKind.FullText, TokenKind.Gateway,
                TokenKind.Management, TokenKind.Remove, TokenKind.RecertDisplay, TokenKind.Disabled, TokenKind.Unused);
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
