﻿namespace FWO.Report.Filter.Ast
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

            List<string> ruleFieldNames = ["rule_src", "rule_dst", "rule_svc", "rule_action", "rule_name", "rule_comment", "rule_uid"];
            List<string> ruleSearchParts = [];
            foreach (string field in ruleFieldNames)
            {
                ruleSearchParts.Add($"{{{field}: {{{queryOperator}: ${queryVarName} }} }} ");
            }
            query.RuleWhereStatement += $"_or: [ {string.Join(", ", ruleSearchParts)} ]";

            List<string> connFieldNames = ["name", "reason" /*, "creator" */];
            List<string> nwobjFieldNames = ["name" /*, "creator" */];
            List<string> nwGroupFieldNames = ["id_string", "name", "comment" /*, "creator" */];
            List<string> svcFieldNames = ["name"];
            List<string> svcGroupFieldNames = ["name", "comment" /*, "creator" */];
            List<string> connSearchParts = [];
            foreach (string field in connFieldNames)
            {
                connSearchParts.Add($"{{{field}: {{{queryOperator}: ${queryVarName} }} }} ");
            }
            foreach (string field in nwobjFieldNames)
            {
                connSearchParts.Add($"{{ nwobject_connections: {{owner_network: {{{field}: {{{queryOperator}: ${queryVarName} }} }} }} }} ");
            }
            foreach (string field in nwGroupFieldNames)
            {
                connSearchParts.Add($"{{ nwgroup_connections: {{nwgroup: {{{field}: {{{queryOperator}: ${queryVarName} }} }} }} }} ");
            }
            foreach (string field in svcFieldNames)
            {
                connSearchParts.Add($"{{ service_connections: {{service: {{{field}: {{{queryOperator}: ${queryVarName} }} }} }} }} ");
            }
            foreach (string field in svcGroupFieldNames)
            {
                connSearchParts.Add($"{{ service_group_connections: {{service_group: {{{field}: {{{queryOperator}: ${queryVarName} }} }} }} }} ");
            }
            query.ConnectionWhereStatement += $"_or: [ {string.Join(", ", connSearchParts)} ]";
            return query;
        }

        private DynGraphqlQuery ExtractGatewayFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable(query, "gwName", Operator.Kind, semanticValue);
            query.RuleWhereStatement += $"device: {{dev_name : {{{ExtractOperator()}: ${queryVarName} }} }}";
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
            query.RuleWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            query.NwObjWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            query.SvcObjWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            query.UserObjWhereStatement += $"management: {{{queryFilterValue} : {{{queryOperation}: ${queryVarName} }} }}";
            return query;
        }

        private DynGraphqlQuery ExtractProtocolFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<string>(query, "proto", Operator.Kind, semanticValue!);
            query.RuleWhereStatement += $"rule_services: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }}";
            query.ConnectionWhereStatement += $"_or: [ {{ service_connections: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }} }}, " +
                $"{{ service_group_connections: {{service_group: {{ service_service_groups: {{ service: {{ stm_ip_proto: {{ip_proto_name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }} }} }} }} ]";
            return query;
        }

        private DynGraphqlQuery ExtractActionFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<string>(query, "action", Operator.Kind, semanticValue!);
            query.RuleWhereStatement += $"rule_action: {{ {ExtractOperator()}: ${queryVarName} }}";
            return query;
        }

        private DynGraphqlQuery ExtractServiceFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<string>(query, "svc", Operator.Kind, semanticValue!);
            query.RuleWhereStatement += $"rule_services: {{ service: {{ svcgrp_flats: {{ serviceBySvcgrpFlatMemberId: {{ svc_name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }} }} ";
            query.ConnectionWhereStatement += $"_or: [ {{ service_connections: {{ service: {{ name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }}, " +
                $"{{ service_group_connections: {{service_group: {{ _or: [ {{ name: {{ {ExtractOperator()}: ${queryVarName} }} }}, " +
                $"{{ service_service_groups: {{ service: {{ name: {{ {ExtractOperator()}: ${queryVarName} }} }} }} }} ] }} }} }} ]";
            return query;
        }
    }
}
