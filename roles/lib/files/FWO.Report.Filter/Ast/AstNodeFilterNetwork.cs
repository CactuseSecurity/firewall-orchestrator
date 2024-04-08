﻿using NetTools;
using System.Net;
using FWO.Logging;

namespace FWO.Report.Filter.Ast
{
    internal class AstNodeFilterNetwork : AstNodeFilter
    {
        public override void ConvertToSemanticType()
        {
            CheckOperator(Operator, false, TokenKind.EQ, TokenKind.EEQ, TokenKind.NEQ);
            // semanticValue = int.Parse(Value.Text);
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

        private DynGraphqlQuery ExtractDestinationFilter(DynGraphqlQuery query)
        {
            if (IsCidr(Value.Text))  // filtering for ip addresses
                query = ExtractIpFilter(query, location: "dst", locationTable: "rule_tos");
            else // string search against dst obj name
            {
                string QueryVarName = AddVariable<string>(query, "dst", Operator.Kind, Value.Text);
                query.ruleWhereStatement += $"rule_tos: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }} }}";
                query.connectionWhereStatement += $"_or: [ {{ nwobject_connections: {{connection_field: {{ _eq: 2 }}, owner_network: {{name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }}, " +
                    $"{{ nwgroup_connections: {{connection_field: {{ _eq: 2 }}, nwgroup: {{ name: {{ {ExtractOperator()}: ${QueryVarName} }}  }} }} }} ]";
            }
            return query;
        }


        private DynGraphqlQuery ExtractSourceFilter(DynGraphqlQuery query)
        {
            if (IsCidr(Value.Text))  // filtering for ip addresses
                query = ExtractIpFilter(query, location: "src", locationTable: "rule_froms");
            else // string search against src obj name
            {
                string QueryVarName = AddVariable<string>(query, "src", Operator.Kind, Value.Text);
                query.ruleWhereStatement += $"rule_froms: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }} }}";
                query.connectionWhereStatement += $"_or: [ {{ nwobject_connections: {{connection_field: {{ _eq: 1 }}, owner_network: {{name: {{ {ExtractOperator()}: ${QueryVarName} }} }} }} }}, " +
                    $"{{ nwgroup_connections: {{connection_field: {{ _eq: 1 }}, nwgroup: {{ name: {{ {ExtractOperator()}: ${QueryVarName} }}  }} }} }} ]";
            }
            return query;
        }

        private static string SanitizeIp(string cidr_str)
        {
            IPAddress? ip;
            if (IPAddress.TryParse(cidr_str, out ip))
            {
                if (ip != null)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        cidr_str = ip.ToString();
                        if (cidr_str.IndexOf("/") < 0) // a single ip without mask
                        {
                            cidr_str += "/128";
                        }
                        if (cidr_str.IndexOf("/") == cidr_str.Length - 1) // wrong format (/ at the end, fixing this by adding 128 mask)
                        {
                            cidr_str += "128";
                        }
                    }
                    else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        cidr_str = ip.ToString();
                        if (cidr_str.IndexOf("/") < 0) // a single ip without mask
                        {
                            cidr_str += "/32";
                        }
                        if (cidr_str.IndexOf("/") == cidr_str.Length - 1) // wrong format (/ at the end, fixing this by adding 32 mask)
                        {
                            cidr_str += "32";
                        }
                    }
                }
                else 
                {
                  Log.WriteWarning("SanitizeIP", $"unexpected IP address family (neither v4 nor v6) found");
                }
            }
            return cidr_str;
        }

        private static bool IsCidr(string cidr)
        {
            return IPAddressRange.TryParse(cidr, out IPAddressRange range);
        }

        private DynGraphqlQuery ExtractIpFilter(DynGraphqlQuery query, string location, string locationTable)
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
            query.ruleWhereStatement +=
                $@" {locationTable}: 
                        {{ object: 
                            {{ objgrp_flats: 
                                {{ objectByObjgrpFlatMemberId:
                                    {{ {ipFilterString} }}
                                }}
                            }}
                        }}";
            query.nwObjWhereStatement +=
                $@" {locationTable}: 
                        {{ object: 
                            {{ objgrp_flats: 
                                {{ objectByObjgrpFlatMemberId:
                                    {{ {ipFilterString} }}
                                }}
                            }}
                        }}";
            ipFilterString = $@" ip_end: {{ _gte: ${QueryVarNameFirst1} }} ip: {{ _lte: ${QueryVarNameLast2} }}";
            int conField = location == "src" ? 1 : 2;
            query.connectionWhereStatement += $"nwobject_connections: {{connection_field: {{ _eq: {conField} }}, owner_network: {{ {ipFilterString} }} }}";
            return query;
        }

        // functions["Disabled"] = this.ExtractDisabled;
        // private DynGraphqlQuery ExtractDisabledQuery(DynGraphqlQuery query)
        // {
        //     string QueryOperation = SetQueryOpString(Operator, Name, Value);

        //     if (QueryOperation == null) 
        //     {
        //         QueryOperation = EQ;
        //     }
        //     if (isCidr(Value))  // filtering for ip addresses
        //         query = ExtractIpFilter(query, location: "src", locationTable: "rule_froms");
        //     else // string search against src obj name
        //     {
        //         string QueryVarName = "src" + query.parameterCounter++;
        //         query.QueryVariables[QueryVarName] = $"%{Value}%";
        //         query.QueryParameters.Add($"${QueryVarName}: String! ");
        //         query.ruleWhereStatement += $"rule_froms: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
        //     }
        //     return query;
        // }            // functions["SourceNegated"] = this.ExtractSourceNegated;
        //     // functions["DestinationNegated"] = this.ExtractDestinationNegated;
        //     // functions["ServiceNegated"] = this.ExtractServiceNegated;
    }
}
