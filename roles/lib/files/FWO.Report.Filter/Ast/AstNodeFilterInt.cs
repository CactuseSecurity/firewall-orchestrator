using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterInt : AstNodeFilter
    {
        int semanticValue;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
            semanticValue = int.Parse(Value.Text);
        }

        public override void Extract(ref DynGraphqlQuery query)
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
                default:
                    break;
            }
        }

        private DynGraphqlQuery ExtractRecertDisplayFilter(DynGraphqlQuery query)
        {
            string QueryVarName = "refdate" + query.parameterCounter++;
            query.QueryParameters.Add($"${QueryVarName}: timestamp! ");
            string refDate = DateTime.Now.AddDays(-semanticValue).ToString("yyyy-MM-dd HH:mm:ss");
            query.QueryVariables[QueryVarName] = refDate;

            query.ruleWhereStatement += $@"
                _or: [
                        {{ rule_metadatum: {{ rule_last_certified: {{ _lte: ${QueryVarName} }} }} }}
                        {{ _and:[ 
                                    {{ rule_metadatum: {{ rule_last_certified: {{ _is_null: true }} }} }}
                                    {{ rule_metadatum: {{ rule_created: {{ _lte: ${QueryVarName} }} }} }}
                                ]
                        }}
                    ]";
            return query;
        }

        private DynGraphqlQuery ExtractDestinationPortFilter(DynGraphqlQuery query)
        {
            string QueryVarName = "dport" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: Int! ");
            query.QueryVariables[QueryVarName] = Value.Text;

            query.ruleWhereStatement +=
                " rule_services: { service: { svcgrp_flats: { service: { svc_port: {_lte" +
                ": $" + QueryVarName + "}, svc_port_end: {_gte: $" + QueryVarName + "} } } } }";
            return query;
        }
    }
}
