using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data.Report;

namespace FWO.Report.Filter
{
    /// <summary>
    /// Builds the GraphQL queries of the change reports (access rule changes and NAT rule changes).
    /// </summary>
    internal static class ChangesReportQueryBuilder
    {
        private const string kLimitOffsetString = $@"limit: $limit
                                        offset: $offset ";

        /// <summary>
        /// Only changes of access rules are shown in the access rule change reports.
        /// </summary>
        private const string kAccessRuleChangesWhere = @"
                                        _or:[
                                                {_and: [{change_action:{_eq:""I""}}, {rule: {access_rule:{_eq:true}}}]},
                                                {_and: [{change_action:{_eq:""D""}}, {ruleByOldRuleId: {access_rule:{_eq:true}}}]},
                                                {_and: [{change_action:{_eq:""C""}}, {rule: {access_rule:{_eq:true}}}, {ruleByOldRuleId: {access_rule:{_eq:true}}}]}
                                            ]";

        /// <summary>
        /// Only changes of original NAT rules are shown in the NAT rule change report. Their translated counterparts
        /// (NAT rules without xlate_rule) are displayed as part of the original NAT rule, not as separate changes.
        /// </summary>
        private const string kNatRuleChangesWhere = @"
                                        _or:[
                                                {_and: [{change_action:{_eq:""I""}}, {rule: {nat_rule:{_eq:true}, xlate_rule:{_is_null:false}}}]},
                                                {_and: [{change_action:{_eq:""D""}}, {ruleByOldRuleId: {nat_rule:{_eq:true}, xlate_rule:{_is_null:false}}}]},
                                                {_and: [{change_action:{_eq:""C""}}, {rule: {nat_rule:{_eq:true}, xlate_rule:{_is_null:false}}}, {ruleByOldRuleId: {nat_rule:{_eq:true}, xlate_rule:{_is_null:false}}}]}
                                            ]";

        private const string kNatRule = "rule: { nat_rule: { _eq: true } }";

        /// <summary>
        /// Network objects (or users) referenced directly in the source or destination of a NAT rule (original or translated part).
        /// </summary>
        private const string kReferencedInNatRule = $@"_or: [ {{ rule_froms: {{ {kNatRule} }} }}, {{ rule_tos: {{ {kNatRule} }} }} ]";

        /// <summary>
        /// Services used in a NAT rule (original or translated part), directly or as member of a group.
        /// </summary>
        private const string kServiceUsedInNatRule = $@"_or: [
                    {{ rule_services: {{ {kNatRule} }} }}
                    {{ svcgrpFlatsBySvcgrpFlatMemberId: {{ service: {{ rule_services: {{ {kNatRule} }} }} }} }}
                ]";

        /// <summary>
        /// Builds the condition for network objects (or users) used in a NAT rule (original or translated part),
        /// directly or as member of a group.
        /// </summary>
        /// <param name="groupMembershipRelation">Relation from the element to the flat group memberships it is part of.</param>
        /// <param name="groupRelation">Relation from a flat group membership to its group.</param>
        /// <returns>The GraphQL condition.</returns>
        private static string UsedInNatRule(string groupMembershipRelation, string groupRelation)
        {
            return $"_or: [ {{ {kReferencedInNatRule} }}, {{ {groupMembershipRelation}: {{ {groupRelation}: {{ {kReferencedInNatRule} }} }} }} ]";
        }

