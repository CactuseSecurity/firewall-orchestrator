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
        /// Returns the context of a gateway of a rule report, or null where the latest hit across all gateways is
        /// shown: in management rulebases view a rulebase is listed once for all gateways using it, and report
        /// data loaded without the device type of the management carries no hits per gateway.
        /// </summary>
        /// <param name="management">Management of the gateway.</param>
        /// <param name="device">Gateway the rules are listed below.</param>
        /// <param name="managementRulebaseView">True if the report lists rulebases once per management.</param>
        public static GatewayHitContext? For(ManagementReport management, DeviceReport device, bool managementRulebaseView = false)
        {
            if (managementRulebaseView || management.DeviceType == null)
            {
                return null;
            }
            return new GatewayHitContext(device.Id, management.DeviceType.SupportsRuleHits);
        }
    }
}
