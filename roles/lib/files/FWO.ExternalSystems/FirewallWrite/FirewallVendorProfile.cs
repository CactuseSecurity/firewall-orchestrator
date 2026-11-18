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

        private const string kCheckPointPolicyTargets = "Policy Targets";

        private static readonly FirewallVendorProfile CheckPoint = new("Any", "Accept", "Drop", "Log", "None", true, false)
        {
            WritesRules = true,
            AllTargetsInstallOn = kCheckPointPolicyTargets,
            AddressesRulesInImportedLayer = true
        };
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
            GroupsHoldAddresses = true
        };
        private static readonly FirewallVendorProfile Azure = new("*", "Allow", "Deny", "", "", false, true)
        {
            DefaultLayerName = kFwoRuleCollectionGroup
        };

        /// <summary>Whether a '/' in a name is escaped as "\/" where the name is part of a JSON-RPC url (FortiManager).</summary>
        public bool EscapeSlashInUrlNames { get; init; }

        /// <summary>Whether group members are added and removed with one call per member (FortiOS and OPNsense).</summary>
        public bool WritesMembersIndividually { get; init; }

        /// <summary>
        /// Whether groups hold the members' addresses as CIDR entries instead of references to member objects (OPNsense:
        /// the alias utility changes only literal entries, so groups are created the same way).
        /// </summary>
        public bool GroupsHoldAddresses { get; init; }

        /// <summary>
        /// Whether rules are written to the vendor. A new rule needs its position, which the rule positioning only
        /// calculates for Check Point so far; the rules of other vendors are left to a person.
        /// </summary>
        public bool WritesRules { get; init; }

        /// <summary>
        /// Layer used when neither the rule position nor the imported rulebase links name one, instead of the gateway's
        /// rulebase; Azure writes into a rule collection group of its own.
        /// </summary>
        public string? DefaultLayerName { get; init; }

        /// <summary>
        /// Install-on value of a rule that applies to all targets of its policy (Check Point: "Policy Targets"); null for
        /// vendors whose rules have no install-on.
        /// </summary>
        public string? AllTargetsInstallOn { get; init; }

        /// <summary>
        /// Whether an existing rule is modified or deleted in the layer it lies in according to the imported configuration
        /// (Check Point: a rule uid is only valid within its layer); a rule missing in the import is then not written.
        /// </summary>
        public bool AddressesRulesInImportedLayer { get; init; }

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
