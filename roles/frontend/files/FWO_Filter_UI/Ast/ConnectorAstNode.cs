using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
{
    public class ConnectorAstNode : AstNode
    {
        public AstNode Right { get; set; }
        public AstNode Left { get; set; }
        public TokenKind ConnectorTyp { get; set; }

        public override string Extract()
        {
            throw new NotImplementedException();
        }
    }
}
