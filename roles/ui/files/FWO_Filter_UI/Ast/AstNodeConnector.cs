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
            string operation;

            switch (ConnectorType)
            {
                case TokenKind.And:
                    operation = ""; // and is the default operator
                    break;
                case TokenKind.Or:
                    operation = "_or: [{"; // or terms need to be enclosed in []
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Filtername Token (and thought there is one) ###");
            }

            query.whereQueryPart += operation;

            Left.Extract(ref query);
            if (ConnectorType == TokenKind.Or)
                query.whereQueryPart += "}, {";
            Right.Extract(ref query);

            if (ConnectorType == TokenKind.Or)
                query.whereQueryPart += "}] ";
            return;
        }
    }
}
