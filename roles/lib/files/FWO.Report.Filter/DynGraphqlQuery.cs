using FWO.Report.Filter.Ast;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using System.Text.RegularExpressions;
using FWO.Logging;

namespace FWO.Report.Filter
{
    public class DynGraphqlQuery
    {
        public string RawFilter { get; private set; }

        public int parameterCounter = 0;
        public Dictionary<string, object> QueryVariables { get; set; } = new Dictionary<string, object>();
        public string FullQuery { get; set; } = "";
        public string ruleWhereStatement { get; set; } = "";
        public string nwObjWhereStatement { get; set; } = "";
        public string svcObjWhereStatement { get; set; } = "";
        public string userObjWhereStatement { get; set; } = "";
        public List<string> QueryParameters { get; set; } = new List<string>()
        {
            " $limit: Int ",
            " $offset: Int ",
            " $mgmId: [Int!]",
            " $relevantImportId: bigint"
        };
        public string ReportTimeString { get; set; } = "";
        public List<int> RelevantManagementIds { get; set; } = new List<int>();

        public ReportType ReportType { get; set; } = ReportType.Rules;

        // $mgmId and $relevantImporId are only needed for time based filtering
        private DynGraphqlQuery(string rawInput) { RawFilter = rawInput; }

        public static string fullTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public static string dateFormat = "yyyy-MM-dd";

        private static void SetDeviceFilter(ref DynGraphqlQuery query, DeviceFilter? deviceFilter)
        {
            bool first = true;
            if (deviceFilter != null)
            {
                query.RelevantManagementIds = deviceFilter.getSelectedManagements();
                query.ruleWhereStatement += "{_or: [{";
                foreach (ManagementSelect mgmt in deviceFilter.Managements)
                {
                    foreach (DeviceSelect dev in mgmt.Devices)
                    {
                        if (dev.Selected == true)
                        {
                            if (first == false)
                            {
                                query.ruleWhereStatement += "}, {";
                            }
                            query.ruleWhereStatement += $" device: {{dev_id: {{_eq:{dev.Id}}} }}";
                            first = false;
                        }
                    }
                }
                query.ruleWhereStatement += "}]}, ";
            }
        }

        private static void SetTimeFilter(ref DynGraphqlQuery query, TimeFilter? timeFilter, ReportType? reportType)
        {
            if (timeFilter != null)
            {
                query.ruleWhereStatement += "{";
                switch (reportType)
                {
                    case ReportType.Rules:
                    case ReportType.Statistics:
                    case ReportType.NatRules:
                        query.ruleWhereStatement +=
                            $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                            $"importControlByRuleLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                        query.nwObjWhereStatement +=
                            $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                            $"importControlByObjLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                        query.svcObjWhereStatement +=
                            $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                            $"importControlBySvcLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                        query.userObjWhereStatement +=
                            $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                            $"importControlByUserLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                        query.ReportTimeString = (timeFilter.IsShortcut ?
                                                  DateTime.Now.ToString(fullTimeFormat) :
                                                  timeFilter.ReportTime.ToString(fullTimeFormat));
                        break;
                    case ReportType.Changes:
                        (string start, string stop) = ResolveTimeRange(timeFilter);
                        query.QueryVariables["start"] = start;
                        query.QueryVariables["stop"] = stop;
                        query.QueryParameters.Add("$start: timestamp! ");
                        query.QueryParameters.Add("$stop: timestamp! ");

                        query.ruleWhereStatement += $@"
                        _and: [
                            {{ import_control: {{ stop_time: {{ _gte: $start }} }} }}
                            {{ import_control: {{ stop_time: {{ _lte: $stop }} }} }}
                        ]
                        change_type_id: {{ _eq: 3 }}
                        security_relevant: {{ _eq: true }}";
                        break;
                    default:
                        Log.WriteError("Filter", $"Unexpected report type found: {reportType}");
                        break;
                }
                query.ruleWhereStatement += "}, ";
            }
        }

        private static (string, string) ResolveTimeRange(TimeFilter timeFilter)
        {
            string start;
            string stop;
            DateTime startOfCurrentYear = new DateTime(DateTime.Now.Year, 1, 1);
            DateTime startOfCurrentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DateTime startOfCurrentWeek = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek);

