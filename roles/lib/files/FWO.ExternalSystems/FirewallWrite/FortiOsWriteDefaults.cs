using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Shipped FortiOS REST API calls. Changes take effect immediately, so there is no publish or install call. Group
    /// members are added and removed one by one in the member table of the group, so members changed on the firewall in the
    /// meantime are kept.
    /// </summary>
    public static class FortiOsWriteDefaults
    {
        private const string kAddressPath = "/api/v2/cmdb/firewall/address";
        private const string kAddress6Path = "/api/v2/cmdb/firewall/address6";
        private const string kServicePath = "/api/v2/cmdb/firewall.service/custom";
        private const string kGroupPath = "/api/v2/cmdb/firewall/addrgrp";
        private const string kPolicyPath = "/api/v2/cmdb/firewall/policy";
        private const string kObjectInPath = "/@@OBJECT_NAME@@";
        private const string kGroupMembersPath = kGroupPath + "/@@GROUP_NAME@@/member";

        private const string kSubnetBody = """{"name":"@@OBJECT_NAME@@","type":"ipmask","subnet":"@@SUBNET@@ @@NETMASK@@","comment":"@@COMMENT@@"}""";
        private const string kRangeBody = """{"name":"@@OBJECT_NAME@@","type":"iprange","start-ip":"@@IP@@","end-ip":"@@IP_END@@","comment":"@@COMMENT@@"}""";
        private const string kPrefix6Body = """{"name":"@@OBJECT_NAME@@","type":"ipprefix","ip6":"@@IP_CIDR@@","comment":"@@COMMENT@@"}""";
        private const string kServiceBody = """{"name":"@@SERVICE_NAME@@","@@PROTOCOL@@-portrange":"@@PORT_RANGE@@","comment":"@@COMMENT@@"}""";

        private const string kRuleCreateBody = """
            {"name":"@@RULE_NAME@@","srcintf":[{"name":"any"}],"dstintf":[{"name":"any"}],
             "srcaddr":@@RULE_SOURCES_NAME_OBJECTS@@,"dstaddr":@@RULE_DESTINATIONS_NAME_OBJECTS@@,"service":@@RULE_SERVICES_NAME_OBJECTS@@,
             "action":"@@RULE_ACTION@@","schedule":"always","logtraffic":"@@RULE_TRACKING@@","comments":"@@RULE_COMMENT@@"}
            """;

        private const string kRuleModifyBody = """
            {"srcaddr":@@RULE_SOURCES_NAME_OBJECTS@@,"dstaddr":@@RULE_DESTINATIONS_NAME_OBJECTS@@,"service":@@RULE_SERVICES_NAME_OBJECTS@@,"comments":"@@RULE_COMMENT@@"}
            """;

        /// <summary>Shipped calls per operation.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.HostCreate, new($"POST {kAddressPath}", kSubnetBody) },
                { FirewallWriteOperation.HostModify, new($"PUT {kAddressPath}{kObjectInPath}", kSubnetBody) },
                { FirewallWriteOperation.HostDelete, new($"DELETE {kAddressPath}{kObjectInPath}", "") },
                { FirewallWriteOperation.NetworkCreate, new($"POST {kAddressPath}", kSubnetBody) },
                { FirewallWriteOperation.NetworkModify, new($"PUT {kAddressPath}{kObjectInPath}", kSubnetBody) },
                { FirewallWriteOperation.NetworkDelete, new($"DELETE {kAddressPath}{kObjectInPath}", "") },
                { FirewallWriteOperation.AddressRangeCreate, new($"POST {kAddressPath}", kRangeBody) },
                { FirewallWriteOperation.AddressRangeModify, new($"PUT {kAddressPath}{kObjectInPath}", kRangeBody) },
                { FirewallWriteOperation.AddressRangeDelete, new($"DELETE {kAddressPath}{kObjectInPath}", "") },
                { FirewallWriteOperation.ServiceCreate, new($"POST {kServicePath}", kServiceBody) },
                { FirewallWriteOperation.ServiceModify, new($"PUT {kServicePath}/@@SERVICE_NAME@@", kServiceBody) },
                { FirewallWriteOperation.ServiceDelete, new($"DELETE {kServicePath}/@@SERVICE_NAME@@", "") },
                { FirewallWriteOperation.GroupCreate, new($"POST {kGroupPath}", """{"name":"@@GROUP_NAME@@","member":@@MEMBERS_NAME_OBJECTS@@,"comment":"@@COMMENT@@"}""") },
                { FirewallWriteOperation.GroupAddMembers, new($"POST {kGroupMembersPath}", """{"name":"@@MEMBER_NAME@@"}""") },
                { FirewallWriteOperation.GroupRemoveMembers, new($"DELETE {kGroupMembersPath}/@@MEMBER_NAME@@", "") },
                { FirewallWriteOperation.GroupDelete, new($"DELETE {kGroupPath}/@@GROUP_NAME@@", "") },
                { FirewallWriteOperation.RuleCreate, new($"POST {kPolicyPath}", kRuleCreateBody) },
                { FirewallWriteOperation.RuleModify, new($"PUT {kPolicyPath}/@@RULE_UID@@", kRuleModifyBody) },
                { FirewallWriteOperation.RuleDelete, new($"DELETE {kPolicyPath}/@@RULE_UID@@", "") }
            };

        /// <summary>Shipped calls for IPv6 network objects, which FortiOS keeps in the address6 table.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Ipv6Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.HostCreate, new($"POST {kAddress6Path}", kPrefix6Body) },
                { FirewallWriteOperation.HostModify, new($"PUT {kAddress6Path}{kObjectInPath}", kPrefix6Body) },
                { FirewallWriteOperation.HostDelete, new($"DELETE {kAddress6Path}{kObjectInPath}", "") },
                { FirewallWriteOperation.NetworkCreate, new($"POST {kAddress6Path}", kPrefix6Body) },
                { FirewallWriteOperation.NetworkModify, new($"PUT {kAddress6Path}{kObjectInPath}", kPrefix6Body) },
                { FirewallWriteOperation.NetworkDelete, new($"DELETE {kAddress6Path}{kObjectInPath}", "") },
                { FirewallWriteOperation.AddressRangeCreate, new($"POST {kAddress6Path}", kRangeBody) },
                { FirewallWriteOperation.AddressRangeModify, new($"PUT {kAddress6Path}{kObjectInPath}", kRangeBody) },
                { FirewallWriteOperation.AddressRangeDelete, new($"DELETE {kAddress6Path}{kObjectInPath}", "") }
            };
    }
}
