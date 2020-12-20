using FWO.Report.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Dynamic;
using System.Net.Http;
using System.Threading.Tasks;
using FWO.Logging;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Client.Abstractions;
using System.Linq;
using FWO.ApiClient.Queries;

namespace FWO.Report.Filter
{
    public class DynGraphqlQuery
    {
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
        public string ReportType { get; set; } = "";

        // $mgmId and $relevantImporId are only needed for time based filtering
        private DynGraphqlQuery() { }

        public static DynGraphqlQuery Generate(AstNode ast)
        {
            string ruleOverviewFragment = RuleQueries.ruleOverviewFragments;

            DynGraphqlQuery query = new DynGraphqlQuery();
            ast.Extract(ref query);

            // if any filter is set, optionally leave out all header texts

            string paramString = string.Join(" ", query.QueryParameters.ToArray());
            switch (query.ReportType)
            {
                // todo: move $mdmId filter from management into query.xxxWhereStatement
                case "statistics":
                    query.FullQuery = $@"
                    query statisticsReport ({paramString}) 
                    {{ 
                        management(where: {{mgm_id: {{_in: $mgmId }} }} order_by: {{ mgm_name: asc }}) 
                        {{
                            name: mgm_name
                            id: mgm_id
                            objects_aggregate(where: {{ {query.nwObjWhereStatement} }}) {{ aggregate {{ count }} }}
                            services_aggregate(where: {{ {query.svcObjWhereStatement} }}) {{ aggregate {{ count }} }}
                            usrs_aggregate(where: {{ {query.userObjWhereStatement} }}) {{ aggregate {{ count }} }}
                            rules_aggregate(where: {{ {query.ruleWhereStatement} }}) {{ aggregate {{ count }} }}
                            devices(order_by: {{ dev_name: asc }}) 
                            {{
                                name: dev_name
                                id: dev_id
                                rules_aggregate(where: {{ {query.ruleWhereStatement} }}) {{ aggregate {{ count }} }}
                            }}
                        }}
                    }}";
                    break;                

                case "rules":
                    query.FullQuery = $@"
                    {ruleOverviewFragment}

                    query rulesReport ({paramString}) 
                    {{ 
                        management( where: {{ mgm_id: {{_in: $mgmId }} }} order_by: {{ mgm_name: asc }} ) 
                            {{
                                id: mgm_id
                                name: mgm_name
                                devices ( order_by: {{ dev_name: asc }} ) 
                                    {{
                                        id: dev_id
                                        name: dev_name
                                        rules(
                                            limit: $limit 
                                            offset: $offset
                                            where: {{ {query.ruleWhereStatement} }} 
                                            order_by: {{ rule_num_numeric: asc }} )
                                            {{
                                                ...ruleOverview
                                            }} 
                                    }}
                            }} 
                    }}";
                    break;
                case "changes":
                    query.FullQuery = $@"
                    {ruleOverviewFragment}

                    query changeReport({paramString}) {{
                        management(order_by: {{mgm_name: asc}}) 
                        {{
                            id: mgm_id
                            name: mgm_name
                            devices (order_by: {{dev_name: asc}}) 
                            {{
                                id: dev_id
                                name: dev_name
                                changelog_rules(
                                    offset: $offset 
                                    limit: $limit 
                                    where: {{ {query.ruleWhereStatement} }}
                                    order_by: {{ control_id: asc }}
                                ) 
                                    {{
                                        import: import_control {{ time: stop_time }}
                                        change_action
                                        old: ruleByOldRuleId {{
                                        ...ruleOverview
                                        }}
                                        new: rule {{
                                        ...ruleOverview
                                        }}
                                    }}
                                }}
                            }}
                        }}
                    ";
                    break;
            }
            return query;
        }
    }
}
