using FWO.Ui.Filter.Ast;
using System.Collections.Generic;
using System.Data;

namespace FWO.Ui.Filter
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
            return ParseStart();
        }

        private AstNode ParseStart()
        {
            return ParseOr();
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
            AstNode rootNode = ParseStart();
            CheckToken(TokenKind.BR);

            return rootNode;
        }

        private AstNode ParseFilter()
        {
            AstNodeFilter filterNode = new AstNodeFilter();

            if (GetNextToken().Kind == TokenKind.Value)
            {
                filterNode.Name = TokenKind.Value;
                filterNode.Operator = TokenKind.EQ;
                filterNode.Value = CheckToken(TokenKind.Value).Text;
            }

            else
            {
                filterNode.Name = ParseFilterName();
                filterNode.Operator = ParseOperator();
                filterNode.Value = ParseValue();
            }

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
                    Position++;
                    return TokenToCheck;
                }                   
            }

            throw new SyntaxErrorException($"Unexpected token ({TokenToCheck}). Expected tokens of type: {string.Join(", ", Matches)}."); // Wrong token
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

        private bool NextTokenExists()
        {
            if (Position >= Tokens.Count)
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