            switch (timeFilter.TimeRangeType)
            {
                case TimeRangeType.Shortcut:
                    switch (timeFilter.TimeRangeShortcut)
                    {
                        case "this year":
                            start = startOfCurrentYear.ToString(dateFormat);
                            stop = startOfCurrentYear.AddYears(1).ToString(dateFormat);
                            break;
                        case "last year":
                            start = startOfCurrentYear.AddYears(-1).ToString(dateFormat);
                            stop = startOfCurrentYear.ToString(dateFormat);
                            break;
                        case "this month":
                            start = startOfCurrentMonth.ToString(dateFormat);
                            stop = startOfCurrentMonth.AddMonths(1).ToString(dateFormat);
                            break;
                        case "last month":
                            start = startOfCurrentMonth.AddMonths(-1).ToString(dateFormat);
                            stop = startOfCurrentMonth.ToString(dateFormat);
                            break;
                        case "this week":
                            start = startOfCurrentWeek.ToString(dateFormat);
                            stop = DateTime.Now.AddDays(1).ToString(dateFormat);
                            break;
                        case "last week":
                            start = startOfCurrentWeek.AddDays(-7).ToString(dateFormat);
                            stop = startOfCurrentWeek.ToString(dateFormat);
                            break;
                        case "today":
                            start = DateTime.Now.ToString(dateFormat);
                            stop = DateTime.Now.AddDays(1).ToString(dateFormat);
                            break;
                        case "yesterday":
                            start = DateTime.Now.AddDays(-1).ToString(dateFormat);
                            stop = DateTime.Now.ToString(dateFormat);
                            break;
                        default:
                            throw new Exception($"Error: wrong time range format:" + timeFilter.TimeRangeShortcut);
                    }
                    break;

                case TimeRangeType.Interval:
                    start = timeFilter.Interval switch
                    {
                        Interval.Days => DateTime.Now.AddDays(-timeFilter.Offset).ToString(fullTimeFormat),
                        Interval.Weeks => DateTime.Now.AddDays(-7 * timeFilter.Offset).ToString(fullTimeFormat),
                        Interval.Months => DateTime.Now.AddMonths(-timeFilter.Offset).ToString(fullTimeFormat),
                        Interval.Years => DateTime.Now.AddYears(-timeFilter.Offset).ToString(fullTimeFormat),
                        _ => throw new Exception($"Error: wrong time interval format:" + timeFilter.Interval.ToString()),
                    };
                    stop = DateTime.Now.ToString(fullTimeFormat);
                    break;

                case TimeRangeType.Fixeddates:
                    if (timeFilter.OpenStart)
                        start = DateTime.MinValue.ToString(fullTimeFormat);
                    else
                        start = timeFilter.StartTime.ToString(fullTimeFormat);
                    if (timeFilter.OpenEnd)
                        stop = DateTime.MaxValue.ToString(fullTimeFormat);
                    else
                        stop = timeFilter.EndTime.ToString(fullTimeFormat);
                    break;
                
                default:
                    throw new NotSupportedException($"Found unexpected TimeRangeType");
            }
            return (start, stop);
        }

        private static void SetFixedFilters(ref DynGraphqlQuery query, DeviceFilter? deviceFilter, TimeFilter? timeFilter, ReportType? reportType)
        {
             // leave out all header texts
            if (reportType != null && reportType == ReportType.Statistics)
            {
                query.ruleWhereStatement += "{rule_head_text: {_is_null: true}}, ";
            }

            SetDeviceFilter(ref query, deviceFilter);
            SetTimeFilter(ref query, timeFilter, reportType);
        }

        public static DynGraphqlQuery GenerateQuery(string rawInput, AstNode? ast, DeviceFilter? deviceFilter, TimeFilter? timeFilter, ReportType? reportType, bool detailed)
        {
            DynGraphqlQuery query = new DynGraphqlQuery(rawInput);

            query.ruleWhereStatement += "_and: [";

            SetFixedFilters(ref query, deviceFilter, timeFilter, reportType);

            query.ruleWhereStatement += "{";

            // now we convert the ast into a graphql query:
            if (ast != null)
                ast.Extract(ref query, reportType);

            query.ruleWhereStatement += "}] ";

            string paramString = string.Join(" ", query.QueryParameters.ToArray());
            
            switch (reportType)
            {
                // todo: move $mdmId filter from management into query.xxxWhereStatement
                // management(where: {{mgm_id: {{_in: $mgmId }} }} order_by: {{ mgm_name: asc }}) 
                        // management(order_by: {{ mgm_name: asc }}) 
                case ReportType.Statistics:
                    query.FullQuery = Queries.compact($@"
                    query statisticsReport ({paramString}) 
                    {{ 
                        management(
                            where: {{ 
                                hide_in_gui: {{_eq: false }}  
                                mgm_id: {{_in: $mgmId }} 
                                stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}} is_pure_routing_device: {{_eq: false}} }}
                            }}
                            order_by: {{ mgm_name: asc }}
                        ) 
                        {{
                            name: mgm_name
                            id: mgm_id
                            objects_aggregate(where: {{ {query.nwObjWhereStatement} }}) {{ aggregate {{ count }} }}
                            services_aggregate(where: {{ {query.svcObjWhereStatement} }}) {{ aggregate {{ count }} }}
                            usrs_aggregate(where: {{ {query.userObjWhereStatement} }}) {{ aggregate {{ count }} }}
                            rules_aggregate(where: {{ {query.ruleWhereStatement} }}) {{ aggregate {{ count }} }}
                            devices( where: {{ hide_in_gui: {{_eq: false }}, stm_dev_typ: {{is_pure_routing_device:{{_eq:false}} }} }} order_by: {{ dev_name: asc }} )
                            {{
                                name: dev_name
                                id: dev_id
                                rules_aggregate(where: {{ {query.ruleWhereStatement} }}) {{ aggregate {{ count }} }}
                            }}
                        }}
                    }}");
                    break;                

                case ReportType.Rules:
                    query.FullQuery = Queries.compact($@"
                    {(detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments)}

                    query rulesReport ({paramString}) 
                    {{ 
                        management( where: 
                            {{ 
                                mgm_id: {{_in: $mgmId }}, 
                                hide_in_gui: {{_eq: false }} 
                                stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}} is_pure_routing_device: {{_eq: false}} }}
                            }} order_by: {{ mgm_name: asc }} ) 
                            {{
                                id: mgm_id
                                name: mgm_name
                                devices ( where: {{ hide_in_gui: {{_eq: false }} }} order_by: {{ dev_name: asc }} ) 
                                    {{
                                        id: dev_id
                                        name: dev_name
                                        rules(
                                            limit: $limit 
                                            offset: $offset
                                            where: {{  access_rule: {{_eq: true}} {query.ruleWhereStatement} }} 
                                            order_by: {{ rule_num_numeric: asc }} )
                                            {{
                                                mgm_id: mgm_id
                                                ...{(detailed ? "ruleDetails" : "ruleOverview")}
                                            }} 
                                    }}
                            }} 
                    }}");
                    break;
                    
                case ReportType.Changes:
                    query.FullQuery = Queries.compact($@"
                    {(detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments)}

                    query changeReport({paramString}) {{
                        management(where: {{ hide_in_gui: {{_eq: false }} stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}} is_pure_routing_device: {{_eq: false}} }} }} order_by: {{mgm_name: asc}}) 
                        {{
                            id: mgm_id
                            name: mgm_name
                            devices (where: {{ hide_in_gui: {{_eq: false}} stm_dev_typ:{{is_pure_routing_device:{{_eq:false}} }} }}, order_by: {{dev_name: asc}} )                           
                            {{
                                id: dev_id
                                name: dev_name
                                changelog_rules(
                                    offset: $offset 
                                    limit: $limit 
                                    where: {{ 
                                        _or:[
                                                {{_and: [{{change_action:{{_eq:""I""}}}}, {{rule: {{access_rule:{{_eq:true}}}}}}]}}, 
                                                {{_and: [{{change_action:{{_eq:""D""}}}}, {{ruleByOldRuleId: {{access_rule:{{_eq:true}}}}}}]}},
                                                {{_and: [{{change_action:{{_eq:""C""}}}}, {{rule: {{access_rule:{{_eq:true}}}}}}, {{ruleByOldRuleId: {{access_rule:{{_eq:true}}}}}}]}}
                                            ]                                        
                                        {query.ruleWhereStatement} 
                                    }}
                                    order_by: {{ control_id: asc }}
                                ) 
                                    {{
                                        import: import_control {{ time: stop_time }}
                                        change_action
                                        old: ruleByOldRuleId {{
                                        mgm_id: mgm_id
                                        ...{(detailed ? "ruleDetails" : "ruleOverview")}
                                        }}
                                        new: rule {{
                                        mgm_id: mgm_id
                                        ...{(detailed ? "ruleDetails" : "ruleOverview")}
                                        }}
                                    }}
                                }}
                            }}
                        }}
                    ");
                    break;

                case ReportType.NatRules:
                    query.FullQuery = Queries.compact($@"
                    {(detailed ? RuleQueries.natRuleDetailsForReportFragments : RuleQueries.natRuleOverviewFragments)}

                    query natRulesReport ({paramString}) 
                    {{ 
                        management( where: {{ mgm_id: {{_in: $mgmId }}, hide_in_gui: {{_eq: false }} stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}} is_pure_routing_device: {{_eq: false}} }} }} order_by: {{ mgm_name: asc }} ) 
                            {{
                                id: mgm_id
                                name: mgm_name
                                devices ( where: {{ hide_in_gui: {{_eq: false }} stm_dev_typ:{{is_pure_routing_device:{{_eq:false}} }} }} order_by: {{ dev_name: asc }} ) 
                                    {{
                                        id: dev_id
                                        name: dev_name
                                        rules(
                                            limit: $limit 
                                            offset: $offset
                                            where: {{  nat_rule: {{_eq: true}}, ruleByXlateRule: {{}} {query.ruleWhereStatement} }} 
                                            order_by: {{ rule_num_numeric: asc }} )
                                            {{
                                                mgm_id: mgm_id
                                                ...{(detailed ? "natRuleDetails" : "natRuleOverview")}
                                            }} 
                                    }}
                            }} 
                    }}");
                    break;
            }

            // remove line breaks and duplicate whitespaces
            Regex pattern = new Regex("\\n");
            query.FullQuery = pattern.Replace(query.FullQuery, "");
            pattern = new Regex("\\s+");
            query.FullQuery = pattern.Replace(query.FullQuery, " ");
            return query;
        }
    }
}
