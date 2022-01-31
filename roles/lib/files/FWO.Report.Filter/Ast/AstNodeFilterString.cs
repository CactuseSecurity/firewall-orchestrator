using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterString : AstNodeFilter
    {
        string? semanticValue;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, false, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
            semanticValue = Value.Text;
        }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            ConvertToSemanticType();

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
            string queryVarName = AddVariable<string>(query, "fullTextFiler", Operator.Kind, semanticValue!);
            string queryOperator = ExtractOperator();

            List<string> ruleFieldNames = new List<string>() { "rule_src", "rule_dst", "rule_svc", "rule_action" };  // TODO: add comment later
            List<string> searchParts = new List<string>();
            foreach (string field in ruleFieldNames)
                searchParts.Add($"{{{field}: {{{queryOperator}: ${queryVarName} }} }} ");
            query.ruleWhereStatement += $"_or: [ {string.Join(", ", searchParts)} ]";
            return query;
        }

        private DynGraphqlQuery ExtractGatewayFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable(query, "gwName", Operator.Kind, semanticValue);
            query.ruleWhereStatement += $"device: {{dev_name : {{{ExtractOperator()}: ${queryVarName} }} }}";
            // query.nwObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.svcObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.userObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            return query;
        }

        private DynGraphqlQuery ExtractManagementFilter(DynGraphqlQuery query)
        {
            string queryOperation = ExtractOperator();
            string queryFilterValue;
            string queryVarName;

            if (int.TryParse(semanticValue!, out int mgm_id)) // dealing with mgm_id filter
            {
                queryVarName = AddVariable<int>(query, "mgmId", Operator.Kind, mgm_id);
                queryFilterValue = "mgm_id";
            }
            else
            {
                queryVarName = AddVariable<string>(query, "mgmName", Operator.Kind, semanticValue!);
                queryFilterValue = "mgm_name";
            }
            query.ruleWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            query.nwObjWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            query.svcObjWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            query.userObjWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            return query;
        }

        private DynGraphqlQuery ExtractProtocolFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<string>(query, "proto", Operator.Kind, semanticValue!);
            query.ruleWhereStatement += $"rule_services: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }}";
            return query;
        }

        private DynGraphqlQuery ExtractActionFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<string>(query, "action", Operator.Kind, semanticValue!);
            query.ruleWhereStatement += $"rule_action: {{ {ExtractOperator()}: ${queryVarName} }}";
            return query;
        }

        private DynGraphqlQuery ExtractServiceFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<string>(query, "svc", Operator.Kind, semanticValue!);
            query.ruleWhereStatement += $"rule_services: {{service: {{svcgrp_flats: {{serviceBySvcgrpFlatMemberId: {{svc_name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }} }}";
            return query;
        }
    }
}
