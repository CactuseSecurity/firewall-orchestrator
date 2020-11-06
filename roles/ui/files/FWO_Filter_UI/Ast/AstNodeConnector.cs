using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter.Ast
{
    public class AstNodeConnector : AstNode
    {
        public AstNode Right { get; set; }
        public AstNode Left { get; set; }
        public TokenKind ConnectorTyp { get; set; }

        public override string Extract(ref DynGraphqlQuery query)
        {
            throw new NotImplementedException();
        }
    }
}
