using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterDateTimeRange : AstNodeFilter
    {
        DateTimeRange semanticValue;

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
                    ExtractLastHitFilter(query, (ReportType)reportType);
                    break;
                default:
                    break;
            }
        }

        private DynGraphqlQuery ExtractLastHitFilter(DynGraphqlQuery query, ReportType reportType)
        {
            string queryVarName = AddVariable<DateTimeRange>(query, "lastHitLimit", Operator.Kind, semanticValue!);
            
            if (reportType.IsChangeReport())
            {
                if (Operator.Kind==TokenKind.LSS) // only show rules which have a hit before a certain date (including no hit rules)
                {
                    query.ruleWhereStatement += $@"
                        _or: [
                            {{ rule: {{ rule_metadatum: {{ rule_last_hit: {{{ExtractOperator()}: ${queryVarName} }} }} }} }}
                            {{ rule: {{ rule_metadatum: {{ rule_last_hit: {{_is_null: true }} }} }} }}
                        ]";
                }
                else // only show rules which have a hit after a certain date (leaving out no hit rules)
                {
                    query.ruleWhereStatement += $"rule: {{ rule_metadatum:{{ rule_last_hit: {{{ExtractOperator()}: ${queryVarName} }} }} }}";
                }
            }
            else
            {
                if (Operator.Kind==TokenKind.LSS) // only show rules which have a hit before a certain date (including no hit rules)
                {
                    query.ruleWhereStatement += $@"
                        _or: [
                            {{ rule_metadatum: {{ rule_last_hit: {{{ExtractOperator()}: ${queryVarName} }} }} }}
                            {{ rule_metadatum: {{ rule_last_hit: {{_is_null: true }} }} }}
                            ]";
                }
                else // only show rules which have a hit after a certain date (leaving out no hit rules)
                {
                    query.ruleWhereStatement += $"rule_metadatum: {{ rule_last_hit: {{{ExtractOperator()}: ${queryVarName} }} }}";
                }
            }
            return query;
        }

        //private DynGraphqlQuery ExtractTimeFilter(DynGraphqlQuery query)
        //{
        //    switch (query.ReportType)
        //    {
        //        case ReportType.Rules:
        //        case ReportType.Statistics:
        //        case ReportType.NatRules:
        //            switch (Operator.Kind)
        //            {
        //                case TokenKind.EQ:
        //                case TokenKind.EEQ:
        //                    query.ruleWhereStatement +=
        //                        $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
        //                        $"importControlByRuleLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
        //                    query.nwObjWhereStatement +=
        //                        $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
        //                        $"importControlByObjLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
        //                    query.svcObjWhereStatement +=
        //                        $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
        //                        $"importControlBySvcLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
        //                    query.userObjWhereStatement +=
        //                        $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
        //                        $"importControlByUserLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
        //                    query.ReportTime = Value.Text;
        //                    break;
        //                default:
        //                    throw new SemanticException($"Unexpected operator token. Expected equals token.", Operator.Position);
        //            }
        //            break;
        //        case ReportType.Changes:
        //            switch (Operator.Kind)
        //            {
        //                case TokenKind.EQ:
        //                case TokenKind.EEQ:
        //                case TokenKind.GRT:
        //                case TokenKind.LSS:
        //                    (string start, string stop) = ResolveTimeRange(Value.Text);
        //                    query.QueryVariables["start"] = start;
        //                    query.QueryVariables["stop"] = stop;
        //                    query.QueryParameters.Add("$start: timestamp! ");
        //                    query.QueryParameters.Add("$stop: timestamp! ");

        //                    query.ruleWhereStatement += $@"
        //                    _and: [
        //                        {{ import_control: {{ stop_time: {{ _gte: $start }} }} }}
        //                        {{ import_control: {{ stop_time: {{ _lte: $stop }} }} }}
        //                    ]
        //                    change_type_id: {{ _eq: 3 }}
        //                    security_relevant: {{ _eq: true }}";
        //                    break;
        //                default:
        //                    throw new SemanticException($"Unexpected operator token.", Operator.Position);
        //            }
        //            break;
        //        default:
        //            Log.WriteError("Filter", $"Unexpected report type found: {query.ReportType}");
        //            break;
        //    }
        //    // todo: deal with time ranges for changes report type
        //    return query;
        //}

        //private (string, string) ResolveTimeRange(string timeRange)
        //{
        //    string start;
        //    string stop;
        //    //string currentTime = (string)DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        //    string currentYear = (string)DateTime.Now.ToString("yyyy");
        //    string currentMonth = (string)DateTime.Now.ToString("MM");
        //    string currentDay = (string)DateTime.Now.ToString("dd");
        //    DateTime startOfCurrentMonth = new DateTime(Convert.ToInt16(currentYear), Convert.ToInt16(currentMonth), 1);
        //    DateTime startOfNextMonth = startOfCurrentMonth.AddMonths(1);
        //    DateTime startOfPrevMonth = startOfCurrentMonth.AddMonths(-1);

        //    switch (timeRange)
        //    {
        //        // todo: add today, yesterday, this week, last week
        //        case "last year":
        //            start = $"{(Convert.ToInt16(currentYear) - 1)}-01-01";
        //            stop = $"{Convert.ToInt16(currentYear)}-01-01";
        //            break;
        //        case "this year":
        //            start = $"{Convert.ToInt16(currentYear)}-01-01";
        //            stop = $"{Convert.ToInt16(currentYear) + 1}-01-01";
        //            break;
        //        case "this month":
        //            start = startOfCurrentMonth.ToString("yyyy-MM-dd");
        //            stop = startOfNextMonth.ToString("yyyy-MM-dd");
        //            break;
        //        case "last month":
        //            start = startOfPrevMonth.ToString("yyyy-MM-dd");
        //            stop = startOfCurrentMonth.ToString("yyyy-MM-dd");
        //            break;
        //        default:
        //            string[] times = timeRange.Split('/');
        //            if (times.Length == 2)
        //            {
        //                start = Convert.ToDateTime(times[0]).ToString("yyyy-MM-dd HH:mm:ss");
        //                if (times[1].Trim().Length < 11)
        //                {
        //                    times[1] += " 23:59:59";
        //                }
        //                stop = Convert.ToDateTime(times[1]).ToString("yyyy-MM-dd HH:mm:ss");
        //            }
        //            else
        //                throw new SyntaxException($"Error: wrong time range format.", Value.Position); // Unexpected token
        //            // we have some hard coded string positions here which we should get rid off
        //            // how can we access the tokens[position].Position information here?
        //            break;
        //    }
        //    return (start, stop);
        //}
    }
}
