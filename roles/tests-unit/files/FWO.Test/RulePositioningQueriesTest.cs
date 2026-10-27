using FWO.Api.Client.Queries;
using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class RulePositioningQueriesTest
{
    [Test]
    public void ImportSelection_IsManagementScopedSuccessfulCompletedAndDeterministic()
    {
        string query = RulePositioningQueries.getLatestSuccessfulRuleImport;

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("$managementId: Int!"));
            Assert.That(query, Does.Contain("$importTypeId: Int!"));
            Assert.That(query, Does.Contain("mgm_id: { _eq: $managementId }"));
            Assert.That(query, Does.Contain("import_type_id: { _eq: $importTypeId }"));
            Assert.That(query, Does.Contain("successful_import: { _eq: true }"));
            Assert.That(query, Does.Contain("stop_time: { _is_null: false }"));
            Assert.That(query, Does.Contain("order_by: [{ stop_time: desc }, { control_id: desc }]"));
            Assert.That(query, Does.Contain("limit: 1"));
            Assert.That(query, Does.Not.Contain("start_time"));
            Assert.That(query, Does.Not.Contain("interval"));
            Assert.That(query, Does.Not.Contain("fresh"));
            Assert.That(ImportType.RULE, Is.EqualTo(1));
        });
    }

    [Test]
    public void LinkSelection_UsesFixedImportIntervalAndSourceOwnershipFields()
    {
        string query = RulePositioningQueries.getGatewayRulebaseLinksAtImport;

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("created: { _lte: $importId }"));
            Assert.That(query, Does.Contain("removed: { _gt: $importId }"));
            Assert.That(query, Does.Contain("order_by: { id: asc }"));
            Assert.That(query, Does.Contain("from_rulebase_id"));
            Assert.That(query, Does.Contain("rule_id mgm_id rulebase_id rule_create removed"));
        });
    }

    [Test]
    public void RulebaseSelection_UsesSameScopeIntervalsAndDeterministicOrdering()
    {
        string query = RulePositioningQueries.getRulebasesAtImport;

        Assert.Multiple(() =>
        {
            Assert.That(Occurrences(query, "mgm_id: { _eq: $managementId }"), Is.EqualTo(2));
            Assert.That(query, Does.Contain("created: { _lte: $importId }"));
            Assert.That(query, Does.Contain("rule_create: { _lte: $importId }"));
            Assert.That(Occurrences(query, "removed: { _gt: $importId }"), Is.EqualTo(2));
            Assert.That(query, Does.Contain("order_by: { id: asc }"));
            Assert.That(query, Does.Contain("order_by: [{ rule_num_numeric: asc }, { rule_id: asc }]"));
        });
    }

    [Test]
    public void GraphQueries_RequestOnlyStructuralContent()
    {
        string combined = string.Join(' ',
            RulePositioningQueries.getGatewayRulebaseLinksAtImport,
            RulePositioningQueries.getRulebasesAtImport);
        string[] forbiddenFields =
        [
            "rule_src", "rule_dst", "rule_services", "rule_action", "rule_from_zones",
            "rule_to_zones", "nat_rule", "rule_comment", "credentials", "rule_installon"
        ];

        Assert.That(forbiddenFields.Where(combined.Contains), Is.Empty);
    }

    [Test]
    public void CheckPointGuardQuery_UsesFixedImportAndDeterministicGuardCollections()
    {
        string query = RulePositioningQueries.getCheckPointLayerGuardsAtImport;

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("$sourceRuleIds: [bigint!]!"));
            Assert.That(query, Does.Contain("rule_id: { _in: $sourceRuleIds }"));
            Assert.That(query, Does.Contain("rule_create: { _lte: $importId }"));
            Assert.That(query, Does.Contain("rf_create: { _lte: $importId }"));
            Assert.That(query, Does.Contain("rt_create: { _lte: $importId }"));
            Assert.That(query, Does.Contain("import_created: { _lte: $importId }"));
            Assert.That(query, Does.Contain("order_by: { rule_id: asc }"));
            Assert.That(query, Does.Contain("rule_src_neg"));
            Assert.That(query, Does.Contain("rule_dst_neg"));
            Assert.That(query, Does.Contain("objgrp_flats"));
        });
    }

    private static int Occurrences(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;
}
