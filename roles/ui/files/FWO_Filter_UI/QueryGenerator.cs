using FWO.Ui.Filter.Ast;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Ui.Filter
{
    public class QueryGenerator
    {
        public static string ToGraphQl(AstNode ast)
        {
            string fullTextFilter = "";
            string timeFilter = "";
            string query;
            string query_parameters = @"
                $managementId: [Int!]
                $deviceId: [Int!]
                $limit: Int
                $offset: Int
                ";

            string query_device_header = @"                    
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

            string query_rules_where = "";
            string query_variables = "";
            string query_rules_overview = @"
                    rule_uid
                    rule_src
                    rule_dst
                    rule_svc
                ";

            // check ast, if it contains
            // - fullTextFilter
            // - timeFilter

            if (timeFilter == "")
                query_rules_where += " active: { _eq: true } ";
            else
                query_rules_where += $" {timeFilter} ";

            if (fullTextFilter != "")
            {
                query_variables += $" \"fullText\": \"{fullTextFilter}\", ";
                query_parameters += " $fullText: String! ";
                query_rules_where += @"
                    _or: [
                        { rule_src: { _ilike: *$fullText*} }
                        { rule_dst: { _ilike: *$fullText*} }
                        { rule_svc: { _ilike: *$fullText*} }
                    ] ";
            }

            query = $@"
                query ruleFilter ({query_parameters}) 
                    {{ 
                        {query_device_header} 
                        rules(
                            limit: $limit 
                            offset: $offset
                            where: {{ {query_rules_where} }} 
                        }}
                        order_by: {{ rule_num_numeric: asc }}
                    ) {{
                        {query_rules_overview}
                    }}";

            query_variables = $"{{ {query_variables.TrimEnd()} }}";
            return query;
        }
    }
}
