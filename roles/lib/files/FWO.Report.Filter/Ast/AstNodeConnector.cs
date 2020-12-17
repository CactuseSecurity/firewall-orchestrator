using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Report.Filter.Ast
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
                    query.ruleWhereStatement += "_or: [{"; // or terms need to be enclosed in []
                    query.nwObjWhereStatement += "_or: [{"; // or terms need to be enclosed in []
                    query.svcObjWhereStatement += "_or: [{"; // or terms need to be enclosed in []
                    query.userObjWhereStatement += "_or: [{"; // or terms need to be enclosed in []
                    break;
                default:
                    throw new Exception("Expected Filtername Token (and thought there is one)");
            }

            Left.Extract(ref query);

            if (ConnectorType == TokenKind.Or)
            {
                query.ruleWhereStatement += "}, {";
                query.nwObjWhereStatement += "}, {";
                query.svcObjWhereStatement += "}, {";
                query.userObjWhereStatement += "}, {";
            }

            Right.Extract(ref query);

            if (ConnectorType == TokenKind.Or)
            {
                query.ruleWhereStatement += "}] ";
                query.nwObjWhereStatement += "}] ";
                query.svcObjWhereStatement += "}] ";
                query.userObjWhereStatement += "}] ";
            }
            return;
        }
    }
}
