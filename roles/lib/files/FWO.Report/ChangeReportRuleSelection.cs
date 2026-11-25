using FWO.Data;

namespace FWO.Report
{
    /// <summary>
    /// Converts the rule changes selected in a change report into the rule versions shown in the rule tab of the
    /// right sidebar: the new version of an added rule, the old version of a deleted rule and both versions of a
    /// modified rule.
    /// </summary>
    public static class ChangeReportRuleSelection
    {
        private const char kAdded = 'I';
        private const char kDeleted = 'D';
        private const char kModified = 'C';

        /// <summary>
        /// Returns copies of the rule versions of the selected changes, labelled for the rule tab.
        /// </summary>
        /// <param name="selectedChanges">Rule changes selected in the change report.</param>
        /// <param name="beforeChangeLabel">Label of the old version of a modified rule.</param>
        /// <param name="afterChangeLabel">Label of the new version of a modified rule.</param>
        /// <returns>The rule versions, in the order of the selection.</returns>
        public static List<Rule> GetRuleVersions(IEnumerable<RuleChange> selectedChanges, string beforeChangeLabel, string afterChangeLabel)
        {
            List<Rule> ruleVersions = [];
            foreach (RuleChange change in selectedChanges)
            {
                switch (change.ChangeAction)
                {
                    case kAdded:
                        AddVersion(ruleVersions, change.NewRule, change.RulebaseName, null);
                        break;
                    case kDeleted:
                        AddVersion(ruleVersions, change.OldRule, change.RulebaseName, null);
                        break;
                    case kModified:
                        AddVersion(ruleVersions, change.OldRule, change.RulebaseName, beforeChangeLabel);
                        AddVersion(ruleVersions, change.NewRule, change.RulebaseName, afterChangeLabel);
                        break;
                }
            }
            return ruleVersions;
        }

        private static void AddVersion(List<Rule> ruleVersions, Rule? rule, string managementName, string? versionLabel)
        {
            if (rule == null || rule.Id <= 0 || ruleVersions.Any(r => r.Id == rule.Id))
            {
                return;
            }
            Rule version = new(rule)
            {
                RulebaseName = string.IsNullOrEmpty(versionLabel) ? managementName : $"{managementName} ({versionLabel})"
            };
            ruleVersions.Add(version);
        }
    }
}
