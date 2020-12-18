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
                case TokenKind.Or: // or terms need to be enclosed in []
                    query.ruleWhereStatement += "_or: [{"; 
                    query.nwObjWhereStatement += "_or: [{";
                    query.svcObjWhereStatement += "_or: [{";
                    query.userObjWhereStatement += "_or: [{"; 
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
