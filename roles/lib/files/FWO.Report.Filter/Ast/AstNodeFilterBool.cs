using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterBool : AstNodeFilter
    {
        bool semanticValue;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
            semanticValue = bool.Parse(Value.Text);
        }

        public override void Extract(ref DynGraphqlQuery query)
        {
            ConvertToSemanticType();

            switch (Name.Kind)
            {
                case TokenKind.Disabled:
                    //ExtractDisabledQuery(query);
                    throw new NotSupportedException("Token of type \"Disabled\" is currently not supported.");
                case TokenKind.SourceNegated:
                    // ExtractSourceNegatedQuery(query);
                    throw new NotSupportedException("Token of type \"SourceNegated\" is currently not supported.");
                case TokenKind.DestinationNegated:
                    // ExtractDestinationNegatedQuery(query);
                    throw new NotSupportedException("Token of type \"DestinationNegated\" is currently not supported.");
                case TokenKind.ServiceNegated:
                    // ExtractServiceNegatedQuery(query);
                    throw new NotSupportedException("Token of type \"ServiceNegated\" is currently not supported.");
                case TokenKind.Remove:
                    ExtractRemoveFilter(query);
                    break;
                default:
                    break;
            }
        }

        private DynGraphqlQuery ExtractRemoveFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<bool>(query, "remove", Operator.Kind, semanticValue);
            query.ruleWhereStatement += $"rule_metadatum: {{rule_to_be_removed: {{ {ExtractOperator()}: ${queryVarName} }}}}";
            return query;
        }
    }
}
