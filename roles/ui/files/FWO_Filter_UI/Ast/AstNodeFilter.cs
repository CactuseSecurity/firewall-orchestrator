using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FWO.Ui.Filter.Ast
{
    class AstNodeFilter : AstNode
    {
        public TokenKind Name { get; set; }
        public TokenKind Operator { get; set; }
        public string Value { get; set; }
        private List<string> ruleFieldNames { get; set; }
        private int queryLevel { get; set; }

        public override void Extract(ref DynGraphqlQuery query)
        {
            Dictionary<string, Func<DynGraphqlQuery, DynGraphqlQuery>> functions = new Dictionary<string, Func<DynGraphqlQuery, DynGraphqlQuery>>();

            functions["FullText"] = this.ExtractFullTextQuery;
            functions["Value"] = this.ExtractFullTextQuery; // "xy" and "FullText=xy" are the same filter

            functions["DestinationPort"] = this.ExtractDestinationPort;
            functions["Time"] = this.ExtractTimeQuery;
            functions["Source"] = this.ExtractSourceQuery;
            functions["Destination"] = this.ExtractDestinationQuery;
            functions["Action"] = this.ExtractActionQuery;
            functions["Service"] = this.ExtractServiceQuery;
            functions["Protocol"] = this.ExtractProtocolQuery;
            functions["Management"] = this.ExtractManagementQuery;
            functions["Gateway"] = this.ExtractGatewayQuery;

            // call the method matching the Name of the current node to build the graphQL query
            query = functions[Name.ToString()](query);

            return;
        }

        private DynGraphqlQuery ExtractTimeQuery(DynGraphqlQuery query)
        {
                query.RuleWhereQuery +=
                    $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                    $"importControlByRuleLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                query.ReportTime = Value;
            // }
            return query;
        }

        private DynGraphqlQuery ExtractIpFilter(DynGraphqlQuery query, string location, string locationTable)
        {
            string filterIP = sanitizeIp(Value);
            (string firstIp, string lastIp) = getFirstAndLastIp(filterIP);
            string ipFilterString = "";

            if (firstIp == lastIp) // optimization, just need a single comparison if searching for single ip
            {
                string QueryVarName = $"{location}Ip" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = firstIp;
                query.QueryParameters.Add($"${QueryVarName}: cidr! ");
                // checking if single filter ip is part of a cidr subnet (or is a direct match for a single ip)
                ipFilterString =  $@" _and: 
                                        [ 
                                            {{ obj_ip: {{ _gte: ${QueryVarName} }} }}
                                            {{ obj_ip: {{ _lte: ${QueryVarName} }} }}
                                        ]";
             }
            else // ip filter is a subnet with /xy
            {
                string QueryVarName0 = $"{location}IpNet" + query.parameterCounter;
                string QueryVarName1 = $"{location}IpLow" + query.parameterCounter;
                string QueryVarName2 = $"{location}IpHigh" + query.parameterCounter++;
                query.QueryVariables[QueryVarName0] = filterIP;
                query.QueryVariables[QueryVarName1] = firstIp;
                query.QueryVariables[QueryVarName2] = lastIp;
                query.QueryParameters.Add($"${QueryVarName0}: cidr! ");
                query.QueryParameters.Add($"${QueryVarName1}: cidr! ");
                query.QueryParameters.Add($"${QueryVarName2}: cidr! ");
                // covering various cases: 
                // 1 - current ip is fully contained in filter ip range
                // 2 - current ip overlaps with lower boundary of filter ip range
                // 3 - current ip overlaps with upper boundary of filter ip range
                // 4 - current ip fully contains filter ip range - does not work
                ipFilterString = 
                     $@" _or: [
                            {{ obj_ip: {{ _eq: ${QueryVarName0} }} }}
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _gte: ${QueryVarName1} }} }}
                                        {{ obj_ip: {{ _lte: ${QueryVarName2} }} }}
                                    ]
                            }}
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _lte: ${QueryVarName1} }} }}
                                        {{ obj_ip: {{ _gte: ${QueryVarName1} }} }}
                                    ]
                            }} 
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _lte: ${QueryVarName2} }} }}
                                        {{ obj_ip: {{ _gte: ${QueryVarName2} }} }}
                                    ]
                            }}
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _lte: ${QueryVarName1} }} }}
                                        {{ obj_ip: {{ _gte: ${QueryVarName2} }} }}
                                    ]
                            }}
                     ]";
            }
            query.RuleWhereQuery += 
                $@" {locationTable}: 
                        {{ object: 
                            {{ objgrp_flats: 
                                {{ objectByObjgrpFlatMemberId:
                                    {{ {ipFilterString} }}
                                }}
                            }}
                        }}";
            return query;
        }

        private DynGraphqlQuery ExtractSourceQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            if (isCidr(Value))  // filtering for ip addresses
                query = ExtractIpFilter(query, location: "src", locationTable: "rule_froms");
            else // string search against src obj name
            {
                string QueryVarName = "src" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = $"%{Value}%";
                query.QueryParameters.Add($"${QueryVarName}: String! ");
                query.RuleWhereQuery += $"rule_froms: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            }
            return query;
        }
        private DynGraphqlQuery ExtractDestinationQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            if (isCidr(Value))  // filtering for ip addresses
                query = ExtractIpFilter(query, location: "dst", locationTable: "rule_tos");
            else // string search against dst obj name
            {
                string QueryVarName = "dst" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = $"%{Value}%";
                query.QueryParameters.Add($"${QueryVarName}: String! ");
                query.RuleWhereQuery += $"rule_tos: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            }
            return query;
        }

        private DynGraphqlQuery ExtractServiceQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "svc" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"rule_services: {{service: {{svcgrp_flats: {{serviceBySvcgrpFlatMemberId: {{svc_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractActionQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "action" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"rule_action: {{ {QueryOperation}: ${QueryVarName} }}";
            return query;
        }
        private DynGraphqlQuery ExtractProtocolQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "proto" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"rule_services: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractManagementQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "mgmtName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractGatewayQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "gwName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";

            return query;
        }
        private DynGraphqlQuery ExtractFullTextQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "fullTextFilter" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";

            ruleFieldNames = new List<string>() { "rule_src", "rule_dst", "rule_svc", "rule_action" };  // TODO: add comment later
            List<string> searchParts = new List<string>();
            foreach (string field in ruleFieldNames)
                searchParts.Add($"{{{field}: {{{QueryOperation}: ${QueryVarName} }} }} ");
            query.RuleWhereQuery += " _or: [";
            query.RuleWhereQuery += string.Join(", ", searchParts);
            query.RuleWhereQuery += "]";

            return query;
        }

        private DynGraphqlQuery ExtractDestinationPort(DynGraphqlQuery query)
        {
            string QueryVarName = "dport" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: Int! ");
            query.QueryVariables[QueryVarName] = Value;

            query.RuleWhereQuery +=
                " rule_services: { service: { svcgrp_flats: { service: { svc_port: {_lte" +
                ": $" + QueryVarName + "}, svc_port_end: {_gte: $" + QueryVarName + "} } } } }";
            return query;
        }

        private static string SetQueryOpString(TokenKind Operator, TokenKind Name, string Value)
        {
            string operation = "";
            switch (Operator)
            {
                case TokenKind.EQ:
                    if (Name == TokenKind.Time || Name == TokenKind.DestinationPort)
                        operation = "_eq";
                    else if ((Name == TokenKind.Source && isCidr(Value)) || Name == TokenKind.DestinationPort)
                        operation = "_eq";
                    else
                        operation = "_ilike";
                    break;
                case TokenKind.NEQ:
                    operation = "_nilike";
                    break;
                default:
                    throw new Exception("### Parser Error: Expected Operator Token (and thought there is one) ###");
            }
            return operation;
        }

        private static string sanitizeIp(string cidr_str)
        {
            IPAddress ip;
            if (IPAddress.TryParse(cidr_str, out ip))
            {
                cidr_str = ip.ToString();
                if (cidr_str.IndexOf("/") < 0) // a single ip without mask
                {
                    cidr_str += "/32";
                }
                if (cidr_str.IndexOf("/") == cidr_str.Length-1) // wrong format (/ at the end, fixing this by adding 32 mask)
                {
                    cidr_str += "32";
                }
            }
            return cidr_str;
        }

        private static bool isCidr(string cidr)
        {
            // IPV4 only:

            string[] IPA = sanitizeIp(cidr).Split('/');
            if (IPA.Length == 2)
            {
                if (IPAddress.TryParse(IPA[0], out _))
                {
                    int bitsInMask = Int16.Parse(IPA[1]);
                    if (bitsInMask >= 0 && bitsInMask <= 32)
                        return true;
                }
            }
            else if (IPA.Length == 1) // no / in string, simple IP
            {
                if (IPAddress.TryParse(cidr, out _))
                {
                    return true;
                }
            }
            // TODO: IPv6 handling
            return false;
        }

        private static string toip(uint ip)
        {
            // TODO: IPv6 handling
            return String.Format("{0}.{1}.{2}.{3}", ip >> 24, (ip >> 16) & 0xff, (ip >> 8) & 0xff, ip & 0xff);
        }

        private static (string, string) getFirstAndLastIp(string cidr)
        {
            // TODO: IPv6 handling
            string[] parts = sanitizeIp(cidr).Split('.', '/');

            uint ipnum = (Convert.ToUInt32(parts[0]) << 24) |
                (Convert.ToUInt32(parts[1]) << 16) |
                (Convert.ToUInt32(parts[2]) << 8) |
                Convert.ToUInt32(parts[3]);

            int maskbits = Convert.ToInt32(parts[4]);
            uint mask = 0xffffffff;
            mask <<= (32 - maskbits);

            uint ipstart = ipnum & mask;
            uint ipend = ipnum | (mask ^ 0xffffffff);
            return (toip(ipstart), toip(ipend));
        }

    }
}
