using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FWO.Report.Filter.Ast
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
            if (Value == "now")    // filtering "now"
                query.RuleWhereQuery += $"active: {{ _eq: true }} ";
            else
            {
                string QueryVarName = "time" + query.parameterCounter++;
                query.RuleWhereQuery +=
                    $"import_control: {{ stop_time: {{_lte: ${QueryVarName} }} }}, " +
                    $"importControlByRuleLastSeen: {{ stop_time: {{_gte: ${QueryVarName} }} }}";
                // TODO: fix report times > last import with a change
                // ruleFieldNames.Add("_or: [{active: {_eq: true}, {importControlByRuleLastSeen: { stop_time: {_gte");
                query.QueryParameters.Add($"${QueryVarName}: timestamp! ");
                query.QueryVariables[QueryVarName] = $"{Value}";
            }
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
                ipFilterString = $"obj_ip: {{ _eq: ${QueryVarName} }}";
            }
            else // ip filter is a subnet with /xy
            {
                string QueryVarName1 = $"{location}IpLow" + query.parameterCounter;
                string QueryVarName2 = $"{location}IpHigh" + query.parameterCounter++;
                query.QueryVariables[QueryVarName1] = firstIp;
                query.QueryVariables[QueryVarName2] = lastIp;
                query.QueryParameters.Add($"${QueryVarName1}: cidr! ");
                query.QueryParameters.Add($"${QueryVarName2}: cidr! ");
                // todo: can we also implement comparison with subnet objects?
                // currently the following match only works if the obj_ip is fully included in the subnet filter
                ipFilterString =
                     $@"    _and: 
                                [ 
                                    {{ obj_ip: {{ _gte: ${QueryVarName1} }} }}
                                    {{ obj_ip: {{ _lte: ${QueryVarName2} }} }}
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
            string QueryOperation = SetQueryOpString(Operator, Name);

            if (isCidr(Value))  // filtering for ip addresses
            {
                string location = "src";
                string locationTable = "rule_froms";
                query = ExtractIpFilter(query, location, locationTable);
            }
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
            string QueryOperation = SetQueryOpString(Operator, Name);

            if (isCidr(Value))  // filtering for ip addresses
            {
                string location = "dst";
                string locationTable = "rule_tos";
                query = ExtractIpFilter(query, location, locationTable);
            }
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
            string QueryOperation = SetQueryOpString(Operator, Name);
            string QueryVarName = "svc" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"rule_services: {{service: {{svcgrp_flats: {{serviceBySvcgrpFlatMemberId: {{svc_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractActionQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name);
            string QueryVarName = "action" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"rule_action: {{ {QueryOperation}: ${QueryVarName} }}";
            return query;
        }
        private DynGraphqlQuery ExtractProtocolQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name);
            string QueryVarName = "proto" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"rule_services: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractManagementQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name);
            string QueryVarName = "mgmtName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractGatewayQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name);
            string QueryVarName = "gwName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.RuleWhereQuery += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";

            return query;
        }
        private DynGraphqlQuery ExtractFullTextQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name);
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

        private string SetQueryOpString(TokenKind Operator, TokenKind Name)
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
