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
            DynGraphqlQuery query = new DynGraphqlQuery();
            string fullTextFilter = "ny";
            string timeFilter = "";
            string ruleOverviewFragment = RuleQueries.ruleOverviewFragments;

            ast.Extract(ref query);

            if (query.timeFilter == "")
                query.whereQueryPart += " active: { _eq: true } ";
            else
                query.whereQueryPart += $" {timeFilter} ";

            if (fullTextFilter != "")
            {
                query.addVariable("fullText", $"%{fullTextFilter}%");
                query.queryParameters.Add(" $fullText: String! ");
                query.whereQueryPart += @"
                    _or: [
                        { rule_src: { _ilike: $fullText} }
                        { rule_dst: { _ilike: $fullText} }
                        { rule_svc: { _ilike: $fullText} }
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

            // remove linebreaks and multiple whitespaces
            query.fullQuery = Regex.Replace(query.fullQuery, "\n", " ");
            query.fullQuery = Regex.Replace(query.fullQuery, @"\s+", " ");
            return query;
        }
    }
}
