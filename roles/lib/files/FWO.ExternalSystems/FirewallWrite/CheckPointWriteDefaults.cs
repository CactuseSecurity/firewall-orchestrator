using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Shipped Check Point management API (web_api) calls for R8x and MDS.</summary>
    public static class CheckPointWriteDefaults
    {
        private const string kHostBody = """{"name":"@@OBJECT_NAME@@","ip-address":"@@IP@@","comments":"@@COMMENT@@"}""";
        private const string kNetworkBody = """{"name":"@@OBJECT_NAME@@","subnet":"@@SUBNET@@","mask-length":@@MASK_LENGTH@@,"comments":"@@COMMENT@@"}""";
        private const string kAddressRangeBody = """{"name":"@@OBJECT_NAME@@","ip-address-first":"@@IP@@","ip-address-last":"@@IP_END@@","comments":"@@COMMENT@@"}""";
        private const string kObjectNameBody = """{"name":"@@OBJECT_NAME@@"}""";
        private const string kServiceBody = """{"name":"@@SERVICE_NAME@@","port":"@@PORT_RANGE@@","comments":"@@COMMENT@@"}""";
        private const string kRuleUidBody = """{"uid":"@@RULE_UID@@","layer":"@@LAYER_NAME@@"}""";

        private const string kRuleCreateBody = """
            {"layer":"@@LAYER_NAME@@","position":@@RULE_POSITION@@,"name":"@@RULE_NAME@@",
             "source":@@RULE_SOURCES@@,"destination":@@RULE_DESTINATIONS@@,"service":@@RULE_SERVICES@@,
             "action":"@@RULE_ACTION@@","track":{"type":"@@RULE_TRACKING@@"},"comments":"@@RULE_COMMENT@@"}
            """;

        private const string kRuleModifyBody = """
            {"uid":"@@RULE_UID@@","layer":"@@LAYER_NAME@@",
             "source":{"add":@@RULE_SOURCES_TO_ADD@@,"remove":@@RULE_SOURCES_TO_REMOVE@@},
             "destination":{"add":@@RULE_DESTINATIONS_TO_ADD@@,"remove":@@RULE_DESTINATIONS_TO_REMOVE@@},
             "service":{"add":@@RULE_SERVICES_TO_ADD@@,"remove":@@RULE_SERVICES_TO_REMOVE@@}}
            """;

        private const string kInstallPolicyBody = """
            {"policy-package":"@@POLICY_NAME@@","access":true,"threat-prevention":false,"targets":["@@GATEWAY_NAME@@"]}
            """;

        /// <summary>Shipped calls per operation.</summary>
        public static IReadOnlyDictionary<FirewallWriteOperation, FirewallWriteDefault> Operations { get; } =
            new Dictionary<FirewallWriteOperation, FirewallWriteDefault>
            {
                { FirewallWriteOperation.HostCreate, new("POST add-host", kHostBody) },
                { FirewallWriteOperation.HostModify, new("POST set-host", kHostBody) },
                { FirewallWriteOperation.HostDelete, new("POST delete-host", kObjectNameBody) },
                { FirewallWriteOperation.NetworkCreate, new("POST add-network", kNetworkBody) },
                { FirewallWriteOperation.NetworkModify, new("POST set-network", kNetworkBody) },
                { FirewallWriteOperation.NetworkDelete, new("POST delete-network", kObjectNameBody) },
                { FirewallWriteOperation.AddressRangeCreate, new("POST add-address-range", kAddressRangeBody) },
                { FirewallWriteOperation.AddressRangeModify, new("POST set-address-range", kAddressRangeBody) },
                { FirewallWriteOperation.AddressRangeDelete, new("POST delete-address-range", kObjectNameBody) },
                { FirewallWriteOperation.ServiceCreate, new("POST add-service-@@PROTOCOL@@", kServiceBody) },
                { FirewallWriteOperation.ServiceModify, new("POST set-service-@@PROTOCOL@@", kServiceBody) },
                { FirewallWriteOperation.ServiceDelete, new("POST delete-service-@@PROTOCOL@@", """{"name":"@@SERVICE_NAME@@"}""") },
                { FirewallWriteOperation.GroupCreate, new("POST add-group", """{"name":"@@GROUP_NAME@@","members":@@MEMBERS@@,"comments":"@@COMMENT@@"}""") },
                { FirewallWriteOperation.GroupAddMembers, new("POST set-group", """{"name":"@@GROUP_NAME@@","members":{"add":@@MEMBERS_TO_ADD@@}}""") },
                { FirewallWriteOperation.GroupRemoveMembers, new("POST set-group", """{"name":"@@GROUP_NAME@@","members":{"remove":@@MEMBERS_TO_REMOVE@@}}""") },
                { FirewallWriteOperation.GroupDelete, new("POST delete-group", """{"name":"@@GROUP_NAME@@"}""") },
                { FirewallWriteOperation.RuleCreate, new("POST add-access-rule", kRuleCreateBody) },
                { FirewallWriteOperation.RuleModify, new("POST set-access-rule", kRuleModifyBody) },
                { FirewallWriteOperation.RuleDelete, new("POST delete-access-rule", kRuleUidBody) },
                { FirewallWriteOperation.Publish, new("POST publish", "{}") },
                { FirewallWriteOperation.InstallPolicy, new("POST install-policy", kInstallPolicyBody) }
            };
    }
}
