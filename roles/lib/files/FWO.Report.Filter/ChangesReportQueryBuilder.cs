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

        private const string kRuleFromCreate = "rf_create";
        private const string kRuleToCreate = "rt_create";
        private const string kRuleServiceCreate = "rs_create";
        private const string kGroupMembershipCreate = "import_created";

        /// <summary>
        /// The old version of a changed or deleted element has its references closed in the import of the change.
        /// So it must have been referenced up to this import (or beyond), not only before it. Its references were
        /// necessarily created before the change, so the create column is not checked.
        /// </summary>
        private const string kReferenceUntilChangeImport = "_or: [ { removed: { _is_null: true } }, { removed: { _gte: $import_id_new } } ]";

        /// <summary>
        /// Rule references and group memberships are never deleted by the import, only closed (removed set).
        /// The new version of a changed element must be referenced at the import of the change: the reference was created
        /// in or before this import and was not closed up to it. Reports over past periods must not depend on the current usage.
        /// </summary>
        /// <param name="createColumn">Column holding the import that created the reference or group membership.</param>
        /// <returns>The GraphQL condition.</returns>
        private static string ReferenceAtChangeImport(string createColumn)
        {
            return $"{createColumn}: {{ _lte: $import_id_new }}, _or: [ {{ removed: {{ _is_null: true }} }}, {{ removed: {{ _gt: $import_id_new }} }} ]";
        }

        /// <summary>
        /// Builds the condition for network objects (or users) referenced directly in the source or destination
        /// of a NAT rule (original or translated part).
        /// </summary>
        /// <param name="referenceValidity">Builds the condition restricting the references to the relevant ones from the create column.</param>
        /// <returns>The GraphQL condition.</returns>
        private static string ReferencedInNatRule(Func<string, string> referenceValidity)
        {
            string fromValidity = referenceValidity(kRuleFromCreate);
            string toValidity = referenceValidity(kRuleToCreate);
            return $"_or: [ {{ rule_froms: {{ {kNatRule}, {fromValidity} }} }}, {{ rule_tos: {{ {kNatRule}, {toValidity} }} }} ]";
        }

        /// <summary>
        /// Builds the condition for network objects (or users) used in a NAT rule (original or translated part),
        /// directly or as member of a group.
        /// </summary>
        /// <param name="groupMembershipRelation">Relation from the element to the flat group memberships it is part of.</param>
        /// <param name="groupRelation">Relation from a flat group membership to its group.</param>
        /// <param name="referenceValidity">Builds the condition restricting references and group memberships to the relevant ones from the create column.</param>
        /// <returns>The GraphQL condition.</returns>
        private static string UsedInNatRule(string groupMembershipRelation, string groupRelation, Func<string, string> referenceValidity)
        {
            string referencedInNatRule = ReferencedInNatRule(referenceValidity);
            string membershipValidity = referenceValidity(kGroupMembershipCreate);
            return $"_or: [ {{ {referencedInNatRule} }}, {{ {groupMembershipRelation}: {{ {membershipValidity}, {groupRelation}: {{ {referencedInNatRule} }} }} }} ]";
        }

        /// <summary>
        /// Builds the condition for services used in a NAT rule (original or translated part), directly or as member of a group.
        /// </summary>
        /// <param name="referenceValidity">Builds the condition restricting references and group memberships to the relevant ones from the create column.</param>
        /// <returns>The GraphQL condition.</returns>
        private static string ServiceUsedInNatRule(Func<string, string> referenceValidity)
        {
            string referencedInNatRule = $"rule_services: {{ {kNatRule}, {referenceValidity(kRuleServiceCreate)} }}";
            string membershipValidity = referenceValidity(kGroupMembershipCreate);
            return $"_or: [ {{ {referencedInNatRule} }}, {{ svcgrpFlatsBySvcgrpFlatMemberId: {{ {membershipValidity}, service: {{ {referencedInNatRule} }} }} }} ]";
        }

        /// <summary>
        /// Builds the condition for changes of elements used in a NAT rule: the new version must be used at the import
        /// of the change, the old version must have been used up to the import of the change.
        /// </summary>
        /// <param name="newRelation">Relation from the changelog entry to the new element version.</param>
        /// <param name="oldRelation">Relation from the changelog entry to the old element version.</param>
        /// <param name="usedInNatRule">Builds the usage condition for a given reference validity.</param>
        /// <returns>The GraphQL condition.</returns>
        private static string ChangeUsedInNatRule(string newRelation, string oldRelation, Func<Func<string, string>, string> usedInNatRule)
        {
            string newVersionUsage = usedInNatRule(ReferenceAtChangeImport);
            string oldVersionUsage = usedInNatRule(_ => kReferenceUntilChangeImport);
            return $"_or: [ {{ {newRelation}: {{ {newVersionUsage} }} }}, {{ {oldRelation}: {{ {oldVersionUsage} }} }} ]";
        }

        /// <summary>
        /// Builds the object, service and user changelog part of a change report. NAT change reports only contain the
        /// changes of objects, services and users used in NAT rules, as these change the NAT rules without a new rule version.
        /// </summary>
        /// <param name="natChanges">True for NAT change reports.</param>
        /// <returns>The changelog query part, only included if the query variable include_objects_in_changes_report is set.</returns>
        private static string GetChangelogObjectsBlock(bool natChanges)
        {
            string objectWhere = natChanges ? ChangeUsedInNatRule("object", "objectByOldObjId",
                validity => UsedInNatRule("objgrpFlatsByObjgrpFlatMemberId", "object", validity)) : "";
            string serviceWhere = natChanges ? ChangeUsedInNatRule("service", "serviceByOldSvcId", ServiceUsedInNatRule) : "";
            string userWhere = natChanges ? ChangeUsedInNatRule("usr", "usrByOldUserId",
                validity => UsedInNatRule("usergrpFlatsByUsergrpFlatMemberId", "usr", validity)) : "";
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
