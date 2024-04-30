using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterInt : AstNodeFilter
    {
        int semanticValue;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
            try
            {
                semanticValue = int.Parse(Value.Text);
            }
            catch (Exception ex)
            {
                throw new SemanticException(ex.Message, Value.Position);
            }
        }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            ConvertToSemanticType();

            switch (Name.Kind)
            {
                case TokenKind.DestinationPort:
                    ExtractDestinationPortFilter(query);
                    break;
                case TokenKind.RecertDisplay:
                    ExtractRecertDisplayFilter(query);
                    break;
                case TokenKind.Owner:
                    ExtractOwnerFilter(query);
                    break;
                case TokenKind.Unused:
                    ExtractUnusedFilter(query);
                    break;
                default:
                    break;
            }
        }

        private DynGraphqlQuery ExtractRecertDisplayFilter(DynGraphqlQuery query)
        {
            // string queryVarName = AddVariable<DateTime>(query, "refdate", Operator.Kind, DateTime.Now.AddDays(semanticValue));
            // query.ruleWhereStatement += $@"  rule_metadatum: {{ recertifications: {{ next_recert_date: {{ _lte: ${queryVarName} }} }} }}";
            return query;
        }

        private DynGraphqlQuery ExtractDestinationPortFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<int>(query, "dport", Operator.Kind, semanticValue);
            query.ruleWhereStatement += "rule_services: { service: { svcgrp_flats: { serviceBySvcgrpFlatMemberId: { svc_port: {_lte" +
                ": $" + queryVarName + "}, svc_port_end: {_gte: $" + queryVarName + " } } } } }";
            query.connectionWhereStatement += $"_or: [ {{ service_connections: {{service: {{ port: {{ _lte: ${queryVarName} }}, port_end: {{ _gte: ${queryVarName} }} }} }} }}, " +
                $"{{ service_group_connections: {{service_group: {{ service_service_groups: {{ service: {{ port: {{ _lte: ${queryVarName} }}, port_end: {{ _gte: ${queryVarName} }} }} }} }} }} }} ]";
            return query;
        }

        private DynGraphqlQuery ExtractOwnerFilter(DynGraphqlQuery query)
        {
            string QueryVarName = AddVariable<string>(query, "owner", Operator.Kind, Value.Text);
            query.ruleWhereStatement += $"owner: {{  {ExtractOperator()}: ${QueryVarName} }}";
            return query;
        }

        private DynGraphqlQuery ExtractUnusedFilter(DynGraphqlQuery query)
        {
            string QueryVarName = AddVariable<DateTime>(query, "cut", Operator.Kind, DateTime.Now.AddDays(-semanticValue));
            query.ruleWhereStatement += $@"rule_metadatum: {{_or: [
                    {{_and: [{{rule_last_hit: {{_is_null: false}} }}, {{rule_last_hit: {{_lte: ${QueryVarName} }} }} ] }},
                    {{ rule_last_hit: {{_is_null: true}} }} 
                ]}}";
            return query;
        }
    }
}
