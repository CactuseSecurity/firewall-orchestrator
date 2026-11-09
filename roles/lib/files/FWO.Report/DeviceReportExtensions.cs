using FWO.Data.Report;

namespace FWO.Report
{
    public static class DeviceReportExtensions
    {
        /// <summary>
        /// Returns the html id of the rule tree of a device (or of a start rulebase in management rulebases view),
        /// used as link target by reference rows of other rule trees.
        /// </summary>
        public static string GetRuleTreeAnchorId(int managementId, int deviceId)
        {
            return $"ruletree-{managementId}-{deviceId}";
        }

        /// <summary>
        /// Checks whether the device has rulebase links and therefore rules to report.
        /// </summary>
        public static bool ContainsRules(this DeviceReport device)
        {
            return device.RulebaseLinks != null && device.RulebaseLinks.Length > 0;
        }

        public static bool ContainsRules(this ManagementReport management)
        {
            return management.Devices != null && management.Devices.Any(d => d.ContainsRules());
        }

        /// <summary>
        /// Checks whether any rulebase of the management contains rules (used in management rulebases view).
        /// </summary>
        public static bool ContainsRulebaseRules(this ManagementReport management)
        {
            return management.Rulebases.Any(rulebase => rulebase.Rules.Length > 0);
        }

        /// <summary>
        /// Counts the rules of all rulebases of the management (used in management rulebases view).
        /// </summary>
        public static int CountRulebaseRules(this ManagementReport management)
        {
            return management.Rulebases.Sum(rulebase => rulebase.Rules.Length);
        }
    }
}
