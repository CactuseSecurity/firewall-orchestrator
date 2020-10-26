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

            while (GetNextToken().Kind == TokenKind.Or)
            {
                CheckToken(TokenKind.Or);
                rootNode.Right = ParseAnd();
                rootNode = new AstNodeConnector()
                {
                    Left = rootNode,
                    ConnectorTyp = TokenKind.Or
                };
            }

            return rootNode;
        }

        private AstNode ParseAnd()
        {
            AstNodeConnector rootNode = new AstNodeConnector() 
            { 
                Left = ParseNot(),
                ConnectorTyp = TokenKind.And
            };

            while (GetNextToken().Kind == TokenKind.And)
            {
                CheckToken(TokenKind.And);
                rootNode.Right = ParseNot();
                rootNode = new AstNodeConnector() 
                { 
                    Left = rootNode,
                    ConnectorTyp = TokenKind.And
                };
            }

            return rootNode;
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

            if (GetNextToken().Kind == TokenKind.Text)
            {
                filterNode.Name = TokenKind.Text;
                filterNode.Operator = TokenKind.EQ;
                filterNode.Value = CheckToken(TokenKind.Text).Text;
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
            return CheckToken(TokenKind.Text).Text;
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
