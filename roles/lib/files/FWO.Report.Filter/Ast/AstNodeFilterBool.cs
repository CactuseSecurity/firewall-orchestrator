using FWO.Report.Filter.Exceptions;
using FWO.Basics;


namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterBool : AstNodeFilter
    {
        bool semanticValue;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
            try
            {
                semanticValue = bool.Parse(Value.Text);
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
                case TokenKind.Disabled:
                    ExtractDisabledQuery(query);
                    break;
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

        private void ExtractRemoveFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<bool>(query, "remove", Operator.Kind, semanticValue);
            query.RuleWhereStatement += $"rule_metadatum: {{rule_to_be_removed: {{ {ExtractOperator()}: ${queryVarName} }}}}";
        }

        private void ExtractDisabledQuery(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<bool>(query, "disabled", Operator.Kind, semanticValue);
            query.RuleWhereStatement += $"rule_disabled: {{ {ExtractOperator()}: ${queryVarName} }}";
        }
    }
}
