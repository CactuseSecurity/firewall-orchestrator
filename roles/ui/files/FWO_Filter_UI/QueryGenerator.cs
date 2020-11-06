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
    public class QueryGenerator
    {
        public static DynGraphqlQuery ToGraphQl(AstNode ast)
        {
            // prepare query object
            DynGraphqlQuery query = new DynGraphqlQuery();
            using var varWriter = new Utf8JsonWriter(query.queryVariables);
            varWriter.WriteStartObject();

            string fullTextFilter = "";
            string timeFilter = "";
            string ruleOverviewFragment = RuleQueries.ruleOverviewFragments;

            ast.Extract(ref query);

            if (query.timeFilter == "")
                query.whereQueryPart += " active: { _eq: true } ";
            else
                query.whereQueryPart += $" {timeFilter} ";

            if (fullTextFilter != "")
            {
                varWriter.WriteString("fullText", fullTextFilter);
                query.queryParameters.Add(" $fullText: String! ");
                query.whereQueryPart += @"
                    _or: [
                        { rule_src: { _ilike: *$fullText*} }
                        { rule_dst: { _ilike: *$fullText*} }
                        { rule_svc: { _ilike: *$fullText*} }
                    ] ";
            }

            string paramString = string.Join(" ", query.queryParameters.ToArray());
            query.fullQuery = $@"
                {ruleOverviewFragment}

                query ruleFilter ({paramString}) 
                    {{ 
                        {query.queryDeviceHeader} 
                        rules(
                            limit: $limit 
                            offset: $offset
                            where: {{ {query.whereQueryPart} }} 
                            order_by: {{ rule_num_numeric: asc }}
                        ) {{
                            ...ruleOverview
                        }} 
                    }} 
                }}";

            query.fullQuery = Regex.Replace(query.fullQuery, "\n", " ");
            varWriter.WriteEndObject();
            varWriter.Flush();
            return query;
        }
    }
}
