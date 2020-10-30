using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter.Ast
{
    class AstNodeUnary : AstNode
    {
        public TokenKind Kind { get; set; }

        public AstNode Value { get; set; }

        public override string Extract()
        {
            throw new NotImplementedException();
        }
    }
}
