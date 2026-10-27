using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Shipped FortiOS REST API calls. Changes take effect immediately, so there is no publish or install call.</summary>
    public static class FortiOsWriteDefaults
    {
        private const string kAddressPath = "/api/v2/cmdb/firewall/address";
        private const string kServicePath = "/api/v2/cmdb/firewall.service/custom";
        private const string kGroupPath = "/api/v2/cmdb/firewall/addrgrp";
        private const string kPolicyPath = "/api/v2/cmdb/firewall/policy";

        private const string kSubnetBody = """{"name":"@@OBJECT_NAME@@","type":"ipmask","subnet":"@@SUBNET@@ @@NETMASK@@","comment":"@@COMMENT@@"}""";
        private const string kRangeBody = """{"name":"@@OBJECT_NAME@@","type":"iprange","start-ip":"@@IP@@","end-ip":"@@IP_END@@","comment":"@@COMMENT@@"}""";
        private const string kServiceBody = """{"name":"@@SERVICE_NAME@@","@@PROTOCOL@@-portrange":"@@PORT_RANGE@@","comment":"@@COMMENT@@"}""";
        private const string kMergedMembersBody = """{"member":@@MERGED_MEMBERS@@}""";

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
                { FirewallWriteOperation.HostModify, new($"PUT {kAddressPath}/@@OBJECT_NAME@@", kSubnetBody) },
                { FirewallWriteOperation.HostDelete, new($"DELETE {kAddressPath}/@@OBJECT_NAME@@", "") },
                { FirewallWriteOperation.NetworkCreate, new($"POST {kAddressPath}", kSubnetBody) },
                { FirewallWriteOperation.NetworkModify, new($"PUT {kAddressPath}/@@OBJECT_NAME@@", kSubnetBody) },
                { FirewallWriteOperation.NetworkDelete, new($"DELETE {kAddressPath}/@@OBJECT_NAME@@", "") },
                { FirewallWriteOperation.AddressRangeCreate, new($"POST {kAddressPath}", kRangeBody) },
                { FirewallWriteOperation.AddressRangeModify, new($"PUT {kAddressPath}/@@OBJECT_NAME@@", kRangeBody) },
                { FirewallWriteOperation.AddressRangeDelete, new($"DELETE {kAddressPath}/@@OBJECT_NAME@@", "") },
                { FirewallWriteOperation.ServiceCreate, new($"POST {kServicePath}", kServiceBody) },
                { FirewallWriteOperation.ServiceModify, new($"PUT {kServicePath}/@@SERVICE_NAME@@", kServiceBody) },
                { FirewallWriteOperation.ServiceDelete, new($"DELETE {kServicePath}/@@SERVICE_NAME@@", "") },
                { FirewallWriteOperation.GroupCreate, new($"POST {kGroupPath}", """{"name":"@@GROUP_NAME@@","member":@@MEMBERS_NAME_OBJECTS@@,"comment":"@@COMMENT@@"}""") },
                { FirewallWriteOperation.GroupAddMembers, new($"PUT {kGroupPath}/@@GROUP_NAME@@", kMergedMembersBody) },
                { FirewallWriteOperation.GroupRemoveMembers, new($"PUT {kGroupPath}/@@GROUP_NAME@@", kMergedMembersBody) },
                { FirewallWriteOperation.GroupDelete, new($"DELETE {kGroupPath}/@@GROUP_NAME@@", "") },
                { FirewallWriteOperation.RuleCreate, new($"POST {kPolicyPath}", kRuleCreateBody) },
                { FirewallWriteOperation.RuleModify, new($"PUT {kPolicyPath}/@@RULE_UID@@", kRuleModifyBody) },
                { FirewallWriteOperation.RuleDelete, new($"DELETE {kPolicyPath}/@@RULE_UID@@", "") }
            };
    }
}
