namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Vendor-specific words rendered into rule and object placeholders: the "any" object, rule actions, logging values
    /// and object name restrictions.
    /// </summary>
    public sealed record FirewallVendorProfile(
        string AnyName,
        string AnyServiceName,
        string AcceptAction,
        string DropAction,
        string LogOn,
        string LogOff,
        bool ReplaceSlashInNames,
        bool ReferencesObjectsByAddress)
    {
        private const string kSlash = "/";
        private const string kSlashReplacement = "_";
        private const char kVdomSeparator = '_';
        private const string kDefaultVdom = "root";

        private static readonly FirewallVendorProfile CheckPoint = new("Any", "Any", "Accept", "Drop", "Log", "None", true, false);
        private static readonly FirewallVendorProfile Fortinet = new("all", "ALL", "accept", "deny", "all", "disable", false, false);
        private static readonly FirewallVendorProfile OpnSense = new("any", "any", "pass", "block", "1", "0", false, false);
        private static readonly FirewallVendorProfile Azure = new("*", "*", "Allow", "Deny", "", "", false, true);

        /// <summary>Returns the profile of a vendor family.</summary>
        public static FirewallVendorProfile For(FirewallVendor vendor) => vendor switch
        {
            FirewallVendor.CheckPoint => CheckPoint,
            FirewallVendor.FortiManager or FirewallVendor.FortiOs => Fortinet,
            FirewallVendor.OpnSense => OpnSense,
            FirewallVendor.Azure => Azure,
            _ => throw new ArgumentOutOfRangeException(nameof(vendor), vendor, "The device type is not supported by the firewall write library.")
        };

        /// <summary>Returns an object name the vendor accepts.</summary>
        public string NormalizeName(string name) => ReplaceSlashInNames ? name.Replace(kSlash, kSlashReplacement) : name;

        /// <summary>
        /// Splits a FortiManager gateway name "device_vdom" into device and VDOM. Names without separator use the root VDOM.
        /// </summary>
        public static (string Device, string Vdom) SplitDeviceAndVdom(string gatewayName)
        {
            int separatorIndex = gatewayName.LastIndexOf(kVdomSeparator);
            return separatorIndex <= 0 || separatorIndex == gatewayName.Length - 1
                ? (gatewayName, kDefaultVdom)
                : (gatewayName[..separatorIndex], gatewayName[(separatorIndex + 1)..]);
        }
    }
}
