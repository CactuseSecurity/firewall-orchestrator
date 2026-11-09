using FWO.Report.Filter.Exceptions;
using FWO.Basics;


namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterReportType : AstNodeFilter
    {
        ReportType semanticValue;

        /// <summary>
        /// Report type names (singular and plural or underscore spellings) accepted in the filter.
        /// </summary>
        private static readonly Dictionary<string, ReportType> kReportTypeNames = new()
        {
            { "rules", ReportType.Rules }, { "rule", ReportType.Rules },
            { "resolvedrules", ReportType.ResolvedRules }, { "resolvedrule", ReportType.ResolvedRules },
            { "resolvedrulestech", ReportType.ResolvedRulesTech }, { "resolvedruletech", ReportType.ResolvedRulesTech },
            { "unusedrules", ReportType.UnusedRules }, { "unusedrule", ReportType.UnusedRules },
            { "statistics", ReportType.Statistics }, { "statistic", ReportType.Statistics },
            { "changes", ReportType.Changes }, { "change", ReportType.Changes },
            { "resolvedchanges", ReportType.ResolvedChanges }, { "resolvedchange", ReportType.ResolvedChanges },
            { "resolvedchangestech", ReportType.ResolvedChangesTech }, { "resolvedchangetech", ReportType.ResolvedChangesTech },
            { "natrules", ReportType.NatRules }, { "nat_rules", ReportType.NatRules },
            { "natchanges", ReportType.NatChanges }, { "nat_changes", ReportType.NatChanges },
            { "recertifications", ReportType.Recertification }, { "recertification", ReportType.Recertification },
            { "connections", ReportType.Connections }, { "connection", ReportType.Connections },
            { "apprules", ReportType.AppRules }, { "apprule", ReportType.AppRules },
            { "variances", ReportType.VarianceAnalysis }, { "variance", ReportType.VarianceAnalysis },
            { "ownerrecertifications", ReportType.OwnerRecertification }, { "ownerrecertification", ReportType.OwnerRecertification }
        };

        /// <summary>
        /// Converts the filter value into the report type.
        /// </summary>
        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, true, TokenKind.EQ, TokenKind.EEQ);
            if (!kReportTypeNames.TryGetValue(Value.Text, out semanticValue))
            {
                throw new SemanticException($"Unexpected report type found", Value.Position);
            }
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
