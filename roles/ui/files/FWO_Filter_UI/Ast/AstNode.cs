using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter.Ast
{
    public abstract class AstNode
    {
        public abstract void Extract(ref DynGraphqlQuery query);
    }
}
