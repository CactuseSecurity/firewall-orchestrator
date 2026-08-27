using NetTools;
using System.Net;
using FWO.Logging;
using FWO.Basics;
using FWO.Report.Filter.Exceptions;


namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterNetwork : AstNodeFilter
    {
        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, false, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
        }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            ConvertToSemanticType();

            switch (Name.Kind)
            {
                case TokenKind.Destination:
                    ExtractDestinationFilter(query);
                    break;
                case TokenKind.Source:
                    ExtractSourceFilter(query);
                    break;
                case TokenKind.DestinationType:
                    ExtractObjectTypeFilter(query, reportType, "dst", "rule_tos");
                    break;
                case TokenKind.SourceType:
                    ExtractObjectTypeFilter(query, reportType, "src", "rule_froms");
                    break;
                default:
                    break;
            }
        }

        private void ExtractDestinationFilter(DynGraphqlQuery query)
        {
            if (IsCidr(Value.Text))  // filtering for ip addresses
            {
                ExtractIpFilter(query, location: "dst", locationTable: "rule_tos");
            }
            else // string search against dst obj name
            {
                string QueryVarName = AddVariable<string>(query, "dst", Operator.Kind, Value.Text);
                query.RuleWhereStatement += $"rule_tos: {{ object: {{ {DirectOrFlatObjectFilter($"obj_name: {{ {ExtractOperator()}: ${QueryVarName} }}")} }} }}";
                query.ConnectionWhereStatement += ConnWhere(QueryVarName, 2);
            }
        }


        private void ExtractSourceFilter(DynGraphqlQuery query)
        {
            if (IsCidr(Value.Text))  // filtering for ip addresses
            {
                ExtractIpFilter(query, location: "src", locationTable: "rule_froms");
            }
            else // string search against src obj name
            {
                string QueryVarName = AddVariable<string>(query, "src", Operator.Kind, Value.Text);
                query.RuleWhereStatement += $"rule_froms: {{ object: {{ {DirectOrFlatObjectFilter($"obj_name: {{ {ExtractOperator()}: ${QueryVarName} }}")} }} }}";
                query.ConnectionWhereStatement += ConnWhere(QueryVarName, 1);
            }
        }

        /// <summary>
        /// Adds a source or destination network-object type predicate to a rule report query.
        /// </summary>
        /// <param name="query">Query to extend.</param>
        /// <param name="reportType">Type of report for which the filter is requested.</param>
        /// <param name="location">Object location prefix used for the query variable.</param>
        /// <param name="locationTable">Rule relation to filter.</param>
        private void ExtractObjectTypeFilter(DynGraphqlQuery query, ReportType? reportType, string location, string locationTable)
        {
            if (reportType is not null && !SupportsObjectTypeFilter(reportType.Value))
            {
                throw new SemanticException("Network object type filters are only supported for report queries that use firewall rule predicates.", Name.Position);
            }

            List<string> objectTypes = ExtractObjectTypes();
            string queryVarName = AddObjectTypeVariable(query, location, objectTypes);
            string typeFilter = $"stm_obj_typ: {{ obj_typ_name: {{ _in: ${queryVarName} }} }}";
            string directOrFlatTypeFilter = DirectOrFlatObjectFilter(typeFilter);
            string objectRelationFilter = $"{locationTable}: {{ object: {{ {directOrFlatTypeFilter} }} }}";

            query.RuleWhereStatement += Operator.Kind == TokenKind.NEQ
                ? $"_not: {{ {objectRelationFilter} }}"
                : objectRelationFilter;
        }

        /// <summary>
        /// Determines whether a report embeds firewall-rule predicates, which are required to apply an object type filter.
        /// Rule, change, compliance, and statistics reports are supported because they consume those predicates.
        /// </summary>
        /// <param name="reportType">Report type to evaluate.</param>
        /// <returns><c>true</c> when the report query consumes rule predicates; otherwise, <c>false</c>.</returns>
        private static bool SupportsObjectTypeFilter(ReportType reportType)
        {
            return reportType.IsRuleReport()
                || reportType.IsChangeReport()
                || reportType.IsComplianceReport()
                || reportType == ReportType.Statistics;
        }

        /// <summary>
        /// Parses and normalizes the comma-separated object type names from the filter value.
        /// </summary>
        /// <returns>Normalized object type names.</returns>
        private List<string> ExtractObjectTypes()
        {
            List<string> objectTypes = Value.Text.Split(',', StringSplitOptions.TrimEntries).ToList();
            if (objectTypes.Count == 0 || objectTypes.Any(string.IsNullOrWhiteSpace))
            {
                throw new SemanticException("Network object type filter requires a comma-separated list of object types.", Value.Position);
            }
            return objectTypes.Select(objectType => objectType.ToLowerInvariant()).ToList();
        }

        /// <summary>
        /// Adds an object type list variable to the GraphQL query.
        /// </summary>
        /// <param name="query">Query to extend.</param>
        /// <param name="location">Object location prefix used for the variable name.</param>
        /// <param name="objectTypes">Object type names assigned to the variable.</param>
        /// <returns>Name of the added GraphQL variable.</returns>
        private static string AddObjectTypeVariable(DynGraphqlQuery query, string location, List<string> objectTypes)
        {
            string queryVarName = $"{location}Type" + query.parameterCounter++;
            query.QueryParameters.Add($"${queryVarName}: [String!]! ");
            query.QueryVariables[queryVarName] = objectTypes;
            return queryVarName;
        }

        private string ConnWhere(string QueryVarName, int field)
        {
            return $"_or: [ {{ nwobject_connections: {{connection_field: {{ _eq: {field} }}, owner_network: {{name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }}, " +
                    $"{{ nwgroup_connections: {{connection_field: {{ _eq: {field} }}, nwgroup: {{ _or: [ {{ name: {{ {ExtractOperator()}: ${QueryVarName} }} }}, {{ id_string: {{ {ExtractOperator()}: ${QueryVarName} }} }} ] }} }} }} ]";
        }

        private static string SanitizeIp(string cidrStr)
        {
            if (IPAddress.TryParse(cidrStr, out IPAddress? ip))
            {
                if (ip != null)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        return SanitizeIp(ip, true);
                    }
                    else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return SanitizeIp(ip, false);
                    }
                }
                else
                {
                    Log.WriteWarning("SanitizeIP", $"unexpected IP address family (neither v4 nor v6) found");
                }
            }
            return cidrStr;
        }

        private static string SanitizeIp(IPAddress ip, bool v6)
        {
            string cidrStr = ip.ToString();
            if (cidrStr.IndexOf('/') < 0) // a single ip without mask
            {
                cidrStr += v6 ? "/128" : "/32";
            }
            if (cidrStr.IndexOf('/') == cidrStr.Length - 1) // wrong format (/ at the end, fixing this by adding 128 mask)
            {
                cidrStr += v6 ? "128" : "32";
            }
            return cidrStr;
        }

        private static bool IsCidr(string cidr)
        {
            return IPAddressRange.TryParse(cidr, out _);
        }

        private void ExtractIpFilter(DynGraphqlQuery query, string location, string locationTable)
        {
            IPAddressRange filterIP = IPAddressRange.Parse(SanitizeIp(Value.Text));
            string firstFilterIp = filterIP.Begin.ToString();
            string lastFilterIp = filterIP.End.ToString();
            string QueryVarNameFirst1 = $"{location}IpLow" + query.parameterCounter;
            string QueryVarNameLast2 = $"{location}IpHigh" + query.parameterCounter++;
            query.QueryVariables[QueryVarNameFirst1] = firstFilterIp;
            query.QueryVariables[QueryVarNameLast2] = lastFilterIp;
            query.QueryParameters.Add($"${QueryVarNameFirst1}: cidr! ");
            query.QueryParameters.Add($"${QueryVarNameLast2}: cidr! ");
            // TODO: might simply set all header IP addresses to 0.0.0.0/32 instead of 0.0.0.0/0 to filter them out

            // logic: end_ip1 >= start_ip2 and start_ip1 <= end_ip2
            // end_ip1 = obj_ip_end
            // start_ip2 = QueryVarNameFirst1
            // start_ip1 = obj_ip
            // end_ip2 = QueryVarNameLast2
            // obj_ip_end >= QueryVarNameFirst1 and obj_ip <= QueryVarNameLast2

            string ipFilterString =
                    $@" obj_ip_end: {{ _gte: ${QueryVarNameFirst1} }} 
                        obj_ip: {{ _lte: ${QueryVarNameLast2} }}";
            string objectIpFilterString = DirectOrFlatObjectFilter(ipFilterString);
            query.RuleWhereStatement +=
                $@" _or: [
                      {{
                        rule_{location}_neg: {{_eq: false}},
                        {locationTable}: {{
                        _or: [{{_and: [{{negated: {{_eq: false}}}}, {{object: {{ {objectIpFilterString} }}}}]}},
                              {{_and: [{{negated: {{_eq: true}}}}, {{object: {{_not: {{ {objectIpFilterString} }}}}}}]}}
                        ]}}
                      }},
                      {{
                        rule_{location}_neg: {{_eq: true}},
                        {locationTable}: {{
                        _or: [{{_and: [{{negated: {{_eq: false}}}}, {{object: {{_not: {{ {objectIpFilterString} }}}}}}]}},
                              {{_and: [{{negated: {{_eq: true}}}}, {{object: {{ {objectIpFilterString} }}}}]}}
                        ]}}
                      }},
                    ]
                ";
            query.NwObjWhereStatement +=
                $@" {locationTable}: {{
                    _or: [{{_and: [{{negated: {{_eq: false}}}}, {{object: {{ {objectIpFilterString} }}}}]}},
                          {{_and: [{{negated: {{_eq: true}}}}, {{object: {{_not: {{ {objectIpFilterString} }}}}}}]}}
                    ]
                }}";
            ExtractIpFilterForConn(query, location, QueryVarNameFirst1, QueryVarNameLast2);
        }

        private static string DirectOrFlatObjectFilter(string objectFilterString)
        {
            string filter = objectFilterString.Trim();
            return $@"_or: [{{ {filter} }}, {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ {filter} }} }} }}]";
        }

        private static void ExtractIpFilterForConn(DynGraphqlQuery query, string location, string QueryVarNameFirst1, string QueryVarNameLast2)
        {
            string ipFilterString = $@" ip_end: {{ _gte: ${QueryVarNameFirst1} }} ip: {{ _lte: ${QueryVarNameLast2} }}";
            int conField = location == "src" ? 1 : 2;
            string nwObjString = $"{{ nwobject_connections: {{connection_field: {{ _eq: {conField} }}, owner_network: {{ {ipFilterString} }} }} }}";
            string nwGrpString = $"{{ nwgroup_connections: {{connection_field: {{ _eq: {conField} }}, nwgroup: {{ nwobject_nwgroups: {{ owner_network: {{ {ipFilterString} }} }} }} }} }}";
            query.ConnectionWhereStatement += $@" _or: [{nwObjString}, {nwGrpString}]";
        }
    }
}
