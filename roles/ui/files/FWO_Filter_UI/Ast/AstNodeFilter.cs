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
            List<string> ruleFieldNames = new List<string>();
            string operation = defineOperator();
            string paramName = getFieldsAndParamName(ref ruleFieldNames, ref query.parameterCounter);
            query.RuleWhereQuery += buildLocalQuery(ruleFieldNames, paramName, operation);

            if (paramName != "active")
            {
                query.QueryVariables[paramName] = Value;
                if (operation == "_ilike" || operation == "_nilike")  /// in case of like operators, add leading and trailing % to the variables
                    query.QueryVariables[paramName] = $"%{query.QueryVariables[paramName]}%";
            }

            if (Name != TokenKind.Time)
                query.QueryParameters.Add("$" + paramName + ": String! ");  // todo: also need to take of ip addresses and svc ports and protocols
            else
            {
                if (paramName != "active")
                    query.QueryParameters.Add("$" + paramName + ": timestamp ");
            }
            return;
        }
        
        private string getFieldsAndParamName(ref List<string> ruleFieldNames, ref int paramCounter)
        {
            string paramName = "";

            switch (Name)
            {
                case TokenKind.Source:
                    ruleFieldNames.Add("rule_src");
                    paramName = "src" + paramCounter++;
                    break;
                case TokenKind.Destination:
                    ruleFieldNames.Add("rule_dst");
                    paramName = "dst" + paramCounter++;
                    break;
                case TokenKind.Service:
                    ruleFieldNames.Add("rule_svc");
                    paramName = "svc" + paramCounter++;
                    break;
                case TokenKind.Action:
                    ruleFieldNames.Add("rule_action");
                    paramName = "action" + paramCounter++;
                    break;
                case TokenKind.Management:
                    ruleFieldNames.Add("management: {mgm_name");
                    paramName = "mgmName" + paramCounter++;
                    break;
                case TokenKind.Gateway:
                    ruleFieldNames.Add("device: {dev_name");
                    paramName = "gwName" + paramCounter++;
                    break;
                case TokenKind.Value:   // in case of missing operation, assume full text search across the following fields
                    ruleFieldNames.Add("rule_src");
                    ruleFieldNames.Add("rule_dst");
                    ruleFieldNames.Add("rule_svc");
                    ruleFieldNames.Add("rule_action");
                    paramName = "fullTextFilter" + paramCounter++;
                    break;
                case TokenKind.Time:
                    if (Value == "true")    // filtering "now"
                    {
                        ruleFieldNames.Add("active");
                        paramName = "active";
                    }
                    else
                    {
                        ruleFieldNames.Add("import_control: { stop_time: {_lte: ");
                        ruleFieldNames.Add("importControlByRuleLastSeen: { stop_time: {_gt:");
                        paramName = "reportTime";
                    }
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Filtername Token (and thought there is one) ###");
            }
            return paramName;
        }
        private string buildLocalQuery(List<string> ruleFieldNames, string paramName, string operation)
        {
            string localQuery = "";
            if (ruleFieldNames.Count > 1)  // full search across all fields
            {
                if (Name == TokenKind.Time)
                {
                    localQuery = ruleFieldNames[0] + "$" + paramName + "} }," + ruleFieldNames[1] + "$" + paramName + " } }";
                }
                else if (Name == TokenKind.Value)
                {
                    localQuery = "_or: [";
                    List<string> searchParts = new List<string>();
                    foreach (string field in ruleFieldNames)
                    {
                        searchParts.Add($"{{{field}: {{{operation}:${paramName}}}}} ");
                    }
                    localQuery += string.Join(", ", searchParts);
                    localQuery += "]";
                }
                else
                {
                    throw new Exception("### Parser Error: found unexpected list of fieldnames  ###");
                }
            }
            else  // default case: just a single ruleField
            {
                if (Name == TokenKind.Time && paramName == "active")   // no real time parameter, settting search to "now"
                    localQuery = " active: {_eq: true } ";
                else
                {
                    localQuery = $" {ruleFieldNames[0]}: {{{operation}:${paramName}}} ";
                    if (Name == TokenKind.Management || Name == TokenKind.Gateway)
                        localQuery += "}";  // these queries go one level deeper, need to add an extra closing bracket
                }
            }
            return localQuery;
        }

        private string defineOperator()
        {
            string operation = "";
            switch (Operator)
            {
                case TokenKind.EQ:
                    if (Name == TokenKind.Time)
                        operation = "_eq";
                    else
                        operation = "_ilike";
                    break;
                case TokenKind.NEQ:
                    operation = "_nilike";
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Operator Token (and thought there is one) ###");
            }
            return operation;
        }
    }
}
