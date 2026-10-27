using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Shipped FortiManager JSON-RPC calls, written to the ADOM of the management (config path). The session is added
    /// by the client. Workspace mode is not used, so there is no publish call. Names inside a JSON-RPC url use the
    /// "..._IN_URL" placeholders, which escape a '/' in the name. Group members are added and removed with the member
    /// attribute url, so members changed on the FortiManager in the meantime are kept.
    /// </summary>
    public static class FortiManagerWriteDefaults
    {
        private const string kEndpoint = "POST /jsonrpc";
        private const string kAddressUrl = "/pm/config/adom/@@ADOM_NAME@@/obj/firewall/address";
        private const string kAddress6Url = "/pm/config/adom/@@ADOM_NAME@@/obj/firewall/address6";
        private const string kServiceUrl = "/pm/config/adom/@@ADOM_NAME@@/obj/firewall/service/custom";
        private const string kGroupUrl = "/pm/config/adom/@@ADOM_NAME@@/obj/firewall/addrgrp";
        private const string kPolicyUrl = "/pm/config/adom/@@ADOM_NAME@@/pkg/@@POLICY_NAME@@/firewall/policy";
        private const string kObjectInUrl = "/@@OBJECT_NAME_IN_URL@@";
        private const string kServiceInUrl = "/@@SERVICE_NAME_IN_URL@@";
        private const string kGroupInUrl = "/@@GROUP_NAME_IN_URL@@";
        private const string kMemberAttribute = "/member";

        private const string kSubnetData = """{"name":"@@OBJECT_NAME@@","type":"ipmask","subnet":["@@SUBNET@@","@@NETMASK@@"],"comment":"@@COMMENT@@"}""";
        private const string kRangeData = """{"name":"@@OBJECT_NAME@@","type":"iprange","start-ip":"@@IP@@","end-ip":"@@IP_END@@","comment":"@@COMMENT@@"}""";
        private const string kPrefix6Data = """{"name":"@@OBJECT_NAME@@","type":"ipprefix","ip6":"@@IP_CIDR@@","comment":"@@COMMENT@@"}""";
        private const string kServiceData = """{"name":"@@SERVICE_NAME@@","protocol":"TCP/UDP/SCTP","@@PROTOCOL@@-portrange":["@@PORT_RANGE@@"],"comment":"@@COMMENT@@"}""";

        private const string kRuleData = """
            {"name":"@@RULE_NAME@@","srcintf":["any"],"dstintf":["any"],"srcaddr":@@RULE_SOURCES@@,"dstaddr":@@RULE_DESTINATIONS@@,
             "service":@@RULE_SERVICES@@,"action":"@@RULE_ACTION@@","schedule":["always"],"logtraffic":"@@RULE_TRACKING@@","comments":"@@RULE_COMMENT@@"}
            """;

        private const string kRuleModifyData = """
            {"srcaddr":@@RULE_SOURCES@@,"dstaddr":@@RULE_DESTINATIONS@@,"service":@@RULE_SERVICES@@,"comments":"@@RULE_COMMENT@@"}
            """;

        private const string kInstallData = """
            {"adom":"@@ADOM_NAME@@","pkg":"@@POLICY_NAME@@","scope":[{"name":"@@DEVICE_NAME@@","vdom":"@@VDOM_NAME@@"}]}
            """;

        /// <summary>Shipped calls per operation.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.HostCreate, Call("add", kAddressUrl, kSubnetData) },
                { FirewallWriteOperation.HostModify, Call("update", kAddressUrl + kObjectInUrl, kSubnetData) },
                { FirewallWriteOperation.HostDelete, Call("delete", kAddressUrl + kObjectInUrl) },
                { FirewallWriteOperation.NetworkCreate, Call("add", kAddressUrl, kSubnetData) },
                { FirewallWriteOperation.NetworkModify, Call("update", kAddressUrl + kObjectInUrl, kSubnetData) },
                { FirewallWriteOperation.NetworkDelete, Call("delete", kAddressUrl + kObjectInUrl) },
                { FirewallWriteOperation.AddressRangeCreate, Call("add", kAddressUrl, kRangeData) },
                { FirewallWriteOperation.AddressRangeModify, Call("update", kAddressUrl + kObjectInUrl, kRangeData) },
                { FirewallWriteOperation.AddressRangeDelete, Call("delete", kAddressUrl + kObjectInUrl) },
                { FirewallWriteOperation.ServiceCreate, Call("add", kServiceUrl, kServiceData) },
                { FirewallWriteOperation.ServiceModify, Call("update", kServiceUrl + kServiceInUrl, kServiceData) },
                { FirewallWriteOperation.ServiceDelete, Call("delete", kServiceUrl + kServiceInUrl) },
                { FirewallWriteOperation.GroupCreate, Call("add", kGroupUrl, """{"name":"@@GROUP_NAME@@","member":@@MEMBERS@@,"comment":"@@COMMENT@@"}""") },
                { FirewallWriteOperation.GroupAddMembers, Call("add", kGroupUrl + kGroupInUrl + kMemberAttribute, "@@MEMBERS_TO_ADD@@") },
                { FirewallWriteOperation.GroupRemoveMembers, Call("delete", kGroupUrl + kGroupInUrl + kMemberAttribute, "@@MEMBERS_TO_REMOVE@@") },
                { FirewallWriteOperation.GroupDelete, Call("delete", kGroupUrl + kGroupInUrl) },
                { FirewallWriteOperation.RuleCreate, Call("add", kPolicyUrl, kRuleData) },
                { FirewallWriteOperation.RuleModify, Call("update", kPolicyUrl + "/@@RULE_UID@@", kRuleModifyData) },
                { FirewallWriteOperation.RuleDelete, Call("delete", kPolicyUrl + "/@@RULE_UID@@") },
                { FirewallWriteOperation.InstallPolicy, Call("exec", "/securityconsole/install/package", kInstallData) }
            };

        /// <summary>Shipped calls for IPv6 network objects, which FortiManager keeps in the address6 table.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Ipv6Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.HostCreate, Call("add", kAddress6Url, kPrefix6Data) },
                { FirewallWriteOperation.HostModify, Call("update", kAddress6Url + kObjectInUrl, kPrefix6Data) },
                { FirewallWriteOperation.HostDelete, Call("delete", kAddress6Url + kObjectInUrl) },
                { FirewallWriteOperation.NetworkCreate, Call("add", kAddress6Url, kPrefix6Data) },
                { FirewallWriteOperation.NetworkModify, Call("update", kAddress6Url + kObjectInUrl, kPrefix6Data) },
                { FirewallWriteOperation.NetworkDelete, Call("delete", kAddress6Url + kObjectInUrl) },
                { FirewallWriteOperation.AddressRangeCreate, Call("add", kAddress6Url, kRangeData) },
                { FirewallWriteOperation.AddressRangeModify, Call("update", kAddress6Url + kObjectInUrl, kRangeData) },
                { FirewallWriteOperation.AddressRangeDelete, Call("delete", kAddress6Url + kObjectInUrl) }
            };

        /// <summary>Builds the JSON-RPC envelope of one call; the client adds the session.</summary>
        private static FirewallWriteDefault Call(string method, string url, string? data = null)
        {
            string dataPart = data == null ? "" : $",\"data\":{data.Trim()}";
            return new FirewallWriteDefault(kEndpoint, $$"""{"method":"{{method}}","params":[{"url":"{{url}}"{{dataPart}}}]}""");
        }
    }
}
