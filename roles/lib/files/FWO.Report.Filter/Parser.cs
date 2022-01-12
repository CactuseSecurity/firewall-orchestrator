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
            if (NextTokenExists())
            {
                if (GetNextToken().Kind == TokenKind.Value)
                {
                    return new AstNodeFilter
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
            else
            {
                return null;
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
            return new AstNodeFilter
            {
                Name = ParseFilterName(),
                Operator = ParseOperator(),
                Value = CheckToken(TokenKind.Value)
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
