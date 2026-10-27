using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Shipped FortiManager JSON-RPC calls, written to the ADOM of the management (config path). The session is added
    /// by the client. Workspace mode is not used, so there is no publish call.
    /// </summary>
    public static class FortiManagerWriteDefaults
    {
        private const string kEndpoint = "POST /jsonrpc";
        private const string kAddressUrl = "/pm/config/adom/@@ADOM_NAME@@/obj/firewall/address";
        private const string kServiceUrl = "/pm/config/adom/@@ADOM_NAME@@/obj/firewall/service/custom";
        private const string kGroupUrl = "/pm/config/adom/@@ADOM_NAME@@/obj/firewall/addrgrp";
        private const string kPolicyUrl = "/pm/config/adom/@@ADOM_NAME@@/pkg/@@POLICY_NAME@@/firewall/policy";

        private const string kSubnetData = """{"name":"@@OBJECT_NAME@@","type":"ipmask","subnet":["@@SUBNET@@","@@NETMASK@@"],"comment":"@@COMMENT@@"}""";
        private const string kRangeData = """{"name":"@@OBJECT_NAME@@","type":"iprange","start-ip":"@@IP@@","end-ip":"@@IP_END@@","comment":"@@COMMENT@@"}""";
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
                { FirewallWriteOperation.HostModify, Call("update", kAddressUrl + "/@@OBJECT_NAME@@", kSubnetData) },
                { FirewallWriteOperation.HostDelete, Call("delete", kAddressUrl + "/@@OBJECT_NAME@@") },
                { FirewallWriteOperation.NetworkCreate, Call("add", kAddressUrl, kSubnetData) },
                { FirewallWriteOperation.NetworkModify, Call("update", kAddressUrl + "/@@OBJECT_NAME@@", kSubnetData) },
                { FirewallWriteOperation.NetworkDelete, Call("delete", kAddressUrl + "/@@OBJECT_NAME@@") },
                { FirewallWriteOperation.AddressRangeCreate, Call("add", kAddressUrl, kRangeData) },
                { FirewallWriteOperation.AddressRangeModify, Call("update", kAddressUrl + "/@@OBJECT_NAME@@", kRangeData) },
                { FirewallWriteOperation.AddressRangeDelete, Call("delete", kAddressUrl + "/@@OBJECT_NAME@@") },
                { FirewallWriteOperation.ServiceCreate, Call("add", kServiceUrl, kServiceData) },
                { FirewallWriteOperation.ServiceModify, Call("update", kServiceUrl + "/@@SERVICE_NAME@@", kServiceData) },
                { FirewallWriteOperation.ServiceDelete, Call("delete", kServiceUrl + "/@@SERVICE_NAME@@") },
                { FirewallWriteOperation.GroupCreate, Call("add", kGroupUrl, """{"name":"@@GROUP_NAME@@","member":@@MEMBERS@@,"comment":"@@COMMENT@@"}""") },
                { FirewallWriteOperation.GroupAddMembers, Call("update", kGroupUrl + "/@@GROUP_NAME@@", """{"member":@@MERGED_MEMBERS@@}""") },
                { FirewallWriteOperation.GroupRemoveMembers, Call("update", kGroupUrl + "/@@GROUP_NAME@@", """{"member":@@MERGED_MEMBERS@@}""") },
                { FirewallWriteOperation.GroupDelete, Call("delete", kGroupUrl + "/@@GROUP_NAME@@") },
                { FirewallWriteOperation.RuleCreate, Call("add", kPolicyUrl, kRuleData) },
                { FirewallWriteOperation.RuleModify, Call("update", kPolicyUrl + "/@@RULE_UID@@", kRuleModifyData) },
                { FirewallWriteOperation.RuleDelete, Call("delete", kPolicyUrl + "/@@RULE_UID@@") },
                { FirewallWriteOperation.InstallPolicy, Call("exec", "/securityconsole/install/package", kInstallData) }
            };

        /// <summary>Builds the JSON-RPC envelope of one call; the client adds the session.</summary>
        private static FirewallWriteDefault Call(string method, string url, string? data = null)
        {
            string dataPart = data == null ? "" : $",\"data\":{data.Trim()}";
            return new FirewallWriteDefault(kEndpoint, $$"""{"method":"{{method}}","params":[{"url":"{{url}}"{{dataPart}}}]}""");
        }
    }
}
