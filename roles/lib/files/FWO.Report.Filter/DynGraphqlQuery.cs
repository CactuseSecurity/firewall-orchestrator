using FWO.Report.Filter.Ast;
using FWO.ApiClient.Queries;
using FWO.Api.Data;
using System.Text.RegularExpressions;

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
        public string ReportTime { get; set; } = "";

        public ReportType ReportType { get; set; } = ReportType.None;

        // $mgmId and $relevantImporId are only needed for time based filtering
        private DynGraphqlQuery(string rawInput) { RawFilter = rawInput; }

        private static void SetFixedFilters(ref DynGraphqlQuery query, DeviceFilter? deviceFilter, ReportType? reportType)
        {
            bool first = true;
            query.ruleWhereStatement += "_and: [";

            // leave out all header texts
            if (reportType != null && reportType == ReportType.Statistics)
            {
                query.ruleWhereStatement += "{rule_head_text: {_is_null: true}}, ";
            }

            if (deviceFilter != null)
            {
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
            query.ruleWhereStatement += "{";
        }

        public static DynGraphqlQuery GenerateQuery(string rawInput, AstNode ast, DeviceFilter? deviceFilter, ReportType? reportType, bool detailed)
        {
            DynGraphqlQuery query = new DynGraphqlQuery(rawInput);

            SetFixedFilters(ref query, deviceFilter, reportType);

            // now we convert the ast into a graphql query:
            ast.Extract(ref query, reportType);

            // Close device filter
            query.ruleWhereStatement += "}] ";

            string paramString = string.Join(" ", query.QueryParameters.ToArray());
            switch (reportType)
            {
                // todo: move $mdmId filter from management into query.xxxWhereStatement
                // management(where: {{mgm_id: {{_in: $mgmId }} }} order_by: {{ mgm_name: asc }}) 
                        // management(order_by: {{ mgm_name: asc }}) 
                case ReportType.Statistics:
                    query.FullQuery = $@"
                    query statisticsReport ({paramString}) 
                    {{ 
                        management(
                            where: {{ 
                                hide_in_gui: {{_eq: false }}  
                                mgm_id: {{_in: $mgmId }} 
                                stm_dev_typ:{{dev_typ_is_multi_mgmt:{{_eq:false}} }}
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
                            devices( where: {{ hide_in_gui: {{_eq: false }} }} order_by: {{ dev_name: asc }} )
                            {{
                                name: dev_name
                                id: dev_id
                                rules_aggregate(where: {{ {query.ruleWhereStatement} }}) {{ aggregate {{ count }} }}
                            }}
                        }}
                    }}";
                    break;                

                case ReportType.Rules:
                    query.FullQuery = $@"
                    {(detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments)}

                    query rulesReport ({paramString}) 
                    {{ 
                        management( where: 
                            {{ 
                                mgm_id: {{_in: $mgmId }}, 
                                hide_in_gui: {{_eq: false }} 
                                stm_dev_typ:{{dev_typ_is_multi_mgmt:{{_eq:false}} }}
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
                                                ...{(detailed ? "ruleDetails" : "ruleOverview")}
                                            }} 
                                    }}
                            }} 
                    }}";
                    break;
                    
                case ReportType.Changes:
                    query.FullQuery = $@"
                    {(detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments)}

                    query changeReport({paramString}) {{
                        management(where: {{ hide_in_gui: {{_eq: false }} stm_dev_typ:{{dev_typ_is_multi_mgmt:{{_eq:false}} }} }} order_by: {{mgm_name: asc}}) 
                        {{
                            id: mgm_id
                            name: mgm_name
                            devices (where: {{ hide_in_gui: {{_eq: false}} }}, order_by: {{dev_name: asc}} )                           
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
                                        ...{(detailed ? "ruleDetails" : "ruleOverview")}
                                        }}
                                        new: rule {{
                                        ...{(detailed ? "ruleDetails" : "ruleOverview")}
                                        }}
                                    }}
                                }}
                            }}
                        }}
                    ";
                    break;

                case ReportType.NatRules:
                    query.FullQuery = $@"
                    {(detailed ? RuleQueries.natRuleDetailsForReportFragments : RuleQueries.natRuleOverviewFragments)}

                    query natRulesReport ({paramString}) 
                    {{ 
                        management( where: {{ mgm_id: {{_in: $mgmId }}, hide_in_gui: {{_eq: false }} stm_dev_typ:{{dev_typ_is_multi_mgmt:{{_eq:false}} }} }} order_by: {{ mgm_name: asc }} ) 
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
                                            where: {{  nat_rule: {{_eq: true}}, ruleByXlateRule: {{}} {query.ruleWhereStatement} }} 
                                            order_by: {{ rule_num_numeric: asc }} )
                                            {{
                                                ...{(detailed ? "natRuleDetails" : "natRuleOverview")}
                                            }} 
                                    }}
                            }} 
                    }}";
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
