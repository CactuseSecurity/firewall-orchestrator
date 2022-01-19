using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterString : AstNodeFilter
    {
        string semanticType;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, false, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
            semanticType = Value.Text;
        }

        public override void Extract(ref DynGraphqlQuery query)
        {
            switch (Name.Kind)
            {
                // "xy" and "FullText=xy" are the same filter
                case TokenKind.FullText:
                case TokenKind.Value:
                    ExtractFullTextFilter(query);
                    break;
                case TokenKind.Service:
                    ExtractServiceFilter(query);
                    break;
                case TokenKind.Protocol:
                    ExtractProtocolFilter(query);
                    break;
                case TokenKind.Action:
                    ExtractActionFilter(query);
                    break;
                case TokenKind.Management:
                    ExtractManagementFilter(query);
                    break;
                case TokenKind.Gateway:
                    ExtractGatewayFilter(query);
                    break;
                default:
                    break;
            }
        }

        private DynGraphqlQuery ExtractFullTextFilter(DynGraphqlQuery query)
        {
            string queryOperation = ExtractOperator();
            string QueryVarName = "fullTextFilter" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{semanticType}%";

            List<string> ruleFieldNames = new List<string>() { "rule_src", "rule_dst", "rule_svc", "rule_action" };  // TODO: add comment later
            List<string> searchParts = new List<string>();
            foreach (string field in ruleFieldNames)
                searchParts.Add($"{{{field}: {{{queryOperation}: ${QueryVarName} }} }} ");
            query.ruleWhereStatement += " _or: [";
            query.ruleWhereStatement += string.Join(", ", searchParts);
            query.ruleWhereStatement += "]";

            return query;
        }

        private DynGraphqlQuery ExtractGatewayFilter(DynGraphqlQuery query)
        {
            string queryOperation = ExtractOperator();
            string QueryVarName = "gwName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{semanticType}%";
            query.ruleWhereStatement += $"device: {{dev_name : {{{queryOperation}: ${QueryVarName} }} }}";
            // query.nwObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.svcObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.userObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";

            return query;
        }

        private DynGraphqlQuery ExtractManagementFilter(DynGraphqlQuery query)
        {
            string queryOperation = ExtractOperator();
            string QueryVarName;

            if (int.TryParse(Value.Text, out int mgm_id)) // dealing with mgm_id filter
            {
                QueryVarName = "mgmtId" + query.parameterCounter++;
                query.QueryParameters.Add($"${QueryVarName}: Int! ");
                query.QueryVariables[QueryVarName] = mgm_id;
                query.ruleWhereStatement += $"management: {{mgm_id : {{{queryOperation}: ${QueryVarName} }} }}";
                query.nwObjWhereStatement += $"management: {{mgm_id : {{{queryOperation}: ${QueryVarName} }} }}";
                query.svcObjWhereStatement += $"management: {{mgm_id : {{{queryOperation}: ${QueryVarName} }} }}";
                query.userObjWhereStatement += $"management: {{mgm_id : {{{queryOperation}: ${QueryVarName} }} }}";
            }
            else
            {
                QueryVarName = "mgmtName" + query.parameterCounter++;
                query.QueryParameters.Add($"${QueryVarName}: String! ");
                query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
                query.ruleWhereStatement += $"management: {{mgm_name : {{{queryOperation}: ${QueryVarName} }} }}";
                query.nwObjWhereStatement += $"management: {{mgm_name : {{{queryOperation}: ${QueryVarName} }} }}";
                query.svcObjWhereStatement += $"management: {{mgm_name : {{{queryOperation}: ${QueryVarName} }} }}";
                query.userObjWhereStatement += $"management: {{mgm_name : {{{queryOperation}: ${QueryVarName} }} }}";
            }
            return query;
        }

        private DynGraphqlQuery ExtractProtocolFilter(DynGraphqlQuery query)
        {
            string queryOperation = ExtractOperator();
            string QueryVarName = "proto" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{semanticType}%";
            query.ruleWhereStatement += $"rule_services: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {queryOperation}: ${QueryVarName} }} }} }} }}";
            return query;
        }

        private DynGraphqlQuery ExtractActionFilter(DynGraphqlQuery query)
        {
            string queryOperation = ExtractOperator();
            string QueryVarName = "action" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{semanticType}%";
            query.ruleWhereStatement += $"rule_action: {{ {queryOperation}: ${QueryVarName} }}";
            return query;
        }

        private DynGraphqlQuery ExtractServiceFilter(DynGraphqlQuery query)
        {
            string queryOperation = ExtractOperator();
            string QueryVarName = "svc" + query.parameterCounter++;
            query.QueryVariables[QueryVarName] = $"%{semanticType}%";
            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.ruleWhereStatement += $"rule_services: {{service: {{svcgrp_flats: {{serviceBySvcgrpFlatMemberId: {{svc_name: {{ {queryOperation}: ${QueryVarName} }} }} }} }} }}";
            return query;
        }
    }
}
