using System.Text.RegularExpressions;
using FWO.Ui.Filter.Ast;
using FWO.ApiClient.Queries;
using System.Text.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
{

    public class DynGraphqlQuery
    {
        MemoryStream queryVariables = new MemoryStream();
        string whereQuery = "";
        List<string> queryParameters = new List<string>();
    }
    public class QueryGenerator
    {
        public static (string query, MemoryStream queryVariables) ToGraphQl(AstNode ast)
        {
            // prepare json stuff
            using var memoryStreamVariables = new MemoryStream();
            using var queryVariables = new Utf8JsonWriter(memoryStreamVariables);
            queryVariables.WriteStartObject();

            string fullTextFilter = "";
            string timeFilter = "";
            string query;

            string ruleOverviewFragment = RuleQueries.ruleOverviewFragments;

            string queryParameters = @"
                $managementId: [Int!]
                $deviceId: [Int!]
                $limit: Int
                $offset: Int
                ";

            string queryDeviceHeader = @"                    
                management(
                    where: { mgm_id: { _in: $managementId } }
                    order_by: { mgm_name: asc }
                ) 
                {
                    mgm_id
                    mgm_name
                    devices(
                        where: { dev_id: { _in: $deviceId } }
                        order_by: { dev_name: asc }
                    ) {
                        dev_id
                        dev_name
                    }
                ";

            DynGraphqlQuery dynGraphqlQuery = new DynGraphqlQuery();

            string queryRulesWhere = "";

            // mock: assumiming simple text filter 
            fullTextFilter = ast.Extract();
            // check ast, if it contains
            // - fullTextFilter
            // - timeFilter

            if (timeFilter == "")
                queryRulesWhere += " active: { _eq: true } ";
            else
                queryRulesWhere += $" {timeFilter} ";

            if (fullTextFilter != "")
            {
                queryVariables.WriteString("fullText", fullTextFilter);
                queryParameters += " $fullText: String! ";
                queryRulesWhere += @"
                    _or: [
                        { rule_src: { _ilike: *$fullText*} }
                        { rule_dst: { _ilike: *$fullText*} }
                        { rule_svc: { _ilike: *$fullText*} }
                    ] ";
            }

            query = $@"
                {ruleOverviewFragment}

                query ruleFilter ({queryParameters}) 
                    {{ 
                        {queryDeviceHeader} 
                        rules(
                            limit: $limit 
                            offset: $offset
                            where: {{ {queryRulesWhere} }} 
                            order_by: {{ rule_num_numeric: asc }}
                        ) {{
                            ...ruleOverview
                        }} 
                    }} 
                }}";

            query = Regex.Replace(query, "\n", " ");
            queryVariables.WriteEndObject();
            queryVariables.Flush();
            return (query, memoryStreamVariables);
        }

        // test method simple full text search without parsing 

    }
}
