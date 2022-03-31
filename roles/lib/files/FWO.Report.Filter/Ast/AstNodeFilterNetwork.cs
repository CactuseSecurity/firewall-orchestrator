using NetTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
            return IPAddressRange.TryParse(cidr, out IPAddressRange range);

            //try
            //{
            //    // IPV4 only:

            //    string[] IPA = SanitizeIp(cidr).Split('/');
            //    if (IPA.Length == 2)
            //    {
            //        if (IPAddress.TryParse(IPA[0], out _))
            //        {
            //            if (int.TryParse(IPA[1], out int bitsInMask) == false)
            //                return false;
            //            else if (bitsInMask >= 0 && bitsInMask <= 32)
            //                return true;
            //        }
            //    }
            //    else if (IPA.Length == 1) // no / in string, simple IP
            //    {
            //        if (IPAddress.TryParse(cidr, out _))
            //        {
            //            return true;
            //        }
            //    }
            //    // TODO: IPv6 handling
            //    return false;
            //}
            //catch (Exception)
            //{
            //    Log.WriteDebug("Ip Address Parsing", "An exception occured while trying to parse an Ip address.");
            //    return false;
            //}
        }

        private static string ToIp(uint ip)
        {
            // TODO: IPv6 handling
            return string.Format("{0}.{1}.{2}.{3}", ip >> 24, (ip >> 16) & 0xff, (ip >> 8) & 0xff, ip & 0xff);
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

        private DynGraphqlQuery ExtractIpFilter(DynGraphqlQuery query, string location, string locationTable)
        {
            string filterIP = SanitizeIp(Value.Text);
            (string firstFilterIp, string lastFilterIp) = GetFirstAndLastIp(filterIP);
            string ipFilterString;

            if (firstFilterIp == lastFilterIp) // optimization, just need a single comparison if searching for single ip
            {
                string QueryVarName = $"{location}Ip" + query.parameterCounter++;
                query.QueryVariables[QueryVarName] = firstFilterIp;
                query.QueryParameters.Add($"${QueryVarName}: cidr! ");
                // checking if single filter ip is part of a cidr subnet (or is a direct match for a single ip)
                ipFilterString = $@" _or: [
                            {{
                                _and: 
                                        [ 
                                            {{ obj_ip: {{ _gte: ${QueryVarName} }} }}
                                            {{ obj_ip: {{ _lte: ${QueryVarName} }} }}
                                        ]
                            }} 
                            {{
                                _and: 
                                        [ 
                                            {{ network_object_limits: {{ first_ip: {{ _lte: ${QueryVarName} }} }} }}
                                            {{ network_object_limits: {{ last_ip: {{ _gte: ${QueryVarName} }} }} }}
                                        ]
                            }} 
                            ]";
            }
            else // ip filter is a subnet with /xy
            {
                string QueryVarNameNet0 = $"{location}IpNet" + query.parameterCounter;
                string QueryVarNameFirst1 = $"{location}IpLow" + query.parameterCounter;
                string QueryVarNameLast2 = $"{location}IpHigh" + query.parameterCounter++;
                query.QueryVariables[QueryVarNameNet0] = filterIP;
                query.QueryVariables[QueryVarNameFirst1] = firstFilterIp;
                query.QueryVariables[QueryVarNameLast2] = lastFilterIp;
                query.QueryParameters.Add($"${QueryVarNameNet0}: cidr! ");
                query.QueryParameters.Add($"${QueryVarNameFirst1}: cidr! ");
                query.QueryParameters.Add($"${QueryVarNameLast2}: cidr! ");
                // covering various cases: 
                // 1 - current ip is fully contained in filter ip range
                // 2 - current ip overlaps with lower boundary of filter ip range
                // 3 - current ip overlaps with upper boundary of filter ip range
                // 4 - current ip fully contains filter ip range - does not work
                ipFilterString =
                     $@" _or: [
                            {{ obj_ip: {{ _eq: ${QueryVarNameNet0} }} }}
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _gte: ${QueryVarNameFirst1} }} }}
                                        {{ obj_ip: {{ _lte: ${QueryVarNameLast2} }} }}
                                    ]
                            }}
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _lte: ${QueryVarNameFirst1} }} }}
                                        {{ obj_ip: {{ _gte: ${QueryVarNameFirst1} }} }}
                                    ]
                            }} 
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _lte: ${QueryVarNameLast2} }} }}
                                        {{ obj_ip: {{ _gte: ${QueryVarNameLast2} }} }}
                                    ]
                            }}
                            {{ _and: 
                                    [ 
                                        {{ obj_ip: {{ _lte: ${QueryVarNameFirst1} }} }}
                                        {{ obj_ip: {{ _gte: ${QueryVarNameLast2} }} }}
                                    ]
                            }}
                            {{
                                _and: 
                                        [ 
                                            {{ network_object_limits: {{ first_ip: {{ _lte: ${QueryVarNameFirst1} }} }} }}
                                            {{ network_object_limits: {{ last_ip: {{ _gte: ${QueryVarNameLast2} }} }} }}
                                        ]
                            }} 
                            {{
                                _and: 
                                        [ 
                                            {{ network_object_limits: {{ first_ip: {{ _lte: ${QueryVarNameFirst1} }} }} }}
                                            {{ network_object_limits: {{ last_ip: {{ _gte: ${QueryVarNameLast2} }} }} }}
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
    }
}
