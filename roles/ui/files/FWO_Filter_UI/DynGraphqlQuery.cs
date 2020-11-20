using FWO.Ui.Filter.Ast;
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

namespace FWO.Ui.Filter
{
    public class DynGraphqlQuery
    {
        public int parameterCounter = 0;
        public string queryDeviceHeader { get; }
        public Dictionary<string, object> QueryVariables { get; set; } = new Dictionary<string, object>();
        public string FullQuery { get; set; } = "";
        public string WhereQueryPart { get; set; } = "";
        public string ManagementQueryPart { get; set; } = "";
        public string DeviceQueryPart { get; set; } = "";
        public List<string> QueryParameters { get; set; } = new List<string>()
        {
            " $limit: Int ",
            " $offset: Int "
        };
        public string TimeFilter { get; set; }

        private DynGraphqlQuery() { }

        public static DynGraphqlQuery Generate(AstNode ast)
        {
            string timeFilter = "";
            string ruleOverviewFragment = RuleQueries.ruleOverviewFragments;

            DynGraphqlQuery query = new DynGraphqlQuery();
            ast.Extract(ref query);

            //if (query.TimeFilter == "")
            //    query.WhereQueryPart += ", active: { _eq: true } ";
            //else
            //    query.WhereQueryPart += $" {timeFilter} ";

            // if any filter is set, leave out all header texts

            string paramString = string.Join(" ", query.QueryParameters.ToArray());
            query.FullQuery = $@"
                {ruleOverviewFragment}

                query ruleFilter ({paramString}) 
                {{ 
                    management(
                        where: {{ {query.ManagementQueryPart} }}
                        order_by: {{ mgm_name: asc }} ) 
                        {{
                            id: mgm_id
                            name: mgm_name
                            devices (
                                where: {{ {query.DeviceQueryPart} }}
                                order_by: {{ dev_name: asc }} ) 
                                {{
                                    id: dev_id
                                    name: dev_name
                                    rules(
                                        limit: $limit 
                                        offset: $offset
                                        where: {{ {query.WhereQueryPart} }} 
                                        order_by: {{ rule_num_numeric: asc }} )
                                        {{
                                            ...ruleOverview
                                        }} 
                                }}
                        }} 
                }}";

            // remove linebreaks and multiple whitespaces
            //query.FullQuery = Regex.Replace(query.FullQuery, "\n", " ");
            //query.FullQuery = Regex.Replace(query.FullQuery, @"\s+", " ");

            return query;
        }
    }
}
