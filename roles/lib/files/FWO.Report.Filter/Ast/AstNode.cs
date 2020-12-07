using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Report.Filter.Ast
{
    public abstract class AstNode
    {
        public abstract void Extract(ref DynGraphqlQuery query);
    }
}
