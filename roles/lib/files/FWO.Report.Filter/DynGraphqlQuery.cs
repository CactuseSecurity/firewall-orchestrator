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
        };  // $mgmId and $relevantImporId are only needed for time based filtering

        public string ReportTimeString { get; set; } = "";
        public List<int> RelevantManagementIds { get; set; } = new List<int>();

        public ReportType ReportType { get; set; } = ReportType.Rules;

        public DynGraphqlQuery(string rawInput) { RawFilter = rawInput; }

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
        private static List<int> GetDeviceFilterAsList(DeviceFilter? deviceFilter)
        {
            List<int> devIdList = new List<int>();
            if (deviceFilter != null)
            {
                foreach (ManagementSelect mgmt in deviceFilter.Managements)
                    foreach (DeviceSelect dev in mgmt.Devices)
                        if (dev.Selected == true)
                            devIdList.Add(dev.Id);
            }
            return devIdList;
        }

        private static void SetTimeFilter(ref DynGraphqlQuery query, TimeFilter? timeFilter, ReportType? reportType, RecertFilter recertFilter)
        {
            if (timeFilter != null)
            {
                query.ruleWhereStatement += "{";
                switch (reportType)
                {
                    case ReportType.Rules:
                    case ReportType.ResolvedRules:
                    case ReportType.ResolvedRulesTech:
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
                    case ReportType.ResolvedChanges:
                    case ReportType.ResolvedChangesTech:
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
                    case ReportType.Recertification:
                        query.nwObjWhereStatement += "{}";
                        query.svcObjWhereStatement += "{}";
                        query.userObjWhereStatement += "{}";
                        query.ReportTimeString = DateTime.Now.AddDays(recertFilter.RecertificationDisplayPeriod).ToString(fullTimeFormat);
                        query.QueryParameters.Add("$refdate1: timestamp! ");
                        query.QueryVariables["refdate1"] = query.ReportTimeString;
                        query.ruleWhereStatement += $@" rule_metadatum: {{ recertifications: {{ next_recert_date: {{ _lte: $refdate1 }} }} }} ";
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


        private static void SetRecertFilter(ref DynGraphqlQuery query, RecertFilter? recertFilter, DeviceFilter deviceFilter)
        {
            // bool first = true;

            List<int> deviceIdFilter = GetDeviceFilterAsList(deviceFilter);
            if (recertFilter != null)
            {
                // query.QueryParameters.Add("$ownerIds: [Int!] ");
                // query.QueryParameters.Add("$refdate1: Timestamp!");

                // setting owner filter:
                if (recertFilter.RecertOwnerList.Count > 0)
                {
                    // query.QueryVariables["ownerIds"] = recertFilter.RecertOwnerList;
                    query.QueryParameters.Add("$ownerWhere: owner_bool_exp");
                    query.QueryVariables["ownerWhere"] = new {id = new {_in = recertFilter.RecertOwnerList}}; 
                }
                else
                { // if no ownerIds are set in the filter, return all recerts
                    query.QueryParameters.Add("$ownerWhere: owner_bool_exp");
                    query.QueryVariables["ownerWhere"] = new {id = new {}};
                }
            }
        }

        private static void SetFixedFilters(ref DynGraphqlQuery query, ReportTemplate reportParams)
        {
            // leave out all header texts
            if (reportParams.ReportParams.ReportType != null &&
                (reportParams.ReportParams.ReportType == (int)ReportType.Statistics ||
                 reportParams.ReportParams.ReportType == (int)ReportType.Recertification))
            {
                query.ruleWhereStatement += "{rule_head_text: {_is_null: true}}, ";
            }
            SetDeviceFilter(ref query, reportParams.ReportParams.DeviceFilter);
            SetTimeFilter(ref query, reportParams.ReportParams.TimeFilter, (ReportType)(reportParams.ReportParams.ReportType ?? throw new Exception("No report type set")), reportParams.ReportParams.RecertFilter);
            if (reportParams.ReportParams.ReportType!= null && (ReportType)reportParams.ReportParams.ReportType==ReportType.Recertification)
            {
                SetRecertFilter(ref query, reportParams.ReportParams.RecertFilter, reportParams.ReportParams.DeviceFilter);
            }
        }

        public static DynGraphqlQuery GenerateQuery(ReportTemplate filter, AstNode? ast)
        {
            DynGraphqlQuery query = new DynGraphqlQuery(filter.Filter);

            query.ruleWhereStatement += "_and: [";

            SetFixedFilters(ref query, filter);

            query.ruleWhereStatement += "{";

            // now we convert the ast into a graphql query:
            if (ast != null)
                ast.Extract(ref query, (ReportType)(filter.ReportParams.ReportType ?? throw new Exception("No report type set")));

            query.ruleWhereStatement += "}] ";

            string paramString = string.Join(" ", query.QueryParameters.ToArray());

            if (((ReportType)(filter.ReportParams.ReportType ?? throw new Exception("No report type set"))).IsResolvedReport())
                filter.Detailed = true;

            switch ((ReportType)(filter.ReportParams.ReportType ?? throw new Exception("No report type set")))
            {
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
                case ReportType.ResolvedRules:
                case ReportType.ResolvedRulesTech:
                    query.FullQuery = Queries.compact($@"
                    {(filter.TenantFiltering ? filter.Detailed ? RuleQueries.tenantRuleDetailsForReportFragments : RuleQueries.tenantRuleOverviewFragments
                                             : filter.Detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments)}

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
                                            where: {{  access_rule: {{_eq: true}} {(filter.TenantFiltering ? "get_rule_froms_for_tenant: {}" : "")} {query.ruleWhereStatement} }} 
                                            order_by: {{ rule_num_numeric: asc }} )
                                            {{
                                                mgm_id: mgm_id
                                                ...{(filter.Detailed ? "ruleDetails" : "ruleOverview")}
                                            }} 
                                    }}
                            }} 
                    }}");
                    break;

                case ReportType.Recertification:
                    // remove Query Parameter relevant import id
                    var itemToRemove = query.QueryParameters.Single(r => r == " $relevantImportId: bigint");
                    query.QueryParameters.Remove(itemToRemove);
                    paramString = string.Join(" ", query.QueryParameters.ToArray());

                    query.FullQuery = Queries.compact($@"{RecertQueries.ruleOpenRecertFragments}
                        query rulesCertReport({paramString}) {{
                        management(
                            where: {{
                                mgm_id: {{ _in: $mgmId }}
                                hide_in_gui: {{ _eq: false }}
                                stm_dev_typ: {{
                                    dev_typ_is_multi_mgmt: {{ _eq: false }}
                                    is_pure_routing_device: {{ _eq: false }}
                                }}
                            }}
                            order_by: {{ mgm_name: asc }}
                        ) {{
                            id: mgm_id
                            name: mgm_name
                            devices(
                                where: {{ hide_in_gui: {{ _eq: false }} }}
                                order_by: {{ dev_name: asc }}
                            ) {{
                                id: dev_id
                                name: dev_name
                                rules(
                                    where: {{ 
                                        rule_metadatum: {{ recertifications_aggregate: {{ count: {{ filter: {{ _and: [{{owner: $ownerWhere}}, {{recert_date: {{_is_null: true}}}}, {{next_recert_date: {{_lte: $refdate1}}}}]}}, predicate: {{_gt: 0}}}}}}}}
                                        active:{{ _eq:true }}
                                        {query.ruleWhereStatement} 
                                    }} 
                                    limit: $limit
                                    offset: $offset
                                    order_by: {{ rule_num_numeric: asc }}
                                ) {{
                                    mgm_id: mgm_id
                                    ...ruleOpenCertOverview
                                }}
                            }}
                        }}
                    }}");
                    break;

                case ReportType.Changes:
                case ReportType.ResolvedChanges:
                case ReportType.ResolvedChangesTech:
                    query.FullQuery = Queries.compact($@"
                    {(filter.TenantFiltering ? filter.Detailed ? RuleQueries.tenantRuleDetailsForReportFragments : RuleQueries.tenantRuleOverviewFragments
                                             : filter.Detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments)}

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
                                        {(filter.TenantFiltering ? "ruleByOldRuleId: { get_rule_froms_for_tenant: {} }" : "")}
                                        {query.ruleWhereStatement} 
                                    }}
                                    order_by: {{ control_id: asc }}
                                ) 
                                    {{
                                        import: import_control {{ time: stop_time }}
                                        change_action
                                        old: ruleByOldRuleId {{
                                        mgm_id: mgm_id
                                        ...{(filter.Detailed ? "ruleDetails" : "ruleOverview")}
                                        }}
                                        new: rule {{
                                        mgm_id: mgm_id
                                        ...{(filter.Detailed ? "ruleDetails" : "ruleOverview")}
                                        }}
                                    }}
                                }}
                            }}
                        }}
                    ");
                    break;

                case ReportType.NatRules:
                    query.FullQuery = Queries.compact($@"
                    {(filter.TenantFiltering ? filter.Detailed ? RuleQueries.tenantNatRuleDetailsForReportFragments : RuleQueries.tenantNatRuleOverviewFragments
                                             : filter.Detailed ? RuleQueries.natRuleDetailsForReportFragments : RuleQueries.natRuleOverviewFragments)}

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
                                            where: {{  nat_rule: {{_eq: true}}, ruleByXlateRule: {{}} {(filter.TenantFiltering ? "get_rule_froms_for_tenant: {}" : "")} {query.ruleWhereStatement} }} 
                                            order_by: {{ rule_num_numeric: asc }} )
                                            {{
                                                mgm_id: mgm_id
                                                ...{(filter.Detailed ? "natRuleDetails" : "natRuleOverview")}
                                            }} 
                                    }}
                            }} 
                    }}");
                    break;
            }

            string pattern = "";

            // remove comment lines (#) before joining lines!
            // Regex.Replace("10, 20, 30", @"(\d+)$",match => (int.Parse(match.Value)+1).ToString())
            // Regex.Replace(query.FullQuery, pattern, m => variablesDictionary[m.Value]);
            // Regex pattern = new Regex(@"#(.*?)\n");

            // TODO: get this working
            // pattern = @"""[^""\\]*(?:\\[\W\w][^""\\]*)*""|(\#.*)";
            // string pattern = @"(.*?)(#.*?)\n(.*?)";
            // query.FullQuery = Regex.Replace(query.FullQuery, pattern, "");

            // remove line breaks and duplicate whitespaces
            pattern = @"\n";
            query.FullQuery = Regex.Replace(query.FullQuery, pattern, "");
            pattern = @"\s+";
            query.FullQuery = Regex.Replace(query.FullQuery, pattern, " ");
            return query;
        }
    }
}
