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
        public string RuleWhereQuery { get; set; } = "";
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
                case "rules":

                    query.FullQuery = $@"
                    {ruleOverviewFragment}

                    query ruleFilter ({paramString}) 
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
                                            where: {{ {query.RuleWhereQuery} }} 
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
                                    where: {{ {query.RuleWhereQuery} }}
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
