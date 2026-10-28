namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Vendor-specific words rendered into rule and object placeholders: the "any" service, rule actions, logging values
    /// and object name restrictions. There is deliberately no "any" network object: a rule side without elements is not
    /// widened to any, it fails.
    /// </summary>
    public sealed record FirewallVendorProfile(
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
        private const string kEscapedSlash = "\\/";
        private const char kVdomSeparator = '_';
        private const string kDefaultVdom = "root";
        private const string kFwoRuleCollectionGroup = "FWO";

        private static readonly FirewallVendorProfile CheckPoint = new("Any", "Accept", "Drop", "Log", "None", true, false);
        private static readonly FirewallVendorProfile FortiOs = new("ALL", "accept", "deny", "all", "disable", false, false)
        {
            WritesMembersIndividually = true
        };
        private static readonly FirewallVendorProfile FortiManager = new("ALL", "accept", "deny", "all", "disable", false, false)
        {
            EscapeSlashInUrlNames = true
        };
        private static readonly FirewallVendorProfile OpnSense = new("any", "pass", "block", "1", "0", false, false)
        {
            WritesMembersIndividually = true,
            RequiresAddressMembers = true
        };
        private static readonly FirewallVendorProfile Azure = new("*", "Allow", "Deny", "", "", false, true)
        {
            DefaultLayerName = kFwoRuleCollectionGroup
        };

        /// <summary>Whether a '/' in a name is escaped as "\/" where the name is part of a JSON-RPC url (FortiManager).</summary>
        public bool EscapeSlashInUrlNames { get; init; }

        /// <summary>Whether group members are added and removed with one call per member (FortiOS and OPNsense).</summary>
        public bool WritesMembersIndividually { get; init; }

        /// <summary>Whether member changes accept only literal IP addresses or CIDRs, rather than alias references.</summary>
        public bool RequiresAddressMembers { get; init; }

        /// <summary>
        /// Layer used when neither the rule position nor the imported rulebase links name one, instead of the gateway's
        /// rulebase; Azure writes into a rule collection group of its own.
        /// </summary>
        public string? DefaultLayerName { get; init; }

        /// <summary>Returns the profile of a vendor family.</summary>
        public static FirewallVendorProfile For(FirewallVendor vendor) => vendor switch
        {
            FirewallVendor.CheckPoint => CheckPoint,
            FirewallVendor.FortiManager => FortiManager,
            FirewallVendor.FortiOs => FortiOs,
            FirewallVendor.OpnSense => OpnSense,
            FirewallVendor.Azure => Azure,
            _ => throw new ArgumentOutOfRangeException(nameof(vendor), vendor, "The device type is not supported by the firewall write library.")
        };

        /// <summary>Returns an object name the vendor accepts.</summary>
        public string NormalizeName(string name) => ReplaceSlashInNames ? name.Replace(kSlash, kSlashReplacement) : name;

        /// <summary>Returns a name as part of a url inside a JSON body, escaped as the vendor requires.</summary>
        public string NameInUrl(string name) => EscapeSlashInUrlNames ? name.Replace(kSlash, kEscapedSlash) : name;

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
