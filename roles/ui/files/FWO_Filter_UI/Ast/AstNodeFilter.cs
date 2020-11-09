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

        public override void Extract(ref DynGraphqlQuery query)
        {
            string localQuery = "";
            string paramName = "";
            List<string> fieldNames = new List<string>();
            string operation;

            switch (Name)
            {
                case TokenKind.Source:
                    fieldNames.Add("rule_src");
                    paramName = "src" + query.parameterCounter++;
                    break;
                case TokenKind.Destination:
                    fieldNames.Add("rule_dst");
                    paramName = "dst" + query.parameterCounter++;
                    break;
                case TokenKind.Service:
                    fieldNames.Add("rule_svc");
                    paramName = "svc" + query.parameterCounter++;
                    break;
                case TokenKind.Action:
                    fieldNames.Add("rule_action");
                    paramName = "action" + query.parameterCounter++;
                    break;
                case TokenKind.Value:
                    fieldNames.Add("rule_src");
                    fieldNames.Add("rule_dst");
                    fieldNames.Add("rule_svc");
                    fieldNames.Add("rule_action");
                    paramName = "fullTextFilter" + query.parameterCounter++;
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Filtername Token (and thought there is one) ###");
            }

            switch (Operator)
            {
                case TokenKind.EQ:
                    //localQuery += "{_ilike: $" + paramName + "}} ";
                    operation = "_ilike";
                    break;
                case TokenKind.NEQ:
                    operation = "_nilike";
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Operator Token (and thought there is one) ###");
            }
            if (fieldNames.Count > 1)  // full search across all fields
            {
                localQuery = "_or: [";
                List<string> searchParts = new List<string>();
                foreach (string field in fieldNames)
                {
                    searchParts.Add($"{{{field}: {{{operation}:${paramName}}}}} ");
                }
                localQuery += string.Join(", ", searchParts);
                localQuery += "]";
            }
            else { // just a single field to be searched
                localQuery = $" {fieldNames[0]}: {{{operation}:${paramName}}} ";
            }

            switch (operation)
            {
                case "_ilike":
                case "_nilike":
                    query.QueryVariables[paramName] = $"%{Value}%";
                    break;
                case "_eq":
                case "_neq":
                    query.QueryVariables[paramName] = Value;
                    break;
                default:
                    throw new Exception("### Parser Error: Expected operation  ###");
            }
            query.WhereQueryPart += localQuery;
            query.QueryParameters.Add("$" + paramName + ": String! ");  // todo: also need to take of ip addresses and svc ports and protocols
            return;
        }
    }
}
