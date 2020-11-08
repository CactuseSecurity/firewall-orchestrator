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
            string timeFilter = "";
            string ruleOverviewFragment = RuleQueries.ruleOverviewFragments;

            ast.Extract(ref query);

            if (query.TimeFilter == "")
                query.WhereQueryPart += ", active: { _eq: true } ";
            else
                query.WhereQueryPart += $" {timeFilter} ";

            string paramString = string.Join(" ", query.QueryParameters.ToArray());
            query.FullQuery = $@"
                {ruleOverviewFragment}

                query ruleFilter ({paramString}) 
                    {{ 
                        {query.queryDeviceHeader} 
                        rules(
                            limit: $limit 
                            offset: $offset
                            where: {{ {query.WhereQueryPart} }} 
                            order_by: {{ rule_num_numeric: asc }}
                        ) {{
                            ...ruleOverview
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
