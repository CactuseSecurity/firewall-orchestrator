using NetTools;
using System.Net;
using FWO.Logging;
using FWO.Basics;


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
                query.RuleWhereStatement += $"rule_tos: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }} }}";
                query.ConnectionWhereStatement += $"_or: [ {{ nwobject_connections: {{connection_field: {{ _eq: 2 }}, owner_network: {{name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }}, " +
                    $"{{ nwgroup_connections: {{connection_field: {{ _eq: 2 }}, nwgroup: {{ _or: [ {{ name: {{ {ExtractOperator()}: ${QueryVarName} }} }}, {{ id_string: {{ {ExtractOperator()}: ${QueryVarName} }} }} ] }} }} }} ]";
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
                query.RuleWhereStatement += $"rule_froms: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }} }}";
                query.ConnectionWhereStatement += $"_or: [ {{ nwobject_connections: {{connection_field: {{ _eq: 1 }}, owner_network: {{name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }}, " +
                    $"{{ nwgroup_connections: {{connection_field: {{ _eq: 1 }}, nwgroup: {{ _or: [ {{ name: {{ {ExtractOperator()}: ${QueryVarName} }} }}, {{ id_string: {{ {ExtractOperator()}: ${QueryVarName} }} }} ] }} }} }} ]";
            }
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
            query.RuleWhereStatement +=
                $@" _or: [
                      {{
                        rule_{location}_neg: {{_eq: false}},
                        {locationTable}: {{
                        _or: [{{_and: [{{negated: {{_eq: false}}}}, {{object: {{objgrp_flats: {{objectByObjgrpFlatMemberId: {{ {ipFilterString} }}}}}}}}]}},
                              {{_and: [{{negated: {{_eq: true}}}}, {{object: {{_not: {{objgrp_flats: {{objectByObjgrpFlatMemberId: {{ {ipFilterString} }}}}}}}}}}]}}
                        ]}}
                      }},
                      {{
                        rule_{location}_neg: {{_eq: true}},
                        {locationTable}: {{
                        _or: [{{_and: [{{negated: {{_eq: false}}}}, {{object: {{_not: {{objgrp_flats: {{objectByObjgrpFlatMemberId: {{ {ipFilterString} }}}}}}}}}}]}},
                              {{_and: [{{negated: {{_eq: true}}}}, {{object: {{objgrp_flats: {{objectByObjgrpFlatMemberId: {{ {ipFilterString} }}}}}}}}]}}
                        ]}}
                      }},
                    ]
                ";
            query.NwObjWhereStatement +=
                $@" {locationTable}: {{
                    _or: [{{_and: [{{negated: {{_eq: false}}}}, {{object: {{objgrp_flats: {{objectByObjgrpFlatMemberId: {{ {ipFilterString} }}}}}}}}]}},
                          {{_and: [{{negated: {{_eq: true}}}}, {{object: {{_not: {{objgrp_flats: {{objectByObjgrpFlatMemberId: {{ {ipFilterString} }}}}}}}}}}]}}
                    ]
                }}";
            ipFilterString = $@" ip_end: {{ _gte: ${QueryVarNameFirst1} }} ip: {{ _lte: ${QueryVarNameLast2} }}";
            int conField = location == "src" ? 1 : 2;
            query.ConnectionWhereStatement += $"nwobject_connections: {{connection_field: {{ _eq: {conField} }}, owner_network: {{ {ipFilterString} }} }}";
        }
    }
}
