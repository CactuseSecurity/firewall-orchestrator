using FWO.Report.Filter.Exceptions;
using FWO.Basics;


namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterReportType : AstNodeFilter
    {
        ReportType semanticValue;

        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ);
            semanticValue = Value.Text switch
            {
                "rules" or "rule" => ReportType.Rules,
                "resolvedrules" or "resolvedrule" => ReportType.ResolvedRules,
                "resolvedrulestech" or "resolvedruletech" => ReportType.ResolvedRulesTech,
                "unusedrules" or "unusedrule" => ReportType.UnusedRules,
                "statistics" or "statistic" => ReportType.Statistics,
                "changes" or "change" => ReportType.Changes,
                "resolvedchanges" or "resolvedchange" => ReportType.ResolvedChanges,
                "resolvedchangestech" or "resolvedchangetech" => ReportType.ResolvedChangesTech,
                "natrules" or "nat_rules" => ReportType.NatRules,
                "recertifications" or "recertification" => ReportType.Recertification,
                "connections" or "connection" => ReportType.Connections,
                "apprules" or "apprule" => ReportType.AppRules,
                "variances" or "variance" => ReportType.VarianceAnalysis,
                _ => throw new SemanticException($"Unexpected report type found", Value.Position)
            };
        }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            ConvertToSemanticType();

            switch (Name.Kind)
            {
                case TokenKind.ReportType:
                    ExtractReportTypeFilter(query);
                    break;
                default:
                    throw new NotSupportedException($"Found unexpected and unsupported filter token: \"{Name}\"");
            }
        }

        private void ExtractReportTypeFilter(DynGraphqlQuery query)
        {
            query.ReportType = semanticValue;

            if (query.ReportType == ReportType.Statistics)
            {
                query.RuleWhereStatement += @$"rule_head_text: {{_is_null: true}}";
            }
        }
    }
}
