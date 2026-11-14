using FWO.Data;
using FWO.Data.Report;

namespace FWO.Ui.Display
{
    /// <summary>
    /// The gateway a rule is listed below in a report, which decides that the last hit of this gateway is shown
    /// instead of the latest hit across all gateways.
    /// </summary>
    /// <param name="DeviceId">Database id of the gateway.</param>
    /// <param name="SupportsRuleHits">Whether the management of the gateway reports rule hits at all.</param>
    public readonly record struct GatewayHitContext(int DeviceId, bool SupportsRuleHits)
    {
        /// <summary>
        /// Returns the context of a rule listed below a gateway of a rule report, or null where the latest hit
        /// across all gateways is shown. That is the case
        /// <list type="bullet">
        /// <item>in management rulebases view, where a rulebase is listed once for all gateways using it,</item>
        /// <item>for report data loaded without the device type of the management,</item>
        /// <item>where the hit on the gateway is not known, so a missing hit must not be shown as "no hits":
        /// for a gateway without any imported hit (the rule hit import has not run yet, is switched off or could
        /// not read the gateway) and for a rule of another management without a hit on the gateway (global
        /// rules, whose hits are not read per gateway for every vendor).</item>
        /// </list>
        /// </summary>
        /// <param name="management">Management of the gateway.</param>
        /// <param name="device">Gateway the rules are listed below.</param>
        /// <param name="rule">Rule whose last hit is displayed.</param>
        /// <param name="managementRulebaseView">True if the report lists rulebases once per management.</param>
        public static GatewayHitContext? For(ManagementReport management, DeviceReport device, Rule rule, bool managementRulebaseView = false)
        {
            if (managementRulebaseView || management.DeviceType == null)
            {
                return null;
            }
            if (!management.DeviceType.SupportsRuleHits)
            {
                return new GatewayHitContext(device.Id, false);
            }
            if (!device.HasImportedRuleHits() || IsUnknownHitOfForeignRule(management, device, rule))
            {
                return null;
            }
            return new GatewayHitContext(device.Id, true);
        }

        /// <summary>
        /// Tells whether the rule belongs to another management than the gateway and has no hit on the gateway.
        /// A rule without a management id (not selected by the query) counts as a rule of the gateway's management.
        /// </summary>
        private static bool IsUnknownHitOfForeignRule(ManagementReport management, DeviceReport device, Rule rule)
        {
            bool isForeignRule = rule.MgmtId != 0 && rule.MgmtId != management.Id;
            return isForeignRule && rule.Metadata.GetGatewayLastHit(device.Id) == null;
        }
    }
}
