using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter.Ast
{
    class AstNodeUnary : AstNode
    {
        public TokenKind Kind { get; set; }

        public AstNode Value { get; set; }

        public override void Extract(ref DynGraphqlQuery query)
        {

            switch (Kind)
            {
                case TokenKind.Not:
                    query.RuleWhereQuery += "_not: {";
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Filtername Token (and thought there is one) ###");
            }
            Value.Extract(ref query);
            query.RuleWhereQuery += "}";
            return;
        }
    }
}