        /// <summary>
        /// Builds the object, service and user changelog part of a change report. NAT change reports only contain the
        /// changes of objects, services and users used in NAT rules, as these change the NAT rules without a new rule version.
        /// </summary>
        /// <param name="natChanges">True for NAT change reports.</param>
        /// <returns>The changelog query part, only included if the query variable include_objects_in_changes_report is set.</returns>
        private static string GetChangelogObjectsBlock(bool natChanges)
        {
            string objectUsage = UsedInNatRule("objgrpFlatsByObjgrpFlatMemberId", "object");
            string userUsage = UsedInNatRule("usergrpFlatsByUsergrpFlatMemberId", "usr");
            string objectWhere = natChanges ? $"_or: [ {{ object: {{ {objectUsage} }} }}, {{ objectByOldObjId: {{ {objectUsage} }} }} ]" : "";
            string serviceWhere = natChanges ? $"_or: [ {{ service: {{ {kServiceUsedInNatRule} }} }}, {{ serviceByOldSvcId: {{ {kServiceUsedInNatRule} }} }} ]" : "";
            string userWhere = natChanges ? $"_or: [ {{ usr: {{ {userUsage} }} }}, {{ usrByOldUserId: {{ {userUsage} }} }} ]" : "";
            return $@"
            changelog_objects: changelog_objects(
                where: {{
                  change_type_id: {{ _eq: 3 }}
                  security_relevant: {{ _eq: true }}
                  control_id: {{ _eq: $import_id_new }}
                  {objectWhere}
                }},
                order_by: {{ control_id: asc }}
              )
            @include(if: $include_objects_in_changes_report) {{
                import: import_control {{ time: stop_time }}
                change_action
                old: objectByOldObjId {{
                    ...networkObjectDetailsChangesOld
                }}
                new: object {{
                    ...networkObjectDetailsChangesNew
                }}
            }}
            changelog_services: changelog_services(
                where: {{
                  change_type_id: {{ _eq: 3 }}
                  security_relevant: {{ _eq: true }}
                  control_id: {{ _eq: $import_id_new }}
                  {serviceWhere}
                }},
                order_by: {{ control_id: asc }}
              )
            @include(if: $include_objects_in_changes_report) {{
                import: import_control {{ time: stop_time }}
                change_action
                old: serviceByOldSvcId {{
                    ...networkServiceDetailsChangesOld
                }}
                new: service {{
                    ...networkServiceDetailsChangesNew
                }}
            }}
            changelog_users: changelog_users(
                where: {{
                  change_type_id: {{ _eq: 3 }}
                  security_relevant: {{ _eq: true }}
                  control_id: {{ _eq: $import_id_new }}
                  {userWhere}
                }},
                order_by: {{ control_id: asc }}
              )
            @include(if: $include_objects_in_changes_report) {{
                import: import_control {{ time: stop_time }}
                change_action
                old: usrByOldUserId {{
                    ...userDetailsChangesOld
                }}
                new: usr {{
                    ...userDetailsChangesNew
                }}
            }}
            ";
        }

        /// <summary>
        /// Builds the query of a change report. NAT change reports select the NAT rule changes and fetch the
        /// translated part of each rule, all other change reports select the access rule changes.
        /// </summary>
        /// <param name="query">Query whose rule filter and changelog table are used.</param>
        /// <param name="paramString">GraphQL parameter declaration of the query.</param>
        /// <param name="filter">Report template defining report type and level of detail.</param>
        /// <returns>The complete change report query.</returns>
        internal static string ConstructChangesQuery(DynGraphqlQuery query, string paramString, ReportTemplate filter)
        {
            bool natChanges = (ReportType)filter.ReportParams.ReportType == ReportType.NatChanges;
            string fragments = GetFragments(natChanges, filter.Detailed);
            string fragmentPrefix = natChanges ? "natRule" : "rule";
            string fragmentLevel = filter.Detailed ? "Details" : "Overview";
            string ruleTypeWhere = natChanges ? kNatRuleChangesWhere : kAccessRuleChangesWhere;

            return $@"
                    {fragments}
                query changeReport({paramString}){{
                    management(where: {{
                            hide_in_gui: {{_eq: false }}
                            stm_dev_typ: {{dev_typ_is_multi_mgmt: {{_eq: false}}, is_pure_routing_device: {{_eq: false}} }}
                            mgm_id: {{_in: $mgmId }}
                        }} order_by: {{mgm_name: asc}})
                    {{
                        id: mgm_id
                        name: mgm_name
                        import_controls {{
                            control_id
                            start_time
                            stop_time
                            successful_import
                            import_errors
                        }}
                        devices {{
                            id: dev_id
                            name: dev_name
                        }}
                        changelog_rules: {query.OpenChangeLogRulesTable}
                                    {kLimitOffsetString}
                                    where: {{
                                        {ruleTypeWhere}
                                        {query.RuleWhereStatement}
                                    }}
                                    order_by: {{ control_id: asc }}
                                )
                            {{
                            import: import_control {{ time: stop_time }}
                            change_action
                            old: ruleByOldRuleId {{
                                    ...{fragmentPrefix}{fragmentLevel}ChangesOld
                            }}
                            new: rule {{
                                    ...{fragmentPrefix}{fragmentLevel}ChangesNew
                            }}
                        }}
                        {GetChangelogObjectsBlock(natChanges)}
                    }}
                }}";
        }

        private static string GetFragments(bool natChanges, bool detailed)
        {
            if (natChanges)
            {
                return detailed ? RuleQueries.natRuleDetailsForChangeReportFragments : RuleQueries.natRuleOverviewForChangeReportFragments;
            }
            return detailed ? RuleQueries.ruleDetailsForChangeReportFragments : RuleQueries.ruleOverviewForChangeReportFragments;
        }
    }
}
