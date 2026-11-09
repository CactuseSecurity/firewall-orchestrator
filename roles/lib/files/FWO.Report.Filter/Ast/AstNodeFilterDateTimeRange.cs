using FWO.Report.Filter.FilterTypes;
using FWO.Basics;


namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterDateTimeRange : AstNodeFilter
    {
        DateTimeRange? semanticValue;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ, TokenKind.LSS, TokenKind.GRT);
            semanticValue = new DateTimeRange(this);
        }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            ConvertToSemanticType();

            switch (Name.Kind)
            {
                case TokenKind.LastHit:
                    ExtractLastHitFilter(query);
                    break;
                default:
                    break;
            }
        }

        private void ExtractLastHitFilter(DynGraphqlQuery query)
        {
            string queryVarName = AddVariable<DateTimeRange>(query, "lastHitLimit", Operator.Kind, semanticValue!);

            if (Operator.Kind == TokenKind.LSS) // only show rules which have a hit before a certain date (including no hit rules)
            {
                query.RuleWhereStatement += $@"
                        _or: [
                                {{ rule_metadatum: {{ rule_last_hit: {{{ExtractOperator()}: ${queryVarName} }} }} }}
                                {{ rule_metadatum: {{ rule_last_hit: {{_is_null: true }} }} }}
                            ]";
            }
            else // only show rules which have a hit after a certain date (leaving out no hit rules)
            {
                query.RuleWhereStatement += $"rule_metadatum: {{ rule_last_hit: {{{ExtractOperator()}: ${queryVarName} }} }}";

            }
        }
    }
}

