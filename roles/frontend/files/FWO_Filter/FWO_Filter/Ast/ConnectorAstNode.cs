using System;
using System.Collections.Generic;
using System.Text;

namespace FWO_Filter
{
    public class ConnectorAstNode : AstNode
    {
        public AstNode Right { get; set; }
        public AstNode Left { get; set; }
        public TokenKind ConnectorTyp { get; set; }
    }
}
