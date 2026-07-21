using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Report.Filter
{
    /// <summary>
    /// Builds GraphQL queries for legacy and split standard Rules reports.
    /// </summary>
    internal static class RuleReportQueryBuilder
    {
        private const string kManagementWhereString = $@"where: {{ hide_in_gui: {{_eq: false }}
                                        mgm_id: {{_in: $mgmId }}
                                        stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}} is_pure_routing_device: {{_eq: false}} }}
                                        }} order_by: {{ mgm_name: asc }}";
        private const string kDeviceWhereStringStart = $@"where: {{ hide_in_gui: {{_eq: false }},
                                        stm_dev_typ: {{is_pure_routing_device:{{_eq:false}} }}";
        private const string kDeviceWhereStringEnd = $@"}} order_by: {{ dev_name: asc }}";
        private const string kLimitOffsetString = $@"limit: $limit
                                        offset: $offset ";

        /// <summary>
        /// Builds the legacy nested query used for tenant-filtered Rules reports and other rule report types.
        /// </summary>
        internal static string ConstructLegacyRulesQuery(DynGraphqlQuery query, string paramString, ReportTemplate filter)
        {
            return $@"
                {GetRulesFragmentDef(filter)}
                query rulesReport ({paramString})
                {{
                    management({kManagementWhereString})
                    {{
                        id: mgm_id
                        uid: mgm_uid
                        name: mgm_name
                        devices ({GetDeviceWhereFilter(filter.ReportParams.DeviceFilter)})
                        {{
                            id: dev_id
                            name: dev_name
                            uid: dev_uid
                            {query.OpenRuleBaseTable}
                                where: {{ {query.RulebaseLinkWhereStatement} }}
                            ) {{
                                linkType: stm_link_type  {{
                                    name
                                    id
                                }}
                                link_type
                                is_initial
                                is_global
                                is_section
                                gw_id
                                from_rule_id
                                from_rulebase_id
                                to_rulebase_id
                                created
                                removed
                            }}
                        }}
                        rulebases {{
                            name
                            uid
                            id
                            {query.OpenRulesTable}
                                {kLimitOffsetString}
                                where: {{ access_rule: {{_eq: true}} {query.RuleWhereStatement} }}
                                order_by: {{ rule_num_numeric: asc }} )
                            {{
                                mgm_id: mgm_id
                                {((ReportType)filter.ReportParams.ReportType == ReportType.UnusedRules ? "rule_metadatum { rule_last_hit }" : "")}
                                ...{GetRulesFragmentCall(filter)}
                            }}
                        }}
                    }}
                }}";
        }

        /// <summary>
        /// Builds the one-time management, device, and rulebase-link query for standard Rules reports.
        /// </summary>
        internal static string ConstructStandardStructureQuery(DynGraphqlQuery query, ReportTemplate filter)
        {
            string importParams = string.IsNullOrWhiteSpace(query.RulebaseLinkWhereStatement)
                ? ""
                : "$import_id_start: bigint $import_id_end: bigint";

            return $@"
                query standardRulesStructure ($mgmId: [Int!] {importParams})
                {{
                    management({kManagementWhereString})
                    {{
                        id: mgm_id
                        uid: mgm_uid
                        name: mgm_name
                        devices ({GetDeviceWhereFilter(filter.ReportParams.DeviceFilter)})
                        {{
                            id: dev_id
                            name: dev_name
                            uid: dev_uid
                            rulebase_links(
                                where: {{ {query.RulebaseLinkWhereStatement} }}
                            ) {{
                                link_type
                                is_initial
                                is_global
                                is_section
                                gw_id
                                from_rule_id
                                from_rulebase_id
                                to_rulebase_id
                                created
                                removed
                            }}
                        }}
                        rulebases {{
                            name
                            id
                        }}
                    }}
                }}";
        }

        /// <summary>
        /// Builds the paged flat-rule query used by standard Rules reports after the static rulebase graph was fetched once.
        /// </summary>
        internal static string ConstructStandardPageQuery(DynGraphqlQuery query, string paramString, ReportTemplate filter)
        {
            return $@"
                {GetRulesFragmentDef(filter)}
                query standardRulesPage ({paramString} $rulebaseIds: [Int!])
                {{
                    firewall_rule(
                        limit: $limit
                        offset: $offset
                        where: {{
                            mgm_id: {{ _in: $mgmId }}
                            rulebase_id: {{ _in: $rulebaseIds }}
                            access_rule: {{ _eq: true }}
                            {query.RuleWhereStatement}
                        }}
                        order_by: [{{ rulebase_id: asc }}, {{ rule_num_numeric: asc }}, {{ rule_id: asc }}]
                    )
                    {{
                        mgm_id: mgm_id
                        ...{GetRulesFragmentCall(filter)}
                    }}
                }}";
        }

        private static string GetRulesFragmentDef(ReportTemplate filter)
        {
            if ((ReportType)filter.ReportParams.ReportType == ReportType.AppRules)
            {
                return RuleQueries.ruleDetailsForAppRuleReportFragments;
            }
            return filter.Detailed ? RuleQueries.ruleDetailsForReportFragments : RuleQueries.ruleOverviewFragments;
        }

        private static string GetRulesFragmentCall(ReportTemplate filter)
        {
            if ((ReportType)filter.ReportParams.ReportType == ReportType.AppRules)
            {
                return "ruleDetailsForAppRuleReport";
            }
            return filter.Detailed ? "ruleDetailsForReport" : "ruleOverview";
        }

        private static string GetDeviceWhereFilter(DeviceFilter deviceFilter)
        {
            if (deviceFilter == null || deviceFilter.Managements == null)
            {
                return kDeviceWhereStringStart + kDeviceWhereStringEnd;
            }

            string deviceWhereStatement = kDeviceWhereStringStart + "_or: [{";
            bool first = true;

            foreach (ManagementSelect management in deviceFilter.Managements)
            {
                if (management.Devices == null)
                {
                    continue;
                }

                foreach (DeviceSelect device in management.Devices)
                {
                    if (!device.Selected)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        deviceWhereStatement += "}, {";
                    }
                    first = false;
                    deviceWhereStatement += $@" dev_id: {{_eq:{device.Id} }} ";
                }
            }
            return deviceWhereStatement + "}] " + kDeviceWhereStringEnd;
        }
    }
}
