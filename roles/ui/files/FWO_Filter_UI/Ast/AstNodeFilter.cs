using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter.Ast
{
    class AstNodeFilter : AstNode
    {
        public TokenKind Name { get; set; }
        public TokenKind Operator { get; set; }
        public string Value { get; set; }

        public override string Extract()
        {
            string Result = "";

            switch (Name)
            {
                case TokenKind.Source:
                    Result += "src_stub ";
                    break;
                case TokenKind.Destination:
                    Result += "dest_stub ";
                    break;
                case TokenKind.Value:
                    Result += "allsearch ";
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Filtername Token (and thought there is one) ###");
            }

            switch (Operator)
            {
                case TokenKind.EQ:
                    Result += "eq_stub ";
                    break;
                case TokenKind.NEQ:
                    Result += "neq_stub ";
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Operator Token (and thought there is one) ###");
            }

            Result += Value + "_stub ";

            return Result;
        }
    }
}
