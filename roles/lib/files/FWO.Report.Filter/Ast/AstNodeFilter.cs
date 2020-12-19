using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using FWO.Logging;
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

            functions["ReportType"] = this.ExtractReportTypeQuery;
            functions["Time"] = this.ExtractTimeQuery;

            // functions["Disabled"] = this.ExtractDisabled;
            // functions["SourceNegated"] = this.ExtractSourceNegated;
            // functions["DestinationNegated"] = this.ExtractDestinationNegated;
            // functions["ServiceNegated"] = this.ExtractServiceNegated;
            
            functions["Source"] = this.ExtractSourceQuery;
            functions["Destination"] = this.ExtractDestinationQuery;
            functions["Action"] = this.ExtractActionQuery;
            functions["Service"] = this.ExtractServiceQuery;
            functions["DestinationPort"] = this.ExtractDestinationPort;
            functions["Protocol"] = this.ExtractProtocolQuery;
            functions["Management"] = this.ExtractManagementQuery;
            functions["Gateway"] = this.ExtractGatewayQuery;

            // call the method matching the Name of the current node to build the graphQL query
            query = functions[Name.ToString()](query);

            return;
        }

        private DynGraphqlQuery ExtractTimeQuery(DynGraphqlQuery query)
        {
            if (query.ReportType == "rules" || query.ReportType == "statistics")
            {
                query.ruleWhereStatement +=
                    $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                    $"importControlByRuleLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                query.nwObjWhereStatement +=
                    $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                    $"importControlByObjLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                query.svcObjWhereStatement +=
                    $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                    $"importControlBySvcLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                query.userObjWhereStatement +=
                    $"import_control: {{ control_id: {{_lte: $relevantImportId }} }}, " +
                    $"importControlByUserLastSeen: {{ control_id: {{_gte: $relevantImportId }} }}";
                query.ReportTime = Value;
            }
            else if (query.ReportType == "changes")
            {
                string start = "";
                string stop = "";
                (start, stop) = resolveTimeRange(Value);
                query.QueryVariables["start"] = start;
                query.QueryVariables["stop"] = stop;
                query.QueryParameters.Add("$start: timestamp! ");
                query.QueryParameters.Add("$stop: timestamp! ");

                query.ruleWhereStatement += $@"
                    _and: [
                        {{ import_control: {{ stop_time: {{ _gte: $start }} }} }}
                        {{ import_control: {{ stop_time: {{ _lte: $stop }} }} }}
                    ]
                    change_type_id: {{ _eq: 3 }}
                    security_relevant: {{ _eq: true }}";
            } else {
                Log.WriteError("Filter", $"Undefined Report Type found: {query.ReportType}");
            }
            // todo: deal with time ranges for changes report type
            return query;
        }
        
        private DynGraphqlQuery ExtractReportTypeQuery(DynGraphqlQuery query)
        {
            query.ReportType = Value;
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
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            if (isCidr(Value))  // filtering for ip addresses
                query = ExtractIpFilter(query, location: "src", locationTable: "rule_froms");
            else // string search against src obj name
            {
                string QueryVarName = "src" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = $"%{Value}%";
                query.QueryParameters.Add($"${QueryVarName}: String! ");
                query.ruleWhereStatement += $"rule_froms: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
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
                query.ruleWhereStatement += $"rule_tos: {{ object: {{ objgrp_flats: {{ objectByObjgrpFlatMemberId: {{ obj_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            }
            return query;
        }

        private DynGraphqlQuery ExtractServiceQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "svc" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.ruleWhereStatement += $"rule_services: {{service: {{svcgrp_flats: {{serviceBySvcgrpFlatMemberId: {{svc_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractActionQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "action" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.ruleWhereStatement += $"rule_action: {{ {QueryOperation}: ${QueryVarName} }}";
            return query;
        }
        private DynGraphqlQuery ExtractProtocolQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "proto" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.ruleWhereStatement += $"rule_services: {{service: {{stm_ip_proto: {{ip_proto_name: {{ {QueryOperation}: ${QueryVarName} }} }} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractManagementQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "mgmtName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.ruleWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            query.nwObjWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            query.svcObjWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            query.userObjWhereStatement += $"management: {{mgm_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            return query;
        }
        private DynGraphqlQuery ExtractGatewayQuery(DynGraphqlQuery query)
        {
            string QueryOperation = SetQueryOpString(Operator, Name, Value);
            string QueryVarName = "gwName" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: String! ");
            query.QueryVariables[QueryVarName] = $"%{Value}%";
            query.ruleWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.nwObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.svcObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";
            // query.userObjWhereStatement += $"device: {{dev_name : {{{QueryOperation}: ${QueryVarName} }} }}";

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
            query.ruleWhereStatement += " _or: [";
            query.ruleWhereStatement += string.Join(", ", searchParts);
            query.ruleWhereStatement += "]";

            return query;
        }

        private DynGraphqlQuery ExtractDestinationPort(DynGraphqlQuery query)
        {
            string QueryVarName = "dport" + query.parameterCounter++;

            query.QueryParameters.Add($"${QueryVarName}: Int! ");
            query.QueryVariables[QueryVarName] = Value;

            query.ruleWhereStatement +=
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
                if (cidr_str.IndexOf("/") == cidr_str.Length - 1) // wrong format (/ at the end, fixing this by adding 32 mask)
                {
                    cidr_str += "32";
                }
            }
            return cidr_str;
        }

        private static bool isCidr(string cidr)
        {
            try
            {
                // IPV4 only:

                string[] IPA = sanitizeIp(cidr).Split('/');
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
        private (string, string) resolveTimeRange(string timeRange)
        {
            string start = "";
            string stop = "";
            DateTime now = DateTime.Now;
            string currentTime = (string)DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string currentYear = (string)DateTime.Now.ToString("yyyy");
            string currentMonth = (string)DateTime.Now.ToString("MM");
            string currentDay = (string)DateTime.Now.ToString("dd");
            DateTime startOfCurrentMonth = new DateTime(Convert.ToInt16(currentYear), Convert.ToInt16(currentMonth), 1);
            DateTime startOfNextMonth = startOfCurrentMonth.AddMonths(1);
            DateTime startOfPrevMonth = startOfCurrentMonth.AddMonths(-1);

            switch (timeRange)
            {
                // todo: add today, yesterday, this week, last week
                case "last year":
                    start = $"{(Convert.ToInt16(currentYear) - 1).ToString()}-01-01";
                    stop = $"{Convert.ToInt16(currentYear).ToString()}-01-01";
                    break;
                case "this year":
                    start = $"{(Convert.ToInt16(currentYear)).ToString()}-01-01";
                    stop = $"{(Convert.ToInt16(currentYear) + 1).ToString()}-01-01";
                    break;
                case "this month":
                    start = startOfCurrentMonth.ToString("yyyy-MM-dd");
                    stop = startOfNextMonth.ToString("yyyy-MM-dd");
                    break;
                case "last month":
                    start = startOfPrevMonth.ToString("yyyy-MM-dd");
                    stop = startOfCurrentMonth.ToString("yyyy-MM-dd");
                    break;
                default:
                    string[] times = timeRange.Split('-');
                    if (times.Length == 2)
                    {
                        start = times[0];
                        stop = times[1];
                        if (start != Convert.ToDateTime(start).ToString() || stop != Convert.ToDateTime(stop).ToString())
                            throw new Exception($"Error: wrong time range format");
                    }
                    else
                    {
                        throw new Exception($"Error: wrong time range format");
                    }
                    break;
            }
            return (start, stop);
        }

    }
}
