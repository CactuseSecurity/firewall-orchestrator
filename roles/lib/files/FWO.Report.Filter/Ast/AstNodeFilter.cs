using System.Net;

namespace FWO.Report.Filter.Ast
{
    class AstNodeFilter : AstNode
    {
        public Token Name { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Operator { get; set; } = new Token(new Range(), "", TokenKind.Value);
        public Token Value { get; set; } = new Token(new Range(), "", TokenKind.Value);
        private List<string>? ruleFieldNames { get; set; }
        private int queryLevel { get; set; }

        public override void Extract(ref DynGraphqlQuery query, ReportType? reportType)
        {
            switch (Name.Kind)
            {
                case TokenKind.Disabled:
                    //ExtractDisabledQuery(query);
                    throw new NotSupportedException("Token of type \"Disabled\" is currently not supported.");
                case TokenKind.SourceNegated:
                    // ExtractSourceNegatedQuery(query);
                    throw new NotSupportedException("Token of type \"SourceNegated\" is currently not supported.");
                case TokenKind.DestinationNegated:
                    // ExtractDestinationNegatedQuery(query);
                    throw new NotSupportedException("Token of type \"DestinationNegated\" is currently not supported.");
                case TokenKind.ServiceNegated:
                    // ExtractServiceNegatedQuery(query);
                    throw new NotSupportedException("Token of type \"ServiceNegated\" is currently not supported.");


                // "xy" and "FullText=xy" are the same filter
                case TokenKind.FullText:
                case TokenKind.Value:
                    ExtractFullTextQuery(query);
                    break;
                case TokenKind.Source:
                    ExtractSourceQuery(query);
                    break;
                case TokenKind.Destination:
                    ExtractDestinationQuery(query);         
                    break;
                case TokenKind.Action:
                    ExtractActionQuery(query);
                    break;
                case TokenKind.Service:
                    ExtractServiceQuery(query);
                    break;
                case TokenKind.DestinationPort:
                    ExtractDestinationPort(query);
                    break;
                case TokenKind.Protocol:
                    ExtractProtocolQuery(query);
                    break;
                case TokenKind.Management:
                    ExtractManagementQuery(query);
                    break;
                case TokenKind.Gateway:
                    ExtractGatewayQuery(query);
                    break;
                case TokenKind.Remove:
                    ExtractRemoveQuery(query);
                    break;
                case TokenKind.RecertDisplay:
                    ExtractRecertDisplay(query);
                    break;
                default:
                    throw new NotSupportedException($"### Compiler Error: Found unexpected and unsupported filter token: \"{Name}\" ###");
            }
        }

        private DynGraphqlQuery ExtractIpFilter(DynGraphqlQuery query, string location, string locationTable)
        {
            string filterIP = SanitizeIp(Value.Text);
            (string firstIp, string lastIp) = GetFirstAndLastIp(filterIP);
            string ipFilterString = "";

            if (firstIp == lastIp) // optimization, just need a single comparison if searching for single ip
            {
                string QueryVarName = $"{location}Ip" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = firstIp;
                query.QueryParameters.Add($"${QueryVarName}: cidr! ");
                // checking if single filter ip is part of a cidr subnet (or is a direct match for a single ip)
                ipFilterString = $@" _and: 
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

        private DynGraphqlQuery ExtractSourceQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            if (IsCidr(Value.Text))  // filtering for ip addresses
                query = ExtractIpFilter(query, location: "src", locationTable: "rule_froms");
            else // string search against src obj name
            {
                string QueryVarName = "src" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
                query.QueryParameters.Add($"${QueryVarName}: String! ");
                query.ruleWhereStatement += $"rule_froms: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            }
            return query;
        }
        private DynGraphqlQuery ExtractDestinationQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            if (IsCidr(Value.Text))  // filtering for ip addresses
                query = ExtractIpFilter(query, location: "dst", locationTable: "rule_tos");
            else // string search against dst obj name
            {
                string QueryVarName = "dst" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
                query.QueryParameters.Add($"${QueryVarName}: String! ");
                query.ruleWhereStatement += $"rule_tos: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            }
            return query;
        }

        private DynGraphqlQuery ExtractServiceQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            string QueryVarName = "svc" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
            query.ruleWhereStatement += $"rule_services: {{service: {{svcgrp_flats: {{serviceBySvcgrpFlatMemberId: {{svc_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractActionQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            string QueryVarName = "action" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
            query.ruleWhereStatement += $"rule_action: {{ {QueryOperation}: ${QueryVarName} }}";
            return query;
        }
        private DynGraphqlQuery ExtractProtocolQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            string QueryVarName = "proto" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
            query.ruleWhereStatement += $"rule_services: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractManagementQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            string QueryVarName;

            if (int.TryParse(Value.Text, out int _)) // dealing with mgm_id filter
            {
                QueryVarName = "mgmtId" + query.parameterCounter++;
                query.QueryParameters.Add($"${QueryVarName}: Int! ");
                query.QueryVariables[QueryVarName] = Value;
                query.ruleWhereStatement += $"management: {{mgm_id : {{{QueryOperation}: ${QueryVarName} }} }}";
                query.nwObjWhereStatement += $"management: {{mgm_id : {{{QueryOperation}: ${QueryVarName} }} }}";
                query.svcObjWhereStatement += $"management: {{mgm_id : {{{QueryOperation}: ${QueryVarName} }} }}";
                query.userObjWhereStatement += $"management: {{mgm_id : {{{QueryOperation}: ${QueryVarName} }} }}";
            }
            else
            {
                QueryVarName = "mgmtName" + query.parameterCounter++;
                query.QueryParameters.Add($"${QueryVarName}: String! ");
                query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
                query.ruleWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
                query.nwObjWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
                query.svcObjWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
                query.userObjWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            }
            return query;
        }
        private DynGraphqlQuery ExtractGatewayQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            string QueryVarName = "gwName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value.Text}%";
            query.ruleWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.nwObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.svcObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.userObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";

            return query;
        }
        private DynGraphqlQuery ExtractFullTextQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value.Text);
            string QueryVarName = "fullTextFilter" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value.Text}%";

            ruleFieldNames = new List<string>() { "rule_src", "rule_dst", "rule_svc", "rule_action" };  // TODO: add comment later
            List<string> searchParts = new List<string>();
            foreach (string field in ruleFieldNames)
                searchParts.Add($"{{{field}: {{{QueryOperation}: ${QueryVarName} }} }} ");
            query.ruleWhereStatement += " _or: [";
            query.ruleWhereStatement += string.Join(", ", searchParts);
            query.ruleWhereStatement += "]";

            return query;
        }

        private DynGraphqlQuery ExtractDestinationPort(DynGraphqlQuery query)
        {
            string QueryVarName = "dport" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: Int! ");
            query.QueryVariables[QueryVarName] = Value.Text;

            query.ruleWhereStatement +=
                " rule_services: { service: { svcgrp_flats: { service: { svc_port: {_lte" +
                ": $" + QueryVarName + "}, svc_port_end: {_gte: $" + QueryVarName + "} } } } }";
            return query;
        }

        private DynGraphqlQuery ExtractRemoveQuery(DynGraphqlQuery query)
        {
            string QueryVarName = "remove" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: Boolean ");
            query.QueryVariables[QueryVarName] = $"{Value.Text}";
            query.ruleWhereStatement += $"rule_metadatum: {{rule_to_be_removed: {{ _eq: ${QueryVarName} }}}}";
            return query;
        }

        private DynGraphqlQuery ExtractRecertDisplay(DynGraphqlQuery query)
        {
            string QueryVarName = "refdate" + query.parameterCounter++;
            query.QueryParameters.Add($"${QueryVarName}: timestamp! ");
            string refDate = DateTime.Now.AddDays(-Convert.ToInt16(Value.Text)).ToString("yyyy-MM-dd HH:mm:ss");
            query.QueryVariables[QueryVarName] = refDate;

            query.ruleWhereStatement += $@"
                _or: [
                        {{ rule_metadatum: {{ rule_last_certified: {{ _lte: ${QueryVarName} }} }} }}
                        {{ _and:[ 
                                    {{ rule_metadatum: {{ rule_last_certified: {{ _is_null: true }} }} }}
                                    {{ rule_metadatum: {{ rule_created: {{ _lte: ${QueryVarName} }} }} }}
                                ]
                        }}
                    ]";
            return query;
        }

        private static string SetQueryOpString(Token @operator, Token filter, string value)
        {
            string operation;
            switch (@operator.Kind)
            {
                case TokenKind.EQ:
                    if (filter.Kind == TokenKind.DestinationPort)
                        operation = "_eq";
                    else if ((filter.Kind == TokenKind.Source && IsCidr(value)) || filter.Kind == TokenKind.DestinationPort)
                        operation = "_eq";
                    else if (filter.Kind == TokenKind.Management && int.TryParse(value, out int _))
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

        private static string SanitizeIp(string cidr_str)
        {
            IPAddress? ip;
            if (IPAddress.TryParse(cidr_str, out ip))
            {
                if (ip != null)
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
            return cidr_str;
        }

        private static bool IsCidr(string cidr)
        {
            try
            {
                // IPV4 only:

                string[] IPA = SanitizeIp(cidr).Split('/');
                if (IPA.Length == 2)
                {
                    if (IPAddress.TryParse(IPA[0], out _))
                    {
                        if (int.TryParse(IPA[1], out int bitsInMask) == false)
                            return false;
                        else if (bitsInMask >= 0 && bitsInMask <= 32)
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
            catch (Exception)
            {
                Logging.Log.WriteDebug("Ip Address Parsing", "An exception occured while trying to parse an Ip address.");
                return false;
            }
        }

        private static string ToIp(uint ip)
        {
            // TODO: IPv6 handling
            return String.Format("{0}.{1}.{2}.{3}", ip >> 24, (ip >> 16) & 0xff, (ip >> 8) & 0xff, ip & 0xff);
        }

        private static (string, string) GetFirstAndLastIp(string cidr)
        {
            // TODO: IPv6 handling
            string[] parts = SanitizeIp(cidr).Split('.', '/');

            uint ipnum = (Convert.ToUInt32(parts[0]) << 24) |
                (Convert.ToUInt32(parts[1]) << 16) |
                (Convert.ToUInt32(parts[2]) << 8) |
                Convert.ToUInt32(parts[3]);

            int maskbits = Convert.ToInt32(parts[4]);
            uint mask = 0xffffffff;
            mask <<= (32 - maskbits);

            uint ipstart = ipnum & mask;
            uint ipend = ipnum | (mask ^ 0xffffffff);
            return (ToIp(ipstart), ToIp(ipend));
        }
    }
}
