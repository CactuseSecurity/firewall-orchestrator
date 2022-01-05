namespace FWO.Report.Filter.Ast
{
    class AstNodeUnary : AstNode
    {
        public Token Operator { get; set; } = new Token(new Range(), "", TokenKind.Not);

        public AstNode? Value { get; set; }

        public override void Extract(ref DynGraphqlQuery query)
        {
            switch (Operator.Kind)
            {
                case TokenKind.Not:
                    query.ruleWhereStatement += "_not: {";
                    break;
                default:
                    throw new NotSupportedException($"### Compiler Error: Found unexpected and unsupported unary token \"{Operator}\" ###");
            }
            Value?.Extract(ref query);
            query.ruleWhereStatement += "}";
        }
    }
}
