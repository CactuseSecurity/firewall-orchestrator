using FWO.Report.Filter.Ast;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Report;
using System.Text.RegularExpressions;
using FWO.Logging;
using FWO.Basics;


namespace FWO.Report.Filter
{
    public class DynGraphqlQuery(string rawInput)
    {
        public string RawFilter { get; private set; } = rawInput;
        public int parameterCounter { get; set; } = 0;
        public Dictionary<string, object> QueryVariables { get; set; } = [];
        public string FullQuery { get; set; } = "";
        public string RuleWhereStatement { get; set; } = "";
        public string NwObjWhereStatement { get; set; } = "";
        public string SvcObjWhereStatement { get; set; } = "";
        public string UserObjWhereStatement { get; set; } = "";
        public string ConnectionWhereStatement { get; set; } = "";
        public string OwnerWhereStatement { get; set; } = "";
        public string OpenRulesTable { get; set; } = "rules(";
        public string OpenChangeLogRulesTable { get; set; } = "changelog_rules(";
        public List<string> QueryParameters { get; set; } =
        [
            " $limit: Int ",
            " $offset: Int ",
            " $active: Boolean ",
        ];

        public string ReportTimeString { get; set; } = "";
        public List<int> RelevantManagementIds { get; set; } = [];

        public ReportType ReportType { get; set; } = ReportType.Rules;
        public FwoOwner? SelectedOwner { get; set; }
        public const string fullTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const string dateFormat = "yyyy-MM-dd";
        public const int layerRecursionLevel = 2;

