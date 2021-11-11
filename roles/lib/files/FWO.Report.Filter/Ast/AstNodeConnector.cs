using FWO.Report.Filter.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Report.Filter.Ast
{
    public class AstNodeConnector : AstNode
    {
        public AstNode? Right { get; set; }
        public AstNode? Left { get; set; }
        public Token? Connector { get; set; }

        public override void Extract(ref DynGraphqlQuery query)
        {
            if (Connector == null)
                throw new ArgumentNullException(nameof(Connector));
            if (Left == null)
                throw new ArgumentNullException(nameof(Left));
            if (Right == null)
                throw new ArgumentNullException(nameof(Right));

            switch (Connector.Kind)
            {
                case TokenKind.And: // and terms should be enclosed in []
                    query.ruleWhereStatement += "_and: [{"; 
                    query.nwObjWhereStatement += "_and: [{";
                    query.svcObjWhereStatement += "_and: [{";
                    query.userObjWhereStatement += "_and: [{"; 
                    break;
                case TokenKind.Or: // or terms need to be enclosed in []
                    query.ruleWhereStatement += "_or: [{"; 
                    query.nwObjWhereStatement += "_or: [{";
                    query.svcObjWhereStatement += "_or: [{";
                    query.userObjWhereStatement += "_or: [{"; 
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (prefix): \"{Connector}\". ###", Connector.Position);
            }

            Left.Extract(ref query);

            switch (Connector.Kind)
            {
                case TokenKind.And:
                case TokenKind.Or:
                    query.ruleWhereStatement += "}, {";
                    query.nwObjWhereStatement += "}, {";
                    query.svcObjWhereStatement += "}, {";
                    query.userObjWhereStatement += "}, {";
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (operator): \"{Connector}\". ###", Connector.Position);
            }

            Right.Extract(ref query);

            switch (Connector.Kind)
            {
                case TokenKind.And:
                case TokenKind.Or:
                    query.ruleWhereStatement += "}] ";
                    query.nwObjWhereStatement += "}] ";
                    query.svcObjWhereStatement += "}] ";
                    query.userObjWhereStatement += "}] ";
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (suffix): \"{Connector}\" ###", Connector.Position);
            }
        }
    }
}
