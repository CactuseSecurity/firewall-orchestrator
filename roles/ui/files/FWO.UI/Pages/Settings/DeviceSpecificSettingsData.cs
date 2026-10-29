using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using System.Globalization;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// How a device-specific-setting field should be presented/edited in the UI.
    /// </summary>
    public enum DeviceSpecificFieldKind
    {
        SingleSelect,
        Text,
        TemplateText,
        StringList,
        ExtTicketSystem,
        FirewallEndpoint,
        FirewallTemplate
    }

    /// <summary>Section of the settings page a field is shown in.</summary>
    public enum DeviceSpecificFieldSection
    {
        General,
        FirewallWrite
    }

    /// <summary>
    /// One selectable option for a SingleSelect field. Value is the name of the underlying data-layer enum member,
    /// TextKey the UI text key of its display name.
    /// </summary>
    public sealed class DeviceSpecificFieldOption(string value, string textKey)
    {
        public string Value { get; } = value;
        public string TextKey { get; } = textKey;
    }

    /// <summary>
    /// Definition (schema) of a single device specific setting in the UI: which typed setting key it edits,
    /// how it is edited, which scopes it is offered at, and how its value is converted between the
    /// data-layer DTOs and the editor's text representation.
    /// </summary>
    public sealed class DeviceSpecificFieldDefinition
    {
        public required DeviceSpecificSettingKey Key { get; init; }
        public required string LabelTextKey { get; init; }
        public required string HelpTextKey { get; init; }

        /// <summary>Optional second label part, e.g. the firewall write operation an endpoint or template belongs to.</summary>
        public string? LabelDetailTextKey { get; init; }

        public DeviceSpecificFieldSection Section { get; init; } = DeviceSpecificFieldSection.General;

        /// <summary>
        /// Default shipped for a device type (by id), shown while the value is empty and restored by "Reset to default".
        /// Null for fields without device type specific defaults.
        /// </summary>
        public Func<int, string?>? ShippedDefault { get; init; }

        /// <summary>Only shown at Global and for device types supported by the firewall write library.</summary>
        public bool FirewallWriteOnly { get; init; } = false;

        public DeviceSpecificFieldKind Kind { get; init; } = DeviceSpecificFieldKind.SingleSelect;
        public List<DeviceSpecificFieldOption> Options { get; init; } = [];

        /// <summary>Lowest (most general) scope at which this field is offered.</summary>
        public DeviceSpecificScopeType MinLevel { get; init; } = DeviceSpecificScopeType.Global;

        /// <summary>Highest (most specific) scope at which this field is offered.</summary>
        public DeviceSpecificScopeType MaxLevel { get; init; } = DeviceSpecificScopeType.Gateway;

        /// <summary>Only shown for scopes below a Fortinet device type.</summary>
        public bool FortinetOnly { get; init; } = false;

        /// <summary>Formats the field's value of a resolved settings object for the editor.</summary>
        public required Func<GlobalDeviceSpecificSettings, string> Read { get; init; }

        /// <summary>Adds an override with the given editor value for this field to a change set.</summary>
        public required Action<DeviceSpecificSettingsChangeSet, string> SetOverride { get; init; }

        public string Id => Key.DatabaseKey;

        /// <summary>Whether the field is offered at the given level.</summary>
        public bool AppliesToLevel(DeviceSpecificScopeType level) => level >= MinLevel && level <= MaxLevel;

        /// <summary>Whether the field is offered on the node's current device path.</summary>
        public bool AppliesTo(DeviceSpecificNode node) => AppliesToLevel(node.Level)
            && (!FortinetOnly || node.IsFortinet || node.Level == DeviceSpecificScopeType.Global)
            && (!FirewallWriteOnly || node.Level == DeviceSpecificScopeType.Global || node.SupportsFirewallWrite);

        /// <summary>The default shipped for the node's device type, or null if there is none.</summary>
        public string? ShippedDefaultFor(DeviceSpecificNode node) =>
            ShippedDefault != null && node.DeviceTypeId is int deviceTypeId ? ShippedDefault(deviceTypeId) : null;
    }

    /// <summary>
    /// One node of the device-specific-settings tree shown in the UI. The tree mirrors the real device
    /// types, managements and gateways; Scope carries the persisted device specific settings node (NodeId 0 while
    /// nothing has been stored for this node yet).
    /// </summary>
    public sealed class DeviceSpecificNode
    {
        public required DeviceSpecificSettingsScope Scope { get; set; }

        /// <summary>Path of the node through the current device hierarchy; settings are resolved along it.</summary>
        public required DeviceSpecificScopePath Path { get; init; }

        /// <summary>Current name of the underlying object (device type, management, gateway).</summary>
        public required string Name { get; init; }

        public DeviceSpecificNode? Parent { get; init; }

        public List<DeviceSpecificNode> Children { get; } = [];

        /// <summary>True for nodes at or below a Fortinet device type.</summary>
        public bool IsFortinet { get; init; }

        /// <summary>Device type of the node; null for Global.</summary>
        public int? DeviceTypeId { get; init; }

        /// <summary>True for nodes at or below a device type the firewall write library can write to.</summary>
        public bool SupportsFirewallWrite => DeviceTypeId is int deviceTypeId && FirewallVendors.FromDeviceTypeId(deviceTypeId) != FirewallVendor.Unsupported;

        public string Id => $"{Level}:{Scope.ObjectKey}";
        public DeviceSpecificScopeType Level => Scope.ScopeType;
        public bool IsPersisted => Scope.NodeId > 0;

        /// <summary>Walks from this node up to Global, this node first.</summary>
        public IEnumerable<DeviceSpecificNode> SelfAndAncestors()
        {
            for (DeviceSpecificNode? current = this; current != null; current = current.Parent)
            {
                yield return current;
            }
        }

        /// <summary>This node followed by all nodes below it, depth first.</summary>
        public IEnumerable<DeviceSpecificNode> SelfAndDescendants()
        {
            yield return this;
            foreach (DeviceSpecificNode descendant in Children.SelectMany(child => child.SelfAndDescendants()))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Field schema over the typed device specific setting keys, plus building the Global / DeviceType /
    /// Management / Gateway tree from the real managements. Drives the settings/devicespecificsettings UI.
    /// </summary>
    public static class DeviceSpecificSettingsData
    {
        public const string GlobalObjectKey = DeviceSpecificScopePath.GlobalObjectKey;
        public const string GlobalDisplayName = DeviceSpecificScopePath.GlobalDisplayName;
        public const string FortinetManufacturer = "Fortinet";

        private const char kListSeparator = ',';

        private static readonly Dictionary<string, string> HelpTextKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "dss_implementation_mode", "H5168" },
            { "dss_install_on", "H5169" },
            { "dss_time_object_creation", "H5199a" },
            { "dss_label_creation", "H5199b" },
            { "dss_implementation_mode_override_for_rules", "H5199c" },
            { "dss_owner_id_custom_field_name", "H5199d" },
            { "dss_change_id_custom_field_name", "H5199e" },
            { "dss_reroute_to_external_ticket_system", "H5199f" },
            { "dss_auto_plan_implementation", "H5199g" },
            { "dss_auto_plan_implementation_for_rules", "H5199h" },
            { "dss_modelling_create_app_zones", "H5199i" },
            { "dss_modelling_implement_network_area_objects", "H5199j" },
            { "dss_modelling_dyn_ip_labels_visible_as_groups", "H5199k" },
            { "dss_importer_host_name", "H5199l" },
            { "dss_logging", "H5190" },
            { "dss_service_object_creation", "H5191" },
            { "dss_address_object_creation", "H5192" },
            { "dss_rule_type", "H5193" },
            { "dss_templates", "H5194" },
            { "dss_positioning_algorithm", "H5195" },
            { "dss_common_service_separation", "H5196" },
            { "dss_zone_from", "H5198" },
            { "dss_zone_to", "H5199" },
            { "dss_modelling_connection_id_field", "H5199m" },
            { "dss_modelling_connection_id_template", "H5199m" },
            { "dss_fw_write_check_certificates", "H5199n" },
            { "dss_fw_write_install_policy", "H5199o" },
            { "dss_fw_write_delete_objects", "H5199t" },
            { "dss_fw_write_endpoint", "H5199p" },
            { "dss_fw_write_template", "H5199q" },
        };

        private static readonly List<DeviceSpecificFieldOption> BoolOptions =
        [
            new(bool.TrueString, "yes"),
            new(bool.FalseString, "no")
        ];

        /// <summary>Localized editors; config may store deeper overrides than the UI offers (MaxLevel).</summary>
        public static readonly IReadOnlyList<DeviceSpecificFieldDefinition> Fields =
        [
            EnumField(DeviceSpecificSettingKeys.InstallOn, "dss_install_on", s => s.InstallOn,
                EnumOptions(
                    (ProvisioningInstallOn.Any, "dss_opt_any"),
                    (ProvisioningInstallOn.GatewaysInPath, "dss_opt_gateways_in_path"))),
            EnumField(DeviceSpecificSettingKeys.PositioningAlgorithm, "dss_positioning_algorithm", s => s.PositioningAlgorithm,
                EnumOptions(
                    (ProvisioningPositioningAlgorithm.CheckPointInlineLayerPerZonePair, "dss_opt_cp_inline_layer_zone_pair"),
                    (ProvisioningPositioningAlgorithm.FortinetEndOfZone, "dss_opt_fortinet_end_of_zone"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection, "dss_opt_cp_end_of_app_section"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSectionDistinguishCommonServices, "dss_opt_cp_end_of_app_section_common_svc"),
                    (ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "dss_opt_default_end_of_rulebase"),
                    (ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection, "dss_opt_cp_layer_guard_end_of_zone_pair_section"),
                    (ProvisioningPositioningAlgorithm.AllowCategoryEndOfAppSectionDistinguishCommonServices, "dss_opt_allow_category"))),
            EnumField(DeviceSpecificSettingKeys.RuleLoggingOverride, "dss_logging", s => s.RuleLoggingOverride,
                EnumOptions(
                    (ProvisioningLoggingMode.Log, "dss_opt_log"),
                    (ProvisioningLoggingMode.LogTrack, "dss_opt_log_track"),
                    (ProvisioningLoggingMode.None, "dss_opt_none"))),
            EnumField(DeviceSpecificSettingKeys.ServiceObjectCreation, "dss_service_object_creation", s => s.ServiceObjectCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "dss_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "dss_opt_submanager")), maxLevel: DeviceSpecificScopeType.Management),
            EnumField(DeviceSpecificSettingKeys.AddressObjectCreation, "dss_address_object_creation", s => s.AddressObjectCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "dss_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "dss_opt_submanager")), maxLevel: DeviceSpecificScopeType.Management),
            EnumField(DeviceSpecificSettingKeys.TimeObjectCreation, "dss_time_object_creation", s => s.TimeObjectCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "dss_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "dss_opt_submanager"),
                    (ProvisioningObjectCreationMode.Ignore, "dss_opt_ignore")), maxLevel: DeviceSpecificScopeType.Management),
            EnumField(DeviceSpecificSettingKeys.LabelCreation, "dss_label_creation", s => s.LabelCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "dss_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "dss_opt_submanager"),
                    (ProvisioningObjectCreationMode.Ignore, "dss_opt_ignore")), maxLevel: DeviceSpecificScopeType.Management),
            EnumField(DeviceSpecificSettingKeys.RuleType, "dss_rule_type", s => s.RuleType,
                EnumOptions(
                    (ProvisioningRuleType.AlwaysAccess, "dss_opt_always_access"),
                    (ProvisioningRuleType.HandleAccessAndNat, "dss_opt_access_nat"),
                    (ProvisioningRuleType.HandleAccessAndIps, "dss_opt_access_ips"),
                    (ProvisioningRuleType.HandleAccessNatIps, "dss_opt_access_nat_ips"),
                    (ProvisioningRuleType.AppSecurity, "dss_opt_app_security")), maxLevel: DeviceSpecificScopeType.Management),
            BoolField(DeviceSpecificSettingKeys.CommonServiceSeparation, "dss_common_service_separation", s => s.CommonServiceSeparation,
                DeviceSpecificScopeType.Management),
            TextField(DeviceSpecificSettingKeys.RuleZoneFromOverride, "dss_zone_from", s => s.RuleZoneFromOverride, fortinetOnly: true,
                maxLevel: DeviceSpecificScopeType.Management),
            TextField(DeviceSpecificSettingKeys.RuleZoneToOverride, "dss_zone_to", s => s.RuleZoneToOverride, fortinetOnly: true,
                maxLevel: DeviceSpecificScopeType.Management),
            EnumField(DeviceSpecificSettingKeys.ImplementationMode, "dss_implementation_mode", s => s.ImplementationMode,
                EnumOptions(
                    (ProvisioningImplementationMode.Automatic, "dss_opt_automatic"),
                    (ProvisioningImplementationMode.Manual, "dss_opt_manual"),
                    (ProvisioningImplementationMode.RerouteToExternalTicketSystem, "dss_opt_reroute"))),
            EnumField(DeviceSpecificSettingKeys.ImplementationModeOverrideForRules, "dss_implementation_mode_override_for_rules", s => s.ImplementationModeOverrideForRules,
                EnumOptions(
                    (ProvisioningRuleImplementationMode.Automatic, "dss_opt_automatic"),
                    (ProvisioningRuleImplementationMode.Manual, "dss_opt_manual"),
                    (ProvisioningRuleImplementationMode.RerouteToExternalTicketSystem, "dss_opt_reroute"))),
            new()
            {
                Key = DeviceSpecificSettingKeys.RerouteToExternalTicketSystem,
                LabelTextKey = "dss_reroute_to_external_ticket_system",
                HelpTextKey = HelpKey("dss_reroute_to_external_ticket_system"),
                Kind = DeviceSpecificFieldKind.ExtTicketSystem,
                Read = s => s.RerouteToExternalTicketSystem?.ToString(CultureInfo.InvariantCulture) ?? "",
                SetOverride = SetExtTicketSystemOverride
            },
            new()
            {
                Key = DeviceSpecificSettingKeys.SecurityProfiles,
                LabelTextKey = "dss_security_profiles",
                HelpTextKey = "H5197",
                Kind = DeviceSpecificFieldKind.StringList,
                MinLevel = DeviceSpecificScopeType.DeviceType,
                MaxLevel = DeviceSpecificScopeType.Management,
                Read = s => string.Join(kListSeparator, s.SecurityProfiles),
                SetOverride = (changes, value) => changes.Set(DeviceSpecificSettingKeys.SecurityProfiles,
                    ParseStringList(value).Where(entry => entry.Length > 0).ToList())
            },
            TextField(DeviceSpecificSettingKeys.OwnerIdCustomFieldName, "dss_owner_id_custom_field_name", s => s.OwnerIdCustomFieldName, maxLevel: DeviceSpecificScopeType.Management),
            TextField(DeviceSpecificSettingKeys.ChangeIdCustomFieldName, "dss_change_id_custom_field_name", s => s.ChangeIdCustomFieldName, maxLevel: DeviceSpecificScopeType.Management),
            TextField(DeviceSpecificSettingKeys.ImporterHostName, "dss_importer_host_name", s => s.ImporterHostName, maxLevel: DeviceSpecificScopeType.Management),
            BoolField(DeviceSpecificSettingKeys.AutoPlanImplementation, "dss_auto_plan_implementation", s => s.AutoPlanImplementation),
            BoolField(DeviceSpecificSettingKeys.AutoPlanImplementationForRules, "dss_auto_plan_implementation_for_rules", s => s.AutoPlanImplementationForRules),
            BoolField(DeviceSpecificSettingKeys.ModellingCreateAppZones, "dss_modelling_create_app_zones", s => s.ModellingCreateAppZones,
                DeviceSpecificScopeType.Management),
            BoolField(DeviceSpecificSettingKeys.ModellingImplementNetworkAreaObjects, "dss_modelling_implement_network_area_objects", s => s.ModellingImplementNetworkAreaObjects,
                DeviceSpecificScopeType.Management),
            BoolField(DeviceSpecificSettingKeys.ModellingDynIpLabelsVisibleAsGroups, "dss_modelling_dyn_ip_labels_visible_as_groups", s => s.ModellingDynIpLabelsVisibleAsGroups,
                DeviceSpecificScopeType.Management),
            EnumField(DeviceSpecificSettingKeys.ModellingConnectionIdField, "dss_modelling_connection_id_field", s => s.ModellingConnectionIdField,
                EnumOptions(
                    (ProvisioningConnectionIdField.Name, "name"),
                    (ProvisioningConnectionIdField.Comment, "comment"),
                    (ProvisioningConnectionIdField.CustomField, "dss_opt_custom_field")), maxLevel: DeviceSpecificScopeType.Management),
            TextField(DeviceSpecificSettingKeys.ModellingConnectionIdTemplate, "dss_modelling_connection_id_template", s => s.ModellingConnectionIdTemplate,
                maxLevel: DeviceSpecificScopeType.Management),
            TextField(DeviceSpecificSettingKeys.Templates, "dss_templates", s => s.Templates, DeviceSpecificFieldKind.TemplateText,
                minLevel: DeviceSpecificScopeType.DeviceType, maxLevel: DeviceSpecificScopeType.DeviceType),
            .. FirewallWriteFields(),
        ];

        /// <summary>Text key of the label of a firewall write operation, e.g. "dss_fw_op_host_create".</summary>
        public static string OperationTextKey(FirewallWriteOperation operation)
        {
            System.Text.StringBuilder textKey = new("dss_fw_op");
            foreach (char character in operation.ToString())
            {
                if (char.IsUpper(character))
                {
                    textKey.Append('_');
                }
                textKey.Append(char.ToLowerInvariant(character));
            }
            return textKey.ToString();
        }

        /// <summary>
        /// Fields of the firewall write library: certificate checks, policy installation and object deletion, then endpoint and body
        /// template per operation. Endpoints and templates show the default shipped for the device type while empty.
        /// </summary>
        private static List<DeviceSpecificFieldDefinition> FirewallWriteFields()
        {
            List<DeviceSpecificFieldDefinition> fields =
            [
                BoolField(FirewallWriteSettingKeys.CheckCertificates, "dss_fw_write_check_certificates", s => s.FwWriteCheckCertificates,
                    DeviceSpecificScopeType.Management, DeviceSpecificFieldSection.FirewallWrite),
                BoolField(FirewallWriteSettingKeys.InstallPolicy, "dss_fw_write_install_policy", s => s.FwWriteInstallPolicy,
                    section: DeviceSpecificFieldSection.FirewallWrite),
                BoolField(FirewallWriteSettingKeys.DeleteObjects, "dss_fw_write_delete_objects", s => s.FwWriteDeleteObjects,
                    section: DeviceSpecificFieldSection.FirewallWrite)
            ];
            foreach (FirewallWriteOperation operation in Enum.GetValues<FirewallWriteOperation>())
            {
                fields.Add(FirewallWriteField(FirewallWriteSettingKeys.Endpoints[operation], operation, DeviceSpecificFieldKind.FirewallEndpoint,
                    s => s.FwWriteEndpoints, deviceTypeId => FirewallWriteDefaults.GetEndpoint(deviceTypeId, operation)));
                fields.Add(FirewallWriteField(FirewallWriteSettingKeys.Templates[operation], operation, DeviceSpecificFieldKind.FirewallTemplate,
                    s => s.FwWriteTemplates, deviceTypeId => FirewallWriteDefaults.GetTemplate(deviceTypeId, operation)));
            }
            return fields;
        }

        private static DeviceSpecificFieldDefinition FirewallWriteField(DeviceSpecificSettingKey<string> key, FirewallWriteOperation operation,
            DeviceSpecificFieldKind kind, Func<GlobalDeviceSpecificSettings, Dictionary<FirewallWriteOperation, string>> store,
            Func<int, string?> shippedDefault)
        {
            string textKey = kind == DeviceSpecificFieldKind.FirewallEndpoint ? "dss_fw_write_endpoint" : "dss_fw_write_template";
            return new()
            {
                Key = key,
                LabelTextKey = textKey,
                LabelDetailTextKey = OperationTextKey(operation),
                HelpTextKey = HelpKey(textKey),
                Kind = kind,
                Section = DeviceSpecificFieldSection.FirewallWrite,
                FirewallWriteOnly = true,
                MinLevel = DeviceSpecificScopeType.DeviceType,
                ShippedDefault = shippedDefault,
                Read = s => store(s).GetValueOrDefault(operation, ""),
                SetOverride = (changes, value) => changes.Set(key, ValidateFirewallWriteValue(kind, value))
            };
        }

        /// <summary>Endpoints must have the form "METHOD path"; an empty value stands for the shipped default.</summary>
        private static string ValidateFirewallWriteValue(DeviceSpecificFieldKind kind, string value)
        {
            if (kind == DeviceSpecificFieldKind.FirewallEndpoint && !string.IsNullOrWhiteSpace(value))
            {
                return FirewallWriteEndpoint.Parse(value).ToString();
            }
            return value;
        }

        /// <summary>
        /// The endpoint value after its method or path was changed in the editor. While the value is empty, the part that
        /// was not changed is taken from the shipped default, so changing one part takes the default over for editing.
        /// </summary>
        /// <param name="value">Current value; empty stands for the shipped default.</param>
        /// <param name="shippedDefault">Default shipped for the device type, if any.</param>
        /// <param name="method">New method, or null if the path was changed.</param>
        /// <param name="path">New path, or null if the method was changed.</param>
        public static string ChangeEndpoint(string value, string? shippedDefault, string? method = null, string? path = null)
        {
            string baseValue = string.IsNullOrWhiteSpace(value) ? shippedDefault ?? "" : value;
            (string currentMethod, string currentPath) = FirewallWriteEndpoint.SplitSetting(baseValue);
            return FirewallWriteEndpoint.CombineSetting(method ?? currentMethod, path ?? currentPath);
        }

        /// <summary>Returns the field definition of a setting key, or null if the key is not edited in the UI.</summary>
        public static DeviceSpecificFieldDefinition? FindField(DeviceSpecificSettingKey key) => Fields.FirstOrDefault(f => f.Key == key);

        /// <summary>Fields offered at the given node, honouring the level range and the Fortinet restriction.</summary>
        public static IEnumerable<DeviceSpecificFieldDefinition> FieldsFor(DeviceSpecificNode node) =>
            Fields.Where(f => f.AppliesTo(node));

        /// <summary>Splits a comma separated list, keeping empty entries so half-filled editor rows survive a re-render.</summary>
        public static List<string> ParseStringList(string value) =>
            value.Length == 0 ? [] : [.. value.Split(kListSeparator, StringSplitOptions.TrimEntries)];

        /// <summary>Creates the settings DTO of a scope type holding only the compiled defaults.</summary>
        public static GlobalDeviceSpecificSettings CreateDefaults(DeviceSpecificScopeType scopeType) => scopeType switch
        {
            DeviceSpecificScopeType.Global => new GlobalDeviceSpecificSettings(),
            DeviceSpecificScopeType.DeviceType => new DeviceTypeDeviceSpecificSettings(),
            DeviceSpecificScopeType.Management => new ManagementDeviceSpecificSettings(),
            DeviceSpecificScopeType.Gateway => new GatewayDeviceSpecificSettings(),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined device specific settings scope type is required.")
        };

        /// <summary>
        /// Builds the Global / DeviceType / Management / Gateway tree from the real managements and their gateways.
        /// Every tree node is linked to its persisted device specific settings node if one exists, otherwise it has NodeId 0.
        /// Hidden managements and pure routing device types are left out.
        /// </summary>
        public static DeviceSpecificNode BuildHierarchy(IEnumerable<Management> managements, IEnumerable<DeviceSpecificSettingsScope> persistedScopes)
        {
            Dictionary<(DeviceSpecificScopeType, string), DeviceSpecificSettingsScope> persisted =
                persistedScopes.ToDictionary(scope => (scope.ScopeType, scope.ObjectKey));

            DeviceSpecificNode global = NewNode(persisted, DeviceSpecificScopePath.Global(), GlobalDisplayName, null, false);

            IEnumerable<IGrouping<int, Management>> byDeviceType = managements
                .Where(m => !m.HideInUi && !m.DeviceType.IsPureRoutingDevice)
                .GroupBy(m => m.DeviceType.Id)
                .OrderBy(group => group.First().DeviceType.NameVersion(), StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<int, Management> group in byDeviceType)
            {
                DeviceType deviceType = group.First().DeviceType;
                bool isFortinet = deviceType.Manufacturer == FortinetManufacturer;
                DeviceSpecificNode deviceTypeNode = NewNode(persisted, DeviceSpecificScopePath.ForDeviceType(deviceType),
                    deviceType.NameVersion(), global, isFortinet, deviceType.Id);

                foreach (Management management in group.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                {
                    DeviceSpecificNode managementNode = NewNode(persisted, DeviceSpecificScopePath.ForManagement(management),
                        management.Name, deviceTypeNode, isFortinet, deviceType.Id);

                    foreach (Device device in management.Devices.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        NewNode(persisted, DeviceSpecificScopePath.ForGateway(management, device), device.Name ?? ObjectKey(device.Id), managementNode, isFortinet,
                            deviceType.Id);
                    }
                }
            }
            return global;
        }

        /// <summary>Finds the tree node for a scope, matched by scope type and object key.</summary>
        public static DeviceSpecificNode? FindNode(DeviceSpecificNode root, DeviceSpecificSettingsScope scope) =>
            root.SelfAndDescendants().FirstOrDefault(n => n.Level == scope.ScopeType && n.Scope.ObjectKey == scope.ObjectKey);

        private static string ObjectKey(int id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static DeviceSpecificNode NewNode(
            Dictionary<(DeviceSpecificScopeType, string), DeviceSpecificSettingsScope> persisted,
            DeviceSpecificScopePath path,
            string name,
            DeviceSpecificNode? parent,
            bool isFortinet,
            int? deviceTypeId = null)
        {
            DeviceSpecificSettingsScope scope = persisted.TryGetValue((path.ScopeType, path.ObjectKey), out DeviceSpecificSettingsScope? stored)
                ? stored
                : new() { ScopeType = path.ScopeType, ObjectKey = path.ObjectKey, DisplayName = name };

            DeviceSpecificNode node = new()
            {
                Scope = scope,
                Path = path,
                Name = name,
                Parent = parent,
                IsFortinet = isFortinet,
                DeviceTypeId = deviceTypeId
            };
            parent?.Children.Add(node);
            return node;
        }

        private static DeviceSpecificFieldDefinition EnumField<TEnum>(
            DeviceSpecificSettingKey<TEnum> key,
            string textKey,
            Func<GlobalDeviceSpecificSettings, TEnum> get,
            List<DeviceSpecificFieldOption> options,
            DeviceSpecificScopeType minLevel = DeviceSpecificScopeType.Global,
            DeviceSpecificScopeType maxLevel = DeviceSpecificScopeType.Gateway) where TEnum : struct, Enum => new()
            {
                Key = key,
                LabelTextKey = textKey,
                HelpTextKey = HelpKey(textKey),
                Kind = DeviceSpecificFieldKind.SingleSelect,
                Options = options,
                MinLevel = minLevel,
                MaxLevel = maxLevel,
                Read = s => get(s).ToString(),
                SetOverride = (changes, value) => changes.Set(key, Enum.Parse<TEnum>(value))
            };

        private static DeviceSpecificFieldDefinition TextField(
            DeviceSpecificSettingKey<string> key,
            string textKey,
            Func<GlobalDeviceSpecificSettings, string> get,
            DeviceSpecificFieldKind kind = DeviceSpecificFieldKind.Text,
            DeviceSpecificScopeType minLevel = DeviceSpecificScopeType.Global,
            bool fortinetOnly = false,
            DeviceSpecificScopeType maxLevel = DeviceSpecificScopeType.Gateway) => new()
            {
                Key = key,
                LabelTextKey = textKey,
                HelpTextKey = HelpKey(textKey),
                Kind = kind,
                MinLevel = minLevel,
                MaxLevel = maxLevel,
                FortinetOnly = fortinetOnly,
                Read = get,
                SetOverride = (changes, value) => changes.Set(key, value)
            };

        private static DeviceSpecificFieldDefinition BoolField(
            DeviceSpecificSettingKey<bool> key,
            string textKey,
            Func<GlobalDeviceSpecificSettings, bool> get,
            DeviceSpecificScopeType maxLevel = DeviceSpecificScopeType.Gateway,
            DeviceSpecificFieldSection section = DeviceSpecificFieldSection.General) => new()
            {
                Key = key,
                LabelTextKey = textKey,
                HelpTextKey = HelpKey(textKey),
                Kind = DeviceSpecificFieldKind.SingleSelect,
                Options = BoolOptions,
                MaxLevel = maxLevel,
                Section = section,
                FirewallWriteOnly = section == DeviceSpecificFieldSection.FirewallWrite,
                Read = s => get(s).ToString(),
                SetOverride = (changes, value) => changes.Set(key, bool.Parse(value))
            };

        /// <summary>An empty selection means undefined, which is never stored, so the level inherits instead.</summary>
        private static void SetExtTicketSystemOverride(DeviceSpecificSettingsChangeSet changes, string value)
        {
            if (value.Length == 0)
            {
                changes.Remove(DeviceSpecificSettingKeys.RerouteToExternalTicketSystem);
            }
            else
            {
                changes.Set(DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, (int?)int.Parse(value, CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Selectable external ticket systems as (ID, display name). A stored ID no longer configured stays selectable
        /// so the editor still shows it.
        /// </summary>
        public static List<(string Value, string Name)> ExtTicketSystemOptions(string? configuredTicketSystems, string currentValue)
        {
            List<(string Value, string Name)> options = [.. ExternalTicketSystem.ParseConfigured(configuredTicketSystems)
                .Select(system => (system.Id.ToString(CultureInfo.InvariantCulture), $"{system.Name} ({system.Id})"))];
            if (currentValue.Length > 0 && !options.Any(option => option.Value == currentValue))
            {
                options.Add((currentValue, currentValue));
            }
            return options;
        }

        /// <summary>Maps labels to the sorted device specific settings help entries.</summary>
        private static string HelpKey(string textKey) =>
            HelpTextKeys.TryGetValue(textKey, out string? helpKey)
                ? helpKey : throw new ArgumentException($"Missing help for {textKey}", nameof(textKey));

        /// <summary>Builds the option list of a select field from the real data-layer enum members.</summary>
        private static List<DeviceSpecificFieldOption> EnumOptions<TEnum>(params (TEnum Value, string TextKey)[] entries) where TEnum : struct, Enum =>
            [.. entries.Select(e => new DeviceSpecificFieldOption(e.Value.ToString(), e.TextKey))];

    }
}
