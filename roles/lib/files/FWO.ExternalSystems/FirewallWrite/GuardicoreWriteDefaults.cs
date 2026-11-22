using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Shipped Akamai Guardicore Segmentation calls, all of REST API v4.0 except the login, for which v3.0 is the only
    /// version. Groups are written as labels with IP criteria (dynamic IP labels): a label that does not exist yet is
    /// created with the template of <see cref="FirewallWriteOperation.LabelWrite"/>; the criteria of an existing label are
    /// replaced by the client through the bulk update (override, not append), and a label value is deleted by its id,
    /// which the client looks up. Label keys are never deleted.
    /// Rules are written to the Allow section, one rule per protocol of a connection, see <see cref="GuardicoreWriteClient"/>:
    /// <see cref="FirewallWriteOperation.RuleCreate"/> creates a rule (an existing rule of the connection with the same
    /// protocol is replaced by PUT on its path with the same body), <see cref="FirewallWriteOperation.RuleDelete"/> deletes a
    /// rule by its id, which the client looks up, and <see cref="FirewallWriteOperation.Publish"/> publishes the changed
    /// policy as a new revision.
    /// </summary>
    public static class GuardicoreWriteDefaults
    {
        /// <summary>Path of the label list, filtered by key and value to find a label.</summary>
        public const string kLabelsPath = "/api/v4.0/labels";

        /// <summary>Path of the bulk label creation.</summary>
        public const string kLabelsBulkPath = "/api/v4.0/labels/bulk";

        /// <summary>Path of the login; Guardicore offers the login in REST API v3.0 only.</summary>
        public const string kAuthenticatePath = "/api/v3.0/authenticate";

        /// <summary>Path of the segmentation rules: listed (paged) and created; with the rule id appended changed or deleted.</summary>
        public const string kRulesPath = "/api/v4.0/visibility/policy/rules";

        /// <summary>Path of the policy revisions; a new revision publishes the changed rules.</summary>
        public const string kRevisionsPath = "/api/v4.0/visibility/policy/revisions";

        /// <summary>Section of the policy FWO writes its rules to.</summary>
        public const string kAllowSection = "ALLOW";

        /// <summary>
        /// Name of the rulebase the importer gives the rules of the Allow section: section number and name, followed by the
        /// ruleset (e.g. "04 Allow APP-1234"), see get_rulebase_name of the Guardicore importer.
        /// </summary>
        public const string kAllowRulebaseName = "04 Allow";

        /// <summary>Whether an imported rulebase holds rules of the Allow section.</summary>
        public static bool IsAllowRulebase(string rulebaseName)
        {
            ArgumentNullException.ThrowIfNull(rulebaseName);
            return rulebaseName == kAllowRulebaseName || rulebaseName.StartsWith(kAllowRulebaseName + " ", StringComparison.Ordinal);
        }

        private const string kLabelCreateBody = """[{"key":"@@LABEL_KEY@@","value":"@@LABEL_VALUE@@","criteria":@@LABEL_CRITERIA@@}]""";

        /// <summary>
        /// A rule of one protocol in the Allow section: the ruleset is the app id, the comment the connection marker. The
        /// sides name labels by "key: value", which the client replaces by the label ids.
        /// </summary>
        private const string kRuleBody =
            """{"section_position":"ALLOW","action":"@@RULE_ACTION@@","enabled":true,"ruleset_name":"@@RULE_NAME@@","comments":"@@RULE_COMMENT@@","ip_protocols":["@@RULE_PROTOCOL@@"],"ports":@@RULE_PORTS@@,"port_ranges":@@RULE_PORT_RANGES@@,"source":@@RULE_SOURCES@@,"destination":@@RULE_DESTINATIONS@@}""";

        private const string kPublishBody = """{"comments":"FWO ticket @@TICKET_ID@@, task @@TASK_NUMBER@@"}""";

        /// <summary>Shipped calls per operation.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.LabelWrite, new($"POST {kLabelsBulkPath}", kLabelCreateBody) },
                { FirewallWriteOperation.LabelDelete, new($"DELETE {kLabelsPath}/@@LABEL_ID@@", "") },
                { FirewallWriteOperation.RuleCreate, new($"POST {kRulesPath}", kRuleBody) },
                { FirewallWriteOperation.RuleDelete, new($"DELETE {kRulesPath}/@@RULE_ID@@", "") },
                { FirewallWriteOperation.Publish, new($"POST {kRevisionsPath}", kPublishBody) }
            };
    }
}
