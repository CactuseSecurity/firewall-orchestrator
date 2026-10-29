using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Shipped Azure Resource Manager calls. Network objects and groups are IP groups in the resource group of the
    /// management (config path); rules are network rules in the rule collection group named after the gateway's rulebase
    /// in the firewall policy of the gateway. Azure has no service objects. The client merges group members and rules
    /// into the current resource, because ARM only replaces whole resources.
    /// </summary>
    public static class AzureWriteDefaults
    {
        /// <summary>ARM api-version of the Microsoft.Network resources used by the shipped calls.</summary>
        public const string kNetworkApiVersion = "2024-05-01";

        private const string kIpGroupPath =
            "/subscriptions/@@SUBSCRIPTION_ID@@/resourceGroups/@@RESOURCE_GROUP@@/providers/Microsoft.Network/ipGroups/";
        private const string kRuleCollectionGroupPath =
            "/subscriptions/@@SUBSCRIPTION_ID@@/resourceGroups/@@RESOURCE_GROUP@@/providers/Microsoft.Network/firewallPolicies/@@POLICY_NAME@@/ruleCollectionGroups/@@LAYER_NAME@@?api-version=" + kNetworkApiVersion;
        private const string kApiVersionQuery = "?api-version=" + kNetworkApiVersion;

        private const string kObjectBody = """{"location":"@@LOCATION@@","properties":{"ipAddresses":@@OBJECT_ADDRESSES@@}}""";
        private const string kMergedMembersBody = """{"location":"@@LOCATION@@","properties":{"ipAddresses":@@MERGED_MEMBERS@@}}""";

        private const string kRuleBody = """
            {"ruleCollectionName":"FWO-@@RULE_ACTION@@","ruleCollectionPriority":10000,"ruleCollectionAction":"@@RULE_ACTION@@",
             "rule":{"ruleType":"NetworkRule","name":"@@RULE_NAME@@","description":"@@RULE_COMMENT@@",
              "sourceAddresses":@@RULE_SOURCE_ADDRESSES@@,"destinationAddresses":@@RULE_DESTINATION_ADDRESSES@@,
              "destinationPorts":@@RULE_SERVICE_PORTS@@,"ipProtocols":@@RULE_SERVICE_PROTOCOLS@@}}
            """;

        private const string kRuleModifyBody = """
            {"rule":{"ruleType":"NetworkRule","name":"@@RULE_UID@@","description":"@@RULE_COMMENT@@",
              "sourceAddresses":@@RULE_SOURCE_ADDRESSES@@,"destinationAddresses":@@RULE_DESTINATION_ADDRESSES@@,
              "destinationPorts":@@RULE_SERVICE_PORTS@@,"ipProtocols":@@RULE_SERVICE_PROTOCOLS@@}}
            """;

        /// <summary>Shipped calls per operation.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.HostCreate, new(IpGroup("PUT", "@@OBJECT_NAME@@"), kObjectBody) },
                { FirewallWriteOperation.HostModify, new(IpGroup("PUT", "@@OBJECT_NAME@@"), kObjectBody) },
                { FirewallWriteOperation.HostDelete, new(IpGroup("DELETE", "@@OBJECT_NAME@@"), "") },
                { FirewallWriteOperation.NetworkCreate, new(IpGroup("PUT", "@@OBJECT_NAME@@"), kObjectBody) },
                { FirewallWriteOperation.NetworkModify, new(IpGroup("PUT", "@@OBJECT_NAME@@"), kObjectBody) },
                { FirewallWriteOperation.NetworkDelete, new(IpGroup("DELETE", "@@OBJECT_NAME@@"), "") },
                { FirewallWriteOperation.AddressRangeCreate, new(IpGroup("PUT", "@@OBJECT_NAME@@"), kObjectBody) },
                { FirewallWriteOperation.AddressRangeModify, new(IpGroup("PUT", "@@OBJECT_NAME@@"), kObjectBody) },
                { FirewallWriteOperation.AddressRangeDelete, new(IpGroup("DELETE", "@@OBJECT_NAME@@"), "") },
                { FirewallWriteOperation.GroupCreate, new(IpGroup("PUT", "@@GROUP_NAME@@"), """{"location":"@@LOCATION@@","properties":{"ipAddresses":@@MEMBER_ADDRESSES@@}}""") },
                { FirewallWriteOperation.GroupAddMembers, new(IpGroup("PUT", "@@GROUP_NAME@@"), kMergedMembersBody) },
                { FirewallWriteOperation.GroupRemoveMembers, new(IpGroup("PUT", "@@GROUP_NAME@@"), kMergedMembersBody) },
                { FirewallWriteOperation.GroupDelete, new(IpGroup("DELETE", "@@GROUP_NAME@@"), "") },
                { FirewallWriteOperation.RuleCreate, new($"PUT {kRuleCollectionGroupPath}", kRuleBody) },
                { FirewallWriteOperation.RuleModify, new($"PUT {kRuleCollectionGroupPath}", kRuleModifyBody) },
                { FirewallWriteOperation.RuleDelete, new($"PUT {kRuleCollectionGroupPath}", """{"ruleName":"@@RULE_UID@@"}""") }
            };

        private static string IpGroup(string method, string namePlaceholder) => $"{method} {kIpGroupPath}{namePlaceholder}{kApiVersionQuery}";
    }
}
