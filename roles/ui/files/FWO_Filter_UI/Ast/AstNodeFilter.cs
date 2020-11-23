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
            int level;
            string operation = defineOperator();
            string paramName = getFieldsAndParamName(out ruleFieldNames, ref query.parameterCounter, out level);
            query.RuleWhereQuery += buildLocalQuery(ruleFieldNames, paramName, operation, level);
            if (paramName != "active") // no need for a parameter if we just want the current config
            {
                query.QueryParameters.Add(buildQueryParameter(paramName));
                query.QueryVariables[paramName] = buildQueryVariable(operation);
            }
            return;
        }
        private string buildQueryVariable(string operation)
        {
            string queryVariable = Value;
            if (operation == "_ilike" || operation == "_nilike")  /// in case of like operators, add leading and trailing % to the variables
                queryVariable = $"%{queryVariable}%";
            return queryVariable;
        }

        private string buildQueryParameter(string paramName)
        {
            if (Name == TokenKind.DestinationPort)
                return "$" + paramName + ": Int! ";
            else if (Name == TokenKind.Time)
                return "$" + paramName + ": timestamp "; // not mandatory because of active filtering
            else
                return "$" + paramName + ": String! ";
        }

        private string getFieldsAndParamName(out List<string> ruleFieldNames, ref int paramCounter, out int level)
        {
            string paramName = "";
            level = 1; // how many levels do we have (equals number of closing brackets)
            ruleFieldNames = new List<string>();
            switch (Name)
            {
                case TokenKind.Source:
                    // ruleFieldNames.Add("rule_src");
                    ruleFieldNames.Add("rule_froms: { object: { objgrp_flats: { objectByObjgrpFlatMemberId: { obj_name");
                    level = 5;
                    paramName = "src" + paramCounter++;
                    break;
                case TokenKind.Destination:
                    //ruleFieldNames.Add("rule_dst");
                    ruleFieldNames.Add("rule_tos: {object: {objgrp_flats: {objectByObjgrpFlatMemberId: {obj_name");
                    level = 5;
                    paramName = "dst" + paramCounter++;
                    break;
                case TokenKind.Service:
                    // ruleFieldNames.Add("rule_svc");
                    ruleFieldNames.Add("rule_services: {service: {svcgrp_flats: {serviceBySvcgrpFlatMemberId: {svc_name");
                    level = 5;
                    paramName = "svc" + paramCounter++;
                    break;
                case TokenKind.Protocol:
                    ruleFieldNames.Add("rule_services: {service: {stm_ip_proto: {ip_proto_name");
                    paramName = "proto" + paramCounter++;
                    level = 4;
                    break;
                case TokenKind.DestinationPort:
                    //  without searching into groups:
                    //     ruleFieldNames.Add("rule_services: {service: {svc_port:{_lte:");
                    //     level = 3;
                    ruleFieldNames.Add(" rule_services: { service: { svcgrp_flats: { service: { svc_port: {_lte");
                    level = 5;
                    ruleFieldNames.Add("svc_port_end:{_gte");
                    paramName = "dport" + paramCounter++;
                    break;
                case TokenKind.Action:
                    ruleFieldNames.Add("rule_action");
                    paramName = "action" + paramCounter++;
                    break;
                case TokenKind.Management:
                    ruleFieldNames.Add("management: {mgm_name");
                    paramName = "mgmName" + paramCounter++;
                    level = 2;
                    break;
                case TokenKind.Gateway:
                    ruleFieldNames.Add("device: {dev_name");
                    paramName = "gwName" + paramCounter++;
                    level = 2;
                    break;
                case TokenKind.FullText:
                case TokenKind.Value:   // in case of missing operation, assume full text search across the following fields
                    ruleFieldNames.Add("rule_src");
                    ruleFieldNames.Add("rule_dst");
                    ruleFieldNames.Add("rule_svc");
                    ruleFieldNames.Add("rule_action");
                    paramName = "fullTextFilter" + paramCounter++;
                    level = 2;
                    break;
                case TokenKind.Time:
                    if (Value == "true")    // filtering "now"
                    {
                        paramName = "active";
                        level = 0;
                    }
                    else
                    {
                        ruleFieldNames.Add("import_control: { stop_time: {_lte");
                        ruleFieldNames.Add("importControlByRuleLastSeen: { stop_time: {_gte");
                        // TODO: missing case: if reportTime > last import, take the rules from the last successul import (max)
                        paramName = "reportTime";
                        level = 2;
                    }
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Filtername Token (and thought there is one) ###");
            }
            return paramName;
        }
        private string buildLocalQuery(List<string> ruleFieldNames, string paramName, string operation, int level)
        {
            string localQuery = "";
            if (ruleFieldNames.Count > 1)  // more complicated query cases
            {
                if (Name == TokenKind.Time)
                {
                    localQuery = ruleFieldNames[0] + ": $" + paramName + "} }," + ruleFieldNames[1] + "$" + paramName;
                }
                else if (Name == TokenKind.DestinationPort)
                {
                    localQuery = ruleFieldNames[0] + ": $" + paramName + "}," + ruleFieldNames[1] + "$" + paramName;
                }
                else if (Name == TokenKind.Value || Name == TokenKind.FullText)
                {
                    localQuery = "_or: [";
                    List<string> searchParts = new List<string>();
                    foreach (string field in ruleFieldNames)
                        searchParts.Add($"{{{field}: {{{operation}:${paramName} }}}} ");
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
                    localQuery = " active: {_eq: true }";
                else if (Name == TokenKind.Protocol)
                    localQuery += $"{ruleFieldNames[0]}: {{ {operation}:${paramName} ";
                else
                    localQuery = $" {ruleFieldNames[0]}: {{{operation}: ${paramName} ";
            }

            // add closing brackets depending on query level
            if (Name != TokenKind.Value && Name != TokenKind.FullText)  // due to or statement, we do not need closing brackets in these cases
                for (int i = 0; i < level; ++i)
                    localQuery += "}";
            return localQuery;
        }

        private string defineOperator()
        {
            string operation = "";
            switch (Operator)
            {
                case TokenKind.EQ:
                    if (Name == TokenKind.Time || Name == TokenKind.DestinationPort)
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