        const string mgmtWhereString = $@"where: {{ hide_in_gui: {{_eq: false }}
                                        mgm_id: {{_in: $mgmId }}
                                        stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}} is_pure_routing_device: {{_eq: false}} }}
                                        }} order_by: {{ mgm_name: asc }}";

        const string devWhereStringStart = $@"where: {{ hide_in_gui: {{_eq: false }},
                                        stm_dev_typ: {{is_pure_routing_device:{{_eq:false}} }}";
        const string devWhereStringEnd = $@"}} order_by: {{ dev_name: asc }}";
        const string devWhereStringDefault = devWhereStringStart + devWhereStringEnd;

        const string limitOffsetString = $@"limit: $limit 
                                        offset: $offset ";


        public static DynGraphqlQuery GenerateQuery(ReportTemplate filter, AstNode? ast)
        {
            DynGraphqlQuery query = new(filter.Filter);
            ConstructWhereStatements(query, filter, ast);
            if (((ReportType)filter.ReportParams.ReportType).IsResolvedReport() || (ReportType)filter.ReportParams.ReportType == ReportType.AppRules)
            {
                filter.Detailed = true;
            }
            ConstructFullQuery(query, filter);
            OverwriteMissingTenantFilters(ref query, filter);
            query.FullQuery = RemoveUnnecessaryWhitespaces(query.FullQuery);
            return query;
        }

        private static string RemoveUnnecessaryWhitespaces(string queryString)
        {
            string pattern = @"\n";
            TimeSpan timeout = TimeSpan.FromSeconds(1);
            string cleanQuery = Regex.Replace(queryString, pattern, "", RegexOptions.None, timeout);
            pattern = @"\s+";
            return Regex.Replace(cleanQuery, pattern, " ", RegexOptions.None, timeout);
        }

        private static void ConstructWhereStatements(DynGraphqlQuery query, ReportTemplate filter, AstNode? ast)
        {
            query.RuleWhereStatement += "_and: [";
            query.ConnectionWhereStatement += "_and: [";
            query.OwnerWhereStatement += "_and: [";

            SetFixedFilters(ref query, filter);

            query.RuleWhereStatement += "{";
            query.ConnectionWhereStatement += "{";
            query.OwnerWhereStatement += "{";

            // now we convert the ast into a graphql query:
            ast?.Extract(ref query, (ReportType)filter.ReportParams.ReportType);
            // TODO: remove rule dev filtering for rework 

            query.RuleWhereStatement += "}] ";
            query.ConnectionWhereStatement += "}] ";
            query.OwnerWhereStatement += "}] ";
        }

        private static string ConstructStatisticsQuery(DynGraphqlQuery query, string paramString)
        {
            var insertIndex = query.RuleWhereStatement.LastIndexOf(']');
            var unusedRulesWhereStatement = insertIndex >= 0
                ? query.RuleWhereStatement.Insert(insertIndex, ", {rule_metadatum: { rule_last_hit: { _is_null: true }}}")
                : query.RuleWhereStatement;
            return $@"
                query statisticsReport ({paramString}) 
                {{ 
                    management({mgmtWhereString}) 
                    {{
                        name: mgm_name
                        id: mgm_id
                        objects_aggregate(where: {{ {query.NwObjWhereStatement} }}) {{ aggregate {{ count }} }}
                        services_aggregate(where: {{ {query.SvcObjWhereStatement} }}) {{ aggregate {{ count }} }}
                        usrs_aggregate(where: {{ {query.UserObjWhereStatement} }}) {{ aggregate {{ count }} }}
                        rules_aggregate(where: {{ {query.RuleWhereStatement} }}) {{ aggregate {{ count }} }}
                        unusedRules_Count: rules_aggregate(where: {{ {unusedRulesWhereStatement}}}) {{ aggregate {{ count }} }}
                        devices({devWhereString})
                        {{
                            name: dev_name
                            id: dev_id
                            rules_aggregate(where: {{ {query.RuleWhereStatement} }}) {{ aggregate {{ count }} }}
                            unusedRules_Count: rules_aggregate(where: {{ {unusedRulesWhereStatement}}}) {{ aggregate {{ count }} }}
                        }}
                    }}
                }}";
            //TODO: show number of rulebase links per gateway ?
        }

        private static string ConstructRulesQuery(DynGraphqlQuery query, string paramString, ReportTemplate filter)
        {
            return $@"
                {(filter.Detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments)}
                query rulesReport ({paramString}) 
                {{ 
                    management({mgmtWhereString}) 
                    {{
                        id: mgm_id
                        uid: mgm_uid
                        name: mgm_name
                        devices ({GetDevWhereFilter(ref query, filter.ReportParams.DeviceFilter)})
                        {{
                            id: dev_id
                            name: dev_name
                            uid: dev_uid
                            rulebase_links {{
                                linkType: stm_link_type  {{
                                    name
                                    id
                                }}
                                link_type
                                is_initial
                                is_global
                                is_section
                                gw_id
                                from_rule_id
                                from_rulebase_id
                                to_rulebase_id
                                created
                                removed
                            }}
                        }}
                        rulebases {{
                            name
                            uid
                            id
                            rules (
                                {limitOffsetString}
                                where: {{ access_rule: {{_eq: true}} {query.RuleWhereStatement} }} 
                                order_by: {{ rule_num_numeric: asc }} )
                            {{
                                mgm_id: mgm_id
                                {((ReportType)filter.ReportParams.ReportType == ReportType.UnusedRules ? "rule_metadatum { rule_last_hit }" : "")}
                                ...{(filter.Detailed ? "ruleDetailsForReport" : "ruleOverview")}
                            }} 
                        }}
                    }} 
                }}";
        }

        private static string ConstructRecertQuery(DynGraphqlQuery query, string paramString)
        {
            return $@"
                {RecertQueries.ruleOpenRecertFragments}
                query rulesCertReport({paramString}) 
                {{
                    management({mgmtWhereString}) 
                    {{
                        id: mgm_id
                        name: mgm_name
                        devices({devWhereStringDefault}) 
                        {{
                            id: dev_id
                            name: dev_name
                            {query.OpenRulesTable}
                                where: {{ 
                                    rule_metadatum: {{ recertifications_aggregate: {{ count: {{ filter: {{ _and: [{{owner: $ownerWhere}}, {{recert_date: {{_is_null: true}}}}, {{next_recert_date: {{_lte: $refdate1}}}}]}}, predicate: {{_gt: 0}}}}}}}}
                                    active:{{ _eq:true }}
                                    {query.RuleWhereStatement} 
                                }} 
                                {limitOffsetString}
                                order_by: {{ rule_num_numeric: asc }}
                            ) 
                            {{
                                mgm_id: mgm_id
                                ...ruleOpenCertOverview
                            }}
                        }}
                    }}
                }}";
        }

        private static string ConstructChangesQuery(DynGraphqlQuery query, string paramString, ReportTemplate filter)
        {
            // was:                             devices ({devWhereString})                           

            return $@"
                    {(filter.Detailed ? RuleQueries.ruleDetailsForChangeReportFragments : RuleQueries.ruleOverviewForChangeReportFragments)}
                    query changeReport({paramString}) 
                    {{
                        management(where: {{
                            hide_in_gui: {{_eq: false }}
                            stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}} is_pure_routing_device: {{_eq: false}} }}
                            mgm_id: {{_in: $mgmId }}
                        }} order_by: {{mgm_name: asc}}) 
                        {{
                            id: mgm_id
                            name: mgm_name
                            devices
                            {{
                                id: dev_id
                                name: dev_name
                                {query.OpenChangeLogRulesTable}
                                    {limitOffsetString} 
                                    where: {{ 
                                        _or:[
                                                {{_and: [{{change_action:{{_eq:""I""}}}}, {{rule: {{access_rule:{{_eq:true}}}}}}]}}, 
                                                {{_and: [{{change_action:{{_eq:""D""}}}}, {{ruleByOldRuleId: {{access_rule:{{_eq:true}}}}}}]}},
                                                {{_and: [{{change_action:{{_eq:""C""}}}}, {{rule: {{access_rule:{{_eq:true}}}}}}, {{ruleByOldRuleId: {{access_rule:{{_eq:true}}}}}}]}}
                                            ]                                        
                                        {query.RuleWhereStatement} 
                                    }}
                                    order_by: {{ control_id: asc }}
                                ) 
                                {{
                                    import: import_control {{ time: stop_time }}
                                    change_action
                                    old: ruleByOldRuleId {{
                                    mgm_id: mgm_id
                                    ...{(filter.Detailed ? "ruleDetailsChangesOld" : "ruleOverviewChangesOld")}
                                    }}
                                    new: rule {{
                                    mgm_id: mgm_id
                                    ...{(filter.Detailed ? "ruleDetailsChangesNew" : "ruleOverviewChangesNew")}
                                    }}
                                }}
                            }}
                        }}
                }}";
        }

        private static string ConstructNatRulesQuery(DynGraphqlQuery query, string paramString, ReportTemplate filter)
        {
            return $@"
                {(filter.Detailed ? RuleQueries.natRuleDetailsForReportFragments : RuleQueries.natRuleOverviewFragments)}
                query natRulesReport ({paramString}) 
                {{ 
                    management({mgmtWhereString}) 
                    {{
                        id: mgm_id
                        name: mgm_name
                        devices ({devWhereStringDefault}) 
                        {{
                            id: dev_id
                            name: dev_name
                            {query.OpenRulesTable}
                                {limitOffsetString}
                                where: {{  nat_rule: {{_eq: true}}, ruleByXlateRule: {{}} {query.RuleWhereStatement} }} 
                                order_by: {{ rule_num_numeric: asc }} )
                                {{
                                    mgm_id: mgm_id
                                    ...{(filter.Detailed ? "natRuleDetails" : "natRuleOverview")}
                                }} 
                        }}
                    }} 
                }}";
        }

        private static string ConstructConnectionsQuery(DynGraphqlQuery query, string paramString)
        {
            return $@"
                {ModellingQueries.connectionResolvedDetailsFragment}
                query getConnectionsResolved ({paramString})
                {{
                    modelling_connection (where: {{ {query.ConnectionWhereStatement} }} order_by: {{ is_interface: desc, common_service: desc, name: asc }})
                    {{
                        ...connectionResolvedDetails
                    }}
                }}";
        }

        private static string ConstructOwnerRecertQuery(DynGraphqlQuery query, string paramString)
        {
            return $@"
                {OwnerQueries.ownerDetailsFragment}
                query getOpenOwnerRecerts ({paramString})
                {{
                    owner (where: {{ {query.OwnerWhereStatement} }} order_by: {{ next_recert_date: desc, name: asc }})
                    {{
                        ...ownerDetails
                    }}
                }}";
        }

        private static void ConstructFullQuery(DynGraphqlQuery query, ReportTemplate filter)
        {
            string paramString = string.Join(" ", query.QueryParameters.ToArray());

            switch ((ReportType)filter.ReportParams.ReportType)
            {
                case ReportType.Statistics:
                    query.FullQuery = Queries.Compact(ConstructStatisticsQuery(query, paramString));
                    break;

                case ReportType.Rules:
                case ReportType.ResolvedRules:
                case ReportType.ResolvedRulesTech:
                case ReportType.UnusedRules:
                case ReportType.AppRules:
                case ReportType.Compliance:
                case ReportType.ComplianceDiff:
                case ReportType.RecertEventReport:
                    query.FullQuery = Queries.Compact(ConstructRulesQuery(query, paramString, filter));
                    break;

                case ReportType.Recertification:
                    query.FullQuery = Queries.Compact(ConstructRecertQuery(query, paramString));
                    break;

                case ReportType.Changes:
                case ReportType.ResolvedChanges:
                case ReportType.ResolvedChangesTech:
                    query.FullQuery = Queries.Compact(ConstructChangesQuery(query, paramString, filter));
                    break;

                case ReportType.NatRules:
                    query.FullQuery = Queries.Compact(ConstructNatRulesQuery(query, paramString, filter));
                    break;

                case ReportType.Connections:
                case ReportType.VarianceAnalysis:
                case ReportType.RecertificationEvent:
                    query.FullQuery = Queries.Compact(ConstructConnectionsQuery(query, paramString));
                    break;

                case ReportType.OwnerRecertification:
                    query.FullQuery = Queries.Compact(ConstructOwnerRecertQuery(query, paramString));
                    break;
            }
        }

        private static void SetFixedFilters(ref DynGraphqlQuery query, ReportTemplate reportParams)
        {
            ReportType reportType = (ReportType)reportParams.ReportParams.ReportType;
            if (reportType.IsDeviceRelatedReport())
            {
                query.QueryParameters.Add("$mgmId: [Int!] ");
            }

            // leave out all header texts
            if ((ReportType)reportParams.ReportParams.ReportType == ReportType.Statistics ||
                (ReportType)reportParams.ReportParams.ReportType == ReportType.Recertification ||
                (((ReportType)reportParams.ReportParams.ReportType).IsRuleReport() && !string.IsNullOrWhiteSpace(reportParams.Filter)))
            {
                query.RuleWhereStatement += "{rule_head_text: {_is_null: true}}, ";
            }
            SetTenantFilter(ref query, reportParams);
            if (((ReportType)reportParams.ReportParams.ReportType).IsDeviceRelatedReport())
            {
                SetDeviceFilter(ref query, reportParams.ReportParams.DeviceFilter);
                SetTimeFilter(ref query, reportParams.ReportParams.TimeFilter, (ReportType)reportParams.ReportParams.ReportType, reportParams.ReportParams.RecertFilter);
            }
            if ((ReportType)reportParams.ReportParams.ReportType == ReportType.Recertification)
            {
                SetRecertFilter(ref query, reportParams.ReportParams.RecertFilter);
            }
            if ((ReportType)reportParams.ReportParams.ReportType == ReportType.OwnerRecertification)
            {
                SetOwnerRecertFilter(ref query, reportParams.ReportParams.ModellingFilter, reportParams.ReportParams.RecertFilter);
            }
            if ((ReportType)reportParams.ReportParams.ReportType == ReportType.UnusedRules)
            {
                SetUnusedFilter(ref query, reportParams.ReportParams.UnusedFilter);
            }
            if ((ReportType)reportParams.ReportParams.ReportType == ReportType.AppRules)
            {
                SetOwnerFilter(ref query, reportParams.ReportParams.ModellingFilter);
            }
            if (((ReportType)reportParams.ReportParams.ReportType).IsConnectionRelatedReport())
            {
                SetConnectionFilter(ref query, reportParams.ReportParams.ModellingFilter);
            }
            if ((ReportType)reportParams.ReportParams.ReportType == ReportType.RecertEventReport)
            {
                SetRuleRecertFilter(ref query, reportParams.ReportParams.ModellingFilter);
            }
        }

        private static void SetRuleRecertFilter(ref DynGraphqlQuery query, ModellingFilter modellingFilter)
        {
            query.RuleWhereStatement += $" {{ rule_metadatum: {{ recertifications: {{ owner_recert_id: {{_eq: {modellingFilter.OwnerRecertId} }}, recertified: {{ _eq: true }} }} }} }}";
        }
        private static void SetDeviceFilter(ref DynGraphqlQuery query, DeviceFilter? deviceFilter)
        {
            if (deviceFilter != null)
            {
                query.RelevantManagementIds = deviceFilter.GetSelectedManagements();
            }
        }
        private static string GetDevWhereFilter(ref DynGraphqlQuery query, DeviceFilter? deviceFilter)
        {
            string devWhereStatement = devWhereStringStart;
            bool first = true;

            devWhereStatement += "_or: [{";

            foreach (ManagementSelect mgmt in deviceFilter.Managements)
            {
                foreach (DeviceSelect dev in mgmt.Devices)
                {
                    if (dev.Selected)
                    {
                        if (!first)
                        {
                            devWhereStatement += "}, {";
                        }
                        first = false;
                        devWhereStatement += $@" dev_id: {{_eq:{dev.Id} }} ";
                    }
                }
            }
            devWhereStatement += "}] ";
            devWhereStatement += devWhereStringEnd;
            return devWhereStatement;
        }


        private static void SetTimeFilter(ref DynGraphqlQuery query, TimeFilter? timeFilter, ReportType? reportType, RecertFilter recertFilter)
        {
            if (timeFilter != null)
            {
                query.RuleWhereStatement += "{";
                switch (reportType)
                {
                    case ReportType.Rules:
                    case ReportType.ResolvedRules:
                    case ReportType.ResolvedRulesTech:
                    case ReportType.Statistics:
                    case ReportType.NatRules:
                    case ReportType.UnusedRules:
                    case ReportType.AppRules:
                    case ReportType.Compliance:
                    case ReportType.ComplianceDiff:
                    case ReportType.RecertEventReport:
                        query.QueryParameters.Add("$import_id_start: bigint ");
                        query.QueryParameters.Add("$import_id_end: bigint ");
                        query.RuleWhereStatement +=
                            $"rule_create: {{_lte: $import_id_end}}" +
                            $"_or: [{{removed: {{_gt: $import_id_start}} }}, {{removed: {{_is_null: true}} }}]";
                        query.NwObjWhereStatement +=
                            $"obj_create: {{_lte: $import_id_end }}" +
                            $"_or: [{{removed: {{_gt: $import_id_start}} }}, {{removed: {{_is_null: true}} }}]";
                        query.SvcObjWhereStatement +=
                            $"svc_create: {{_lte: $import_id_end }}" +
                            $"_or: [{{removed: {{_gt: $import_id_start}} }}, {{removed: {{_is_null: true}} }}]";
                        query.UserObjWhereStatement +=
                            $"user_create: {{_lte: $import_id_end }}" +
                            $"_or: [{{removed: {{_gt: $import_id_start}} }}, {{removed: {{_is_null: true}} }}]";
                        query.ReportTimeString = timeFilter.IsShortcut ?
                            DateTime.Now.ToString(fullTimeFormat) : timeFilter.ReportTime.ToString(fullTimeFormat);
                        break;
                    case ReportType.Changes:
                    case ReportType.ResolvedChanges:
                    case ReportType.ResolvedChangesTech:
                        query.QueryParameters.Add("$import_id_old: bigint ");
                        query.QueryParameters.Add("$import_id_new: bigint ");

                        query.RuleWhereStatement += $@"
                        control_id: {{ _eq: $import_id_new }}
                        change_type_id: {{ _eq: 3 }}
                        security_relevant: {{ _eq: true }}";
                        break;
                    case ReportType.Recertification:
                        query.QueryParameters.Add("$import_id_start: bigint ");
                        query.QueryParameters.Add("$import_id_end: bigint ");
                        query.NwObjWhereStatement += "{}";
                        query.SvcObjWhereStatement += "{}";
                        query.UserObjWhereStatement += "{}";
                        query.ReportTimeString = DateTime.Now.AddDays(recertFilter.RecertificationDisplayPeriod).ToString(fullTimeFormat);
                        query.QueryParameters.Add("$refdate1: timestamp! ");
                        query.QueryVariables["refdate1"] = query.ReportTimeString;
                        query.RuleWhereStatement += $@" rule_metadatum: {{ recertifications: {{ next_recert_date: {{ _lte: $refdate1 }} }} }} ";
                        break;
                    case ReportType.Connections:
                    case ReportType.VarianceAnalysis:
                    case ReportType.RecertificationEvent:
                        break;
                    default:
                        Log.WriteError("Filter", $"Unexpected report type found: {reportType}");
                        break;
                }
                query.RuleWhereStatement += "}, ";
            }
        }

        public static (string, string) ResolveTimeRange(TimeFilter timeFilter)
        {
            string start;
            string stop;
            DateTime startOfCurrentYear = new(DateTime.Now.Year, 1, 1);
            DateTime startOfCurrentMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
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
                            start = startOfCurrentWeek.AddDays(-GlobalConst.kDaysPerWeek).ToString(dateFormat);
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
                            throw new NotSupportedException($"Error: wrong time range format:" + timeFilter.TimeRangeShortcut);
                    }
                    break;

                case TimeRangeType.Interval:
                    start = timeFilter.Interval switch
                    {
                        SchedulerInterval.Days => DateTime.Now.AddDays(-timeFilter.Offset).ToString(fullTimeFormat),
                        SchedulerInterval.Weeks => DateTime.Now.AddDays(-GlobalConst.kDaysPerWeek * timeFilter.Offset).ToString(fullTimeFormat),
                        SchedulerInterval.Months => DateTime.Now.AddMonths(-timeFilter.Offset).ToString(fullTimeFormat),
                        SchedulerInterval.Years => DateTime.Now.AddYears(-timeFilter.Offset).ToString(fullTimeFormat),
                        _ => throw new NotSupportedException($"Error: wrong time interval format:" + timeFilter.Interval.ToString()),
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

        private static void SetRecertFilter(ref DynGraphqlQuery query, RecertFilter? recertFilter)
        {
            if (recertFilter != null)
            {
                query.QueryParameters.Add("$ownerWhere: owner_bool_exp");
                query.QueryVariables["ownerWhere"] = recertFilter.RecertOwnerList.Count > 0 ?
                    new { id = new { _in = recertFilter.RecertOwnerList } } : new { id = new { } };
            }
        }

        private static void SetOwnerRecertFilter(ref DynGraphqlQuery query, ModellingFilter? modellingFilter, RecertFilter? recertFilter)
        {
            if (modellingFilter != null)
            {
                query.QueryParameters.Add("$selectedOwners: [Int!]");
                query.QueryVariables["selectedOwners"] = new List<int>(modellingFilter.SelectedOwners.Select(o => o.Id)).ToArray();
                query.OwnerWhereStatement += $@"{{ id: {{ _in: $selectedOwners }} }}";

                if (modellingFilter.RecertActivated)
                {
                    query.OwnerWhereStatement += $@"{{ recert_active: {{ _eq: true }} }}";
                }
                if (!modellingFilter.ShowAllOwners)
                {
                    query.QueryParameters.Add("$refDate: timestamp");
                    query.QueryVariables["refDate"] = DateTime.Now.AddDays(recertFilter?.RecertificationDisplayPeriod ?? 0);
                    query.OwnerWhereStatement += $@"{{ next_recert_date: {{ _lte: $refDate }} }}";
                }
            }
        }

        private static void SetOwnerFilter(ref DynGraphqlQuery query, ModellingFilter? modellingFilter)
        {
            if (modellingFilter != null)
            {
                // currently overruling tenant filter!!
                // query.OpenRulesTable = $"rules: get_rules_for_owner(args: {{ownerid: {modellingFilter.SelectedOwner.Id} }}, ";
                query.OpenRulesTable = $@"
                                        rulebase_links(order_by: {{order_no: asc}}) {{
                                            rulebase_id
                                            order_no
                                            rulebase {{
                                                id
                                                name
                                                rules: get_rules_for_owner(args: {{ownerid: {modellingFilter.SelectedOwner.Id} }}, ";
                query.SelectedOwner = modellingFilter.SelectedOwner;
            }
        }

        private static void SetConnectionFilter(ref DynGraphqlQuery query, ModellingFilter? modellingFilter)
        {
            if (modellingFilter != null)
            {
                query.QueryParameters.Add("$appId: Int!");
                query.QueryVariables["appId"] = modellingFilter.SelectedOwner.Id;
                query.ConnectionWhereStatement += $@"{{ _or: [ {{ app_id: {{ _eq: $appId }} }}, {{ proposed_app_id: {{ _eq: $appId }} }} ], removed: {{ _eq: false }} }}";
            }
        }

        private static void SetUnusedFilter(ref DynGraphqlQuery query, UnusedFilter? unusedFilter)
        {
            if (unusedFilter != null)
            {
                query.QueryParameters.Add("$cut: timestamp");
                query.QueryParameters.Add("$tolerance: timestamp");
                query.QueryVariables["cut"] = DateTime.Now.AddDays(-unusedFilter.UnusedForDays);
                query.QueryVariables["tolerance"] = DateTime.Now.AddDays(-unusedFilter.CreationTolerance);
                query.RuleWhereStatement += $@"{{rule_metadatum: {{_or: [
                    {{_and: [{{rule_last_hit: {{_is_null: false}} }}, {{rule_last_hit: {{_lte: $cut}} }} ] }},
                    {{_and: [{{rule_last_hit: {{_is_null: true}} }}, {{rule_created: {{_lte: $tolerance}} }} ] }} 
                ]}} }}";
            }
        }

        private static void SetTenantFilter(ref DynGraphqlQuery query, ReportTemplate filter)
        {
            if (filter.ReportParams.TenantFilter.IsActive)
            {
                int tenant_id = filter.ReportParams.TenantFilter.TenantId;
                query.OpenRulesTable = $"rules: get_rules_for_tenant(args: {{tenant: {tenant_id}}}, ";
                query.OpenChangeLogRulesTable = $"changelog_rules: get_changelog_rules_for_tenant(args: {{tenant: {tenant_id}}}, ";
            }
        }

        private static void OverwriteMissingTenantFilters(ref DynGraphqlQuery query, ReportTemplate filter)
        {
            // the following additional filters are used for standard and simulated tenant filtering (by admin users)
            if (filter.ReportParams.TenantFilter.IsActive)
            {
                int tenant_id = filter.ReportParams.TenantFilter.TenantId;
                query.FullQuery = Regex.Replace(query.FullQuery, @"\srules\s*\(", $" rules: get_rules_for_tenant(args: {{tenant: {tenant_id}}}, ");
                query.FullQuery = Regex.Replace(query.FullQuery, @"changelog_rules\s*\(", $" changelog_rules: get_changelog_rules_for_tenant(args: {{tenant: {tenant_id}}}, ");
                query.FullQuery = Regex.Replace(query.FullQuery, @"rule_froms\s*\(", $"rule_froms: get_rule_froms_for_tenant(args: {{tenant: {tenant_id}}}");
                query.FullQuery = Regex.Replace(query.FullQuery, @"rule_froms\s*{", $"rule_froms: get_rule_froms_for_tenant(args: {{tenant: {tenant_id}}}) {{");
                query.FullQuery = Regex.Replace(query.FullQuery, @"rule_tos\s*\(", $"rule_tos: get_rule_tos_for_tenant(args: {{tenant: {tenant_id}}}");
                query.FullQuery = Regex.Replace(query.FullQuery, @"rule_tos\s*{", $"rule_tos: get_rule_tos_for_tenant(args: {{tenant: {tenant_id}}}) {{");
            }
        }
    }
}
