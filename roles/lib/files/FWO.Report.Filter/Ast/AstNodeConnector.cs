using FWO.Report.Filter.Exceptions;
using FWO.Basics;


namespace FWO.Report.Filter.Ast
{
    public class AstNodeConnector : AstNode
    {
        public AstNode? Right { get; set; }
        public AstNode? Left { get; set; }
        public Token? Connector { get; set; }
        private readonly string StartAnd = "_and: [{";
        private readonly string StartOr = "_or: [{";
        private readonly string Inbetween = "}, {";
        private readonly string CloseBrackets = "}] ";


        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            if (Connector == null)
            {
                throw new InvalidOperationException(nameof(Connector));
            }
            if (Left == null)
            {
                throw new InvalidOperationException(nameof(Left));
            }
            if (Right == null)
            {
                throw new InvalidOperationException(nameof(Right));
            }

            switch (Connector.Kind)
            {
                case TokenKind.And: // and terms should be enclosed in []
                    query.RuleWhereStatement += StartAnd;
                    query.NwObjWhereStatement += StartAnd;
                    query.SvcObjWhereStatement += StartAnd;
                    query.UserObjWhereStatement += StartAnd;
                    query.ConnectionWhereStatement += StartAnd;
                    break;
                case TokenKind.Or: // or terms need to be enclosed in []
                    query.RuleWhereStatement += StartOr;
                    query.NwObjWhereStatement += StartOr;
                    query.SvcObjWhereStatement += StartOr;
                    query.UserObjWhereStatement += StartOr;
                    query.ConnectionWhereStatement += StartOr;
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (prefix): \"{Connector}\". ###", Connector.Position);
            }

            Left.Extract(ref query, reportType);

            switch (Connector.Kind)
            {
                case TokenKind.And:
                case TokenKind.Or:
                    query.RuleWhereStatement += Inbetween;
                    query.NwObjWhereStatement += Inbetween;
                    query.SvcObjWhereStatement += Inbetween;
                    query.UserObjWhereStatement += Inbetween;
                    query.ConnectionWhereStatement += Inbetween;
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (operator): \"{Connector}\". ###", Connector.Position);
            }

            Right.Extract(ref query, reportType);

            switch (Connector.Kind)
            {
                case TokenKind.And:
                case TokenKind.Or:
                    query.RuleWhereStatement += CloseBrackets;
                    query.NwObjWhereStatement += CloseBrackets;
                    query.SvcObjWhereStatement += CloseBrackets;
                    query.UserObjWhereStatement += CloseBrackets;
                    query.ConnectionWhereStatement += CloseBrackets;
                    break;
                default:
                    throw new SemanticException($"### Compiler Error: Found unexpected and unsupported connector token (suffix): \"{Connector}\" ###", Connector.Position);
            }
        }
    }
}
