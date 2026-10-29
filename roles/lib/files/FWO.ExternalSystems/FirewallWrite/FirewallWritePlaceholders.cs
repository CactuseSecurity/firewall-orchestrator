namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Placeholders of the firewall write endpoint and body templates. Scalar placeholders are meant to be used inside
    /// JSON strings and are inserted JSON-escaped (URL-encoded in endpoint paths); list placeholders and
    /// <see cref="RulePosition"/> are inserted as complete JSON values and are used without quotes. The "..._IN_URL"
    /// placeholders hold a name for a url inside a JSON body (FortiManager JSON-RPC), with a '/' in the name escaped.
    /// </summary>
    public static class FirewallWritePlaceholders
    {
        // context
        public const string ManagementName = "@@MANAGEMENT_NAME@@";
        public const string GatewayName = "@@GATEWAY_NAME@@";
        public const string DeviceName = "@@DEVICE_NAME@@";
        public const string VdomName = "@@VDOM_NAME@@";
        public const string PolicyName = "@@POLICY_NAME@@";
        public const string LayerName = "@@LAYER_NAME@@";
        public const string DomainName = "@@DOMAIN_NAME@@";
        public const string AdomName = "@@ADOM_NAME@@";
        public const string ResourceGroup = "@@RESOURCE_GROUP@@";
        public const string SubscriptionId = "@@SUBSCRIPTION_ID@@";
        public const string TicketId = "@@TICKET_ID@@";
        public const string TaskId = "@@TASK_ID@@";
        public const string TaskNumber = "@@TASK_NUMBER@@";

        // network objects
        public const string ObjectName = "@@OBJECT_NAME@@";
        public const string ObjectNameInUrl = "@@OBJECT_NAME_IN_URL@@";
        public const string Ip = "@@IP@@";
        public const string IpEnd = "@@IP_END@@";
        public const string IpCidr = "@@IP_CIDR@@";
        public const string Subnet = "@@SUBNET@@";
        public const string MaskLength = "@@MASK_LENGTH@@";
        public const string Netmask = "@@NETMASK@@";
        public const string Comment = "@@COMMENT@@";
        public const string ObjectAddresses = "@@OBJECT_ADDRESSES@@";

        // services
        public const string ServiceName = "@@SERVICE_NAME@@";
        public const string ServiceNameInUrl = "@@SERVICE_NAME_IN_URL@@";
        public const string Protocol = "@@PROTOCOL@@";
        public const string ProtocolId = "@@PROTOCOL_ID@@";
        public const string Port = "@@PORT@@";
        public const string PortEnd = "@@PORT_END@@";
        public const string PortRange = "@@PORT_RANGE@@";

        // groups
        public const string GroupName = "@@GROUP_NAME@@";
        public const string GroupNameInUrl = "@@GROUP_NAME_IN_URL@@";
        public const string MemberName = "@@MEMBER_NAME@@";
        public const string Members = "@@MEMBERS@@";
        public const string MembersToAdd = "@@MEMBERS_TO_ADD@@";
        public const string MembersToRemove = "@@MEMBERS_TO_REMOVE@@";
        public const string MembersNameObjects = "@@MEMBERS_NAME_OBJECTS@@";
        public const string MembersLines = "@@MEMBERS_LINES@@";
        public const string MemberAddresses = "@@MEMBER_ADDRESSES@@";

        // rules
        public const string RuleName = "@@RULE_NAME@@";
        public const string RuleUid = "@@RULE_UID@@";
        public const string RulePosition = "@@RULE_POSITION@@";
        public const string RuleSources = "@@RULE_SOURCES@@";
        public const string RuleDestinations = "@@RULE_DESTINATIONS@@";
        public const string RuleServices = "@@RULE_SERVICES@@";
        public const string RuleSourcesToAdd = "@@RULE_SOURCES_TO_ADD@@";
        public const string RuleSourcesToRemove = "@@RULE_SOURCES_TO_REMOVE@@";
        public const string RuleDestinationsToAdd = "@@RULE_DESTINATIONS_TO_ADD@@";
        public const string RuleDestinationsToRemove = "@@RULE_DESTINATIONS_TO_REMOVE@@";
        public const string RuleServicesToAdd = "@@RULE_SERVICES_TO_ADD@@";
        public const string RuleServicesToRemove = "@@RULE_SERVICES_TO_REMOVE@@";
        public const string RuleSourcesNameObjects = "@@RULE_SOURCES_NAME_OBJECTS@@";
        public const string RuleDestinationsNameObjects = "@@RULE_DESTINATIONS_NAME_OBJECTS@@";
        public const string RuleServicesNameObjects = "@@RULE_SERVICES_NAME_OBJECTS@@";
        public const string RuleSourcesCsv = "@@RULE_SOURCES_CSV@@";
        public const string RuleDestinationsCsv = "@@RULE_DESTINATIONS_CSV@@";
        public const string RuleSourceAddresses = "@@RULE_SOURCE_ADDRESSES@@";
        public const string RuleDestinationAddresses = "@@RULE_DESTINATION_ADDRESSES@@";
        public const string RuleServicesCsv = "@@RULE_SERVICES_CSV@@";
        public const string RuleProtocol = "@@RULE_PROTOCOL@@";
        public const string RuleServicePorts = "@@RULE_SERVICE_PORTS@@";
        public const string RuleServiceProtocols = "@@RULE_SERVICE_PROTOCOLS@@";
        public const string RuleAction = "@@RULE_ACTION@@";
        public const string RuleTracking = "@@RULE_TRACKING@@";
        public const string RuleComment = "@@RULE_COMMENT@@";

        // resolved by the vendor client at execution time, after login
        public const string ObjectUuid = "@@OBJECT_UUID@@";
        public const string MergedMembers = "@@MERGED_MEMBERS@@";
        public const string Location = "@@LOCATION@@";

        /// <summary>Placeholders that only the vendor client can resolve, because they need data read from the firewall.</summary>
        public static IReadOnlyList<string> RuntimePlaceholders { get; } = new List<string> { ObjectUuid, MergedMembers, Location }.AsReadOnly();
    }
}
