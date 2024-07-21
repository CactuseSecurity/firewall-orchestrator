using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter.Ast
{
    public class AstNodeConnector : AstNode
    {
        public AstNode? Right { get; set; }
        public AstNode? Left { get; set; }
        public Token? Connector { get; set; }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
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
                    query.RuleWhereStatement += "_and: [{"; 
                    query.NwObjWhereStatement += "_and: [{";
                    query.SvcObjWhereStatement += "_and: [{";
                    query.UserObjWhereStatement += "_and: [{";
                    query.ConnectionWhereStatement += "_and: [{"; 
                    break;
                case TokenKind.Or: // or terms need to be enclosed in []
                    query.RuleWhereStatement += "_or: [{"; 
                    query.NwObjWhereStatement += "_or: [{";
                    query.SvcObjWhereStatement += "_or: [{";
                    query.UserObjWhereStatement += "_or: [{";
                    query.ConnectionWhereStatement += "_or: [{"; 
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (prefix): \"{Connector}\". ###", Connector.Position);
            }

            Left.Extract(ref query, reportType);

            switch (Connector.Kind)
            {
                case TokenKind.And:
                case TokenKind.Or:
                    query.RuleWhereStatement += "}, {";
                    query.NwObjWhereStatement += "}, {";
                    query.SvcObjWhereStatement += "}, {";
                    query.UserObjWhereStatement += "}, {";
                    query.ConnectionWhereStatement += "}, {";
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (operator): \"{Connector}\". ###", Connector.Position);
            }

            Right.Extract(ref query, reportType);

            switch (Connector.Kind)
            {
                case TokenKind.And:
                case TokenKind.Or:
                    query.RuleWhereStatement += "}] ";
                    query.NwObjWhereStatement += "}] ";
                    query.SvcObjWhereStatement += "}] ";
                    query.UserObjWhereStatement += "}] ";
                    query.ConnectionWhereStatement += "}] ";
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (suffix): \"{Connector}\" ###", Connector.Position);
            }
        }
    }
}
