using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter
{
    public class Parser
    {
        /// <summary>
        /// Maximum number of brackets and negations that may enclose a filter term. Each level costs stack
        /// frames while parsing and extracting, and a stack overflow cannot be caught, so deeper filters are
        /// rejected with a syntax error.
        /// </summary>
        public const int kMaxNestingDepth = 100;

        int position;
        int nestingDepth;
        readonly List<Token> tokens;

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
                EnterNesting(rootNode.Operator);
                rootNode.Value = ParseNot();
                LeaveNesting();
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
            Token openingBracket = CheckToken(TokenKind.BL);
            EnterNesting(openingBracket);
            AstNode rootNode = ParseOr();
            CheckToken(TokenKind.BR);
            LeaveNesting();

            return rootNode;
        }

        /// <summary>
        /// Opens one nesting level for a bracket or negation and rejects the filter when it gets too deep.
        /// </summary>
        /// <param name="nestingToken">The bracket or negation token that opens the level.</param>
        private void EnterNesting(Token nestingToken)
        {
            nestingDepth++;
            if (nestingDepth > kMaxNestingDepth)
            {
                throw new SyntaxException($"Filter is nested too deeply. At most {kMaxNestingDepth} brackets and negations may enclose a term.", nestingToken.Position);
            }
        }

        /// <summary>
        /// Closes the nesting level opened by the last call of <see cref="EnterNesting"/>.
        /// </summary>
        private void LeaveNesting()
        {
            nestingDepth--;
        }

        private AstNode ParseFilter()
        {
            Token Name = ParseFilterName();
            Token Operator = ParseOperator();
            Token Value = CheckToken(TokenKind.Value);
            return Name.Kind switch
            {
                TokenKind.Value or TokenKind.Service or TokenKind.Action or TokenKind.Management or TokenKind.Gateway or TokenKind.FullText or TokenKind.Protocol or TokenKind.Criticality or TokenKind.OwnerState
                => new AstNodeFilterString() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Disabled or TokenKind.SourceNegated or TokenKind.DestinationNegated or TokenKind.ServiceNegated or TokenKind.Remove
                => new AstNodeFilterBool() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Time or TokenKind.LastHit
                => new AstNodeFilterDateTimeRange() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.ReportType
                => new AstNodeFilterReportType() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.TaskType or TokenKind.Phase or TokenKind.States or TokenKind.ReferenceDate
                => new AstNodeFilterWorkflow() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.DestinationPort or TokenKind.RecertDisplay or TokenKind.Unused
                => new AstNodeFilterInt() { Name = Name, Operator = Operator, Value = Value },

                TokenKind.Source or TokenKind.Destination or TokenKind.SourceType or TokenKind.DestinationType
                => new AstNodeFilterNetwork() { Name = Name, Operator = Operator, Value = Value },

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
                TokenKind.LastHit, TokenKind.Destination, TokenKind.Source, TokenKind.DestinationType, TokenKind.SourceType, TokenKind.Service, TokenKind.Protocol,
                TokenKind.DestinationPort, TokenKind.Action, TokenKind.FullText, TokenKind.Gateway,
                TokenKind.Management, TokenKind.Remove, TokenKind.RecertDisplay, TokenKind.Disabled, TokenKind.Unused,
                TokenKind.ReportType, TokenKind.TaskType, TokenKind.Phase, TokenKind.States, TokenKind.OwnerState,
                TokenKind.Criticality, TokenKind.ReferenceDate);
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
