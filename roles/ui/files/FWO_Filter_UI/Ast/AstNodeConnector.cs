using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter.Ast
{
    public class AstNodeConnector : AstNode
    {
        public AstNode Right { get; set; }
        public AstNode Left { get; set; }
        public TokenKind ConnectorType { get; set; }

        public override void Extract(ref DynGraphqlQuery query)
        {
            switch (ConnectorType)
            {
                case TokenKind.And: // and is the default operator
                    break;
                case TokenKind.Or:
                    query.RuleWhereQuery += "_or: [{"; // or terms need to be enclosed in []
                    break;
                default:
                    throw new Exception("Expected Filtername Token (and thought there is one)");
            }

            Left.Extract(ref query);

            if (ConnectorType == TokenKind.Or)
                query.RuleWhereQuery += "}, {";

            Right.Extract(ref query);

            if (ConnectorType == TokenKind.Or)
                query.RuleWhereQuery += "}] ";
            return;
        }
    }
}
