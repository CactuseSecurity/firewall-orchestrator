using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Shipped OPNsense API calls. Network objects, services and groups are aliases; the client looks up the alias UUID
    /// by name. Groups hold their members' CIDR entries, so member changes can use the alias utility's locked, persistent
    /// per-address calls for host/network aliases. Publish applies the aliases, install applies the filter rules.
    /// </summary>
    public static class OpnSenseWriteDefaults
    {
        private const string kAliasPath = "/api/firewall/alias";
        private const string kFilterPath = "/api/firewall/filter";

        private const string kHostBody = """{"alias":{"enabled":"1","name":"@@OBJECT_NAME@@","type":"host","content":"@@IP@@","description":"@@COMMENT@@"}}""";
        private const string kNetworkBody = """{"alias":{"enabled":"1","name":"@@OBJECT_NAME@@","type":"network","content":"@@IP_CIDR@@","description":"@@COMMENT@@"}}""";
        private const string kRangeBody = """{"alias":{"enabled":"1","name":"@@OBJECT_NAME@@","type":"host","content":"@@IP@@-@@IP_END@@","description":"@@COMMENT@@"}}""";
        private const string kServiceBody = """{"alias":{"enabled":"1","name":"@@SERVICE_NAME@@","type":"port","content":"@@PORT@@:@@PORT_END@@","description":"@@COMMENT@@"}}""";
        private const string kMemberBody = """{"address":"@@MEMBER_NAME@@"}""";
        private const string kAliasUtilityPath = "/api/firewall/alias_util";

        private const string kRuleBody = """
            {"rule":{"enabled":"1","action":"@@RULE_ACTION@@","quick":"1","interface":"","direction":"in","ipprotocol":"inet",
             "protocol":"@@RULE_PROTOCOL@@","source_net":"@@RULE_SOURCES_CSV@@","destination_net":"@@RULE_DESTINATIONS_CSV@@",
             "destination_port":"@@RULE_SERVICES_CSV@@","log":"@@RULE_TRACKING@@","description":"@@RULE_NAME@@"}}
            """;

        private const string kRuleModifyBody = """
            {"rule":{"source_net":"@@RULE_SOURCES_CSV@@","destination_net":"@@RULE_DESTINATIONS_CSV@@","destination_port":"@@RULE_SERVICES_CSV@@"}}
            """;

        /// <summary>Shipped calls per operation.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.HostCreate, new($"POST {kAliasPath}/addItem", kHostBody) },
                { FirewallWriteOperation.HostModify, new($"POST {kAliasPath}/setItem/@@OBJECT_UUID@@", kHostBody) },
                { FirewallWriteOperation.HostDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.NetworkCreate, new($"POST {kAliasPath}/addItem", kNetworkBody) },
                { FirewallWriteOperation.NetworkModify, new($"POST {kAliasPath}/setItem/@@OBJECT_UUID@@", kNetworkBody) },
                { FirewallWriteOperation.NetworkDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.AddressRangeCreate, new($"POST {kAliasPath}/addItem", kRangeBody) },
                { FirewallWriteOperation.AddressRangeModify, new($"POST {kAliasPath}/setItem/@@OBJECT_UUID@@", kRangeBody) },
                { FirewallWriteOperation.AddressRangeDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.Ipv6HostCreate, new($"POST {kAliasPath}/addItem", kHostBody) },
                { FirewallWriteOperation.Ipv6HostModify, new($"POST {kAliasPath}/setItem/@@OBJECT_UUID@@", kHostBody) },
                { FirewallWriteOperation.Ipv6HostDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.Ipv6NetworkCreate, new($"POST {kAliasPath}/addItem", kNetworkBody) },
                { FirewallWriteOperation.Ipv6NetworkModify, new($"POST {kAliasPath}/setItem/@@OBJECT_UUID@@", kNetworkBody) },
                { FirewallWriteOperation.Ipv6NetworkDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.Ipv6AddressRangeCreate, new($"POST {kAliasPath}/addItem", kRangeBody) },
                { FirewallWriteOperation.Ipv6AddressRangeModify, new($"POST {kAliasPath}/setItem/@@OBJECT_UUID@@", kRangeBody) },
                { FirewallWriteOperation.Ipv6AddressRangeDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.ServiceCreate, new($"POST {kAliasPath}/addItem", kServiceBody) },
                { FirewallWriteOperation.ServiceModify, new($"POST {kAliasPath}/setItem/@@OBJECT_UUID@@", kServiceBody) },
                { FirewallWriteOperation.ServiceDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.GroupCreate, new($"POST {kAliasPath}/addItem", """{"alias":{"enabled":"1","name":"@@GROUP_NAME@@","type":"network","content":"@@MEMBERS_LINES@@","description":"@@COMMENT@@"}}""") },
                { FirewallWriteOperation.GroupAddMembers, new($"POST {kAliasUtilityPath}/add/@@GROUP_NAME@@", kMemberBody) },
                { FirewallWriteOperation.GroupRemoveMembers, new($"POST {kAliasUtilityPath}/delete/@@GROUP_NAME@@", kMemberBody) },
                { FirewallWriteOperation.GroupDelete, new($"POST {kAliasPath}/delItem/@@OBJECT_UUID@@", "") },
                { FirewallWriteOperation.RuleCreate, new($"POST {kFilterPath}/addRule", kRuleBody) },
                { FirewallWriteOperation.RuleModify, new($"POST {kFilterPath}/setRule/@@RULE_UID@@", kRuleModifyBody) },
                { FirewallWriteOperation.RuleDelete, new($"POST {kFilterPath}/delRule/@@RULE_UID@@", "") },
                { FirewallWriteOperation.Publish, new($"POST {kAliasPath}/reconfigure", "") },
                { FirewallWriteOperation.InstallPolicy, new($"POST {kFilterPath}/apply", "") }
            };
    }
}
