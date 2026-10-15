using FWO.Data;
using FWO.Data.Provisioning;
using System.Globalization;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// How a provisioning-setting field should be presented/edited in the UI.
    /// </summary>
    public enum ProvisioningFieldKind
    {
        SingleSelect,
        Text,
        TemplateText,
        StringList,
        ExtTicketSystem
    }

    /// <summary>
    /// One selectable option for a SingleSelect field. Value is the name of the underlying data-layer enum member,
    /// TextKey the UI text key of its display name.
    /// </summary>
    public sealed class ProvisioningFieldOption(string value, string textKey)
    {
        public string Value { get; } = value;
        public string TextKey { get; } = textKey;
    }

    /// <summary>
    /// Definition (schema) of a single provisioning setting in the UI: which typed setting key it edits,
    /// how it is edited, which scopes it is offered at, and how its value is converted between the
    /// data-layer DTOs and the editor's text representation.
    /// </summary>
    public sealed class ProvisioningFieldDefinition
    {
        public required ProvisioningSettingKey Key { get; init; }
        public required string LabelTextKey { get; init; }
        public required string HelpTextKey { get; init; }
        public ProvisioningFieldKind Kind { get; init; } = ProvisioningFieldKind.SingleSelect;
        public List<ProvisioningFieldOption> Options { get; init; } = [];

        /// <summary>Lowest (most general) scope at which this field is offered.</summary>
        public ProvisioningScopeType MinLevel { get; init; } = ProvisioningScopeType.Global;

        /// <summary>Highest (most specific) scope at which this field is offered.</summary>
        public ProvisioningScopeType MaxLevel { get; init; } = ProvisioningScopeType.Gateway;

        /// <summary>Only shown for scopes below a Fortinet device type.</summary>
        public bool FortinetOnly { get; init; } = false;

        /// <summary>Formats the field's value of a resolved settings object for the editor.</summary>
        public required Func<GlobalProvisioningSettings, string> Read { get; init; }

        /// <summary>Adds an override with the given editor value for this field to a change set.</summary>
        public required Action<ProvisioningSettingsChangeSet, string> SetOverride { get; init; }

        public string Id => Key.DatabaseKey;

        /// <summary>Whether the field is offered at the given level.</summary>
        public bool AppliesToLevel(ProvisioningScopeType level) => level >= MinLevel && level <= MaxLevel;

        /// <summary>Whether the field is offered on the node's current device path.</summary>
        public bool AppliesTo(ProvisioningNode node) => AppliesToLevel(node.Level) && (!FortinetOnly || node.IsFortinet || node.Level == ProvisioningScopeType.Global);
    }

    /// <summary>
    /// One node of the provisioning-settings tree shown in the UI. The tree mirrors the real device
    /// types, managements and gateways; Scope carries the persisted provisioning node (NodeId 0 while
    /// nothing has been stored for this node yet).
    /// </summary>
    public sealed class ProvisioningNode
    {
        public required ProvisioningSettingsScope Scope { get; set; }

        /// <summary>Path of the node through the current device hierarchy; settings are resolved along it.</summary>
        public required ProvisioningScopePath Path { get; init; }

        /// <summary>Current name of the underlying object (device type, management, gateway).</summary>
        public required string Name { get; init; }

        public ProvisioningNode? Parent { get; init; }

        public List<ProvisioningNode> Children { get; } = [];

        /// <summary>True for nodes at or below a Fortinet device type.</summary>
        public bool IsFortinet { get; init; }

        public string Id => $"{Level}:{Scope.ObjectKey}";
        public ProvisioningScopeType Level => Scope.ScopeType;
        public bool IsPersisted => Scope.NodeId > 0;

        /// <summary>Walks from this node up to Global, this node first.</summary>
        public IEnumerable<ProvisioningNode> SelfAndAncestors()
        {
            for (ProvisioningNode? current = this; current != null; current = current.Parent)
            {
                yield return current;
            }
        }

        /// <summary>This node followed by all nodes below it, depth first.</summary>
        public IEnumerable<ProvisioningNode> SelfAndDescendants()
        {
            yield return this;
            foreach (ProvisioningNode descendant in Children.SelectMany(child => child.SelfAndDescendants()))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Field schema over the typed provisioning setting keys, plus building the Global / DeviceType /
    /// Management / Gateway tree from the real managements. Drives the settings/fwconfigprovisioning UI.
    /// </summary>
    public static class ProvisioningSettingsData
    {
        public const string GlobalObjectKey = ProvisioningScopePath.GlobalObjectKey;
        public const string GlobalDisplayName = ProvisioningScopePath.GlobalDisplayName;
        public const string FortinetManufacturer = "Fortinet";

        private const char kListSeparator = ',';

        private static readonly Dictionary<string, string> HelpTextKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "prov_implementation_mode", "H5168" },
            { "prov_install_on", "H5169" },
            { "prov_time_object_creation", "H5170" },
            { "prov_label_creation", "H5172" },
            { "prov_implementation_mode_override_for_rules", "H5173" },
            { "prov_owner_id_custom_field_name", "H5174" },
            { "prov_change_id_custom_field_name", "H5175" },
            { "prov_reroute_to_external_ticket_system", "H5176" },
            { "prov_plan_implementation", "H5177" },
            { "prov_plan_implementation_override_for_rules", "H5178" },
            { "prov_modelling_create_app_zones", "H5179" },
            { "prov_modelling_implement_network_area_objects", "H5180" },
            { "prov_modelling_dyn_ip_labels_visible_as_groups", "H5188" },
            { "prov_importer_host_name", "H5189" },
            { "prov_logging", "H5190" },
            { "prov_service_object_creation", "H5191" },
            { "prov_address_object_creation", "H5192" },
            { "prov_rule_type", "H5193" },
            { "prov_templates", "H5194" },
            { "prov_positioning_algorithm", "H5195" },
            { "prov_common_service_separation", "H5196" },
            { "prov_zone_from", "H5198" },
            { "prov_zone_to", "H5199" },
            { "prov_modelling_connection_id_field", "H5200" },
            { "prov_modelling_connection_id_template", "H5200" },
        };

        private static readonly List<ProvisioningFieldOption> BoolOptions =
        [
            new(bool.TrueString, "yes"),
            new(bool.FalseString, "no")
        ];

        /// <summary>Localized editors; config may store deeper overrides than the UI offers (MaxLevel).</summary>
        public static readonly IReadOnlyList<ProvisioningFieldDefinition> Fields =
        [
            EnumField(ProvisioningSettingKeys.InstallOn, "prov_install_on", s => s.InstallOn,
                EnumOptions(
                    (ProvisioningInstallOn.Any, "prov_opt_any"),
                    (ProvisioningInstallOn.GatewaysInPath, "prov_opt_gateways_in_path"))),
            EnumField(ProvisioningSettingKeys.PositioningAlgorithm, "prov_positioning_algorithm", s => s.PositioningAlgorithm,
                EnumOptions(
                    (ProvisioningPositioningAlgorithm.CheckPointInlineLayerPerZonePair, "prov_opt_cp_inline_layer_zone_pair"),
                    (ProvisioningPositioningAlgorithm.FortinetEndOfZone, "prov_opt_fortinet_end_of_zone"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection, "prov_opt_cp_end_of_app_section"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSectionDistinguishCommonServices, "prov_opt_cp_end_of_app_section_common_svc"),
                    (ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "prov_opt_default_end_of_rulebase"),
                    (ProvisioningPositioningAlgorithm.AllowCategoryEndOfAppSectionDistinguishCommonServices, "prov_opt_allow_category"))),
            EnumField(ProvisioningSettingKeys.RuleLoggingOverride, "prov_logging", s => s.RuleLoggingOverride,
                EnumOptions(
                    (ProvisioningLoggingMode.Log, "prov_opt_log"),
                    (ProvisioningLoggingMode.LogTrack, "prov_opt_log_track"),
                    (ProvisioningLoggingMode.None, "prov_opt_none"))),
            EnumField(ProvisioningSettingKeys.ServiceObjectCreation, "prov_service_object_creation", s => s.ServiceObjectCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "prov_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "prov_opt_submanager")), maxLevel: ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.AddressObjectCreation, "prov_address_object_creation", s => s.AddressObjectCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "prov_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "prov_opt_submanager")), maxLevel: ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.TimeObjectCreation, "prov_time_object_creation", s => s.TimeObjectCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "prov_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "prov_opt_submanager"),
                    (ProvisioningObjectCreationMode.Ignore, "prov_opt_ignore")), maxLevel: ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.LabelCreation, "prov_label_creation", s => s.LabelCreation,
                EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "prov_opt_supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "prov_opt_submanager"),
                    (ProvisioningObjectCreationMode.Ignore, "prov_opt_ignore")), maxLevel: ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.RuleType, "prov_rule_type", s => s.RuleType,
                EnumOptions(
                    (ProvisioningRuleType.AlwaysAccess, "prov_opt_always_access"),
                    (ProvisioningRuleType.HandleAccessAndNat, "prov_opt_access_nat"),
                    (ProvisioningRuleType.HandleAccessAndIps, "prov_opt_access_ips"),
                    (ProvisioningRuleType.HandleAccessNatIps, "prov_opt_access_nat_ips"),
                    (ProvisioningRuleType.AppSecurity, "prov_opt_app_security")), maxLevel: ProvisioningScopeType.Management),
            BoolField(ProvisioningSettingKeys.CommonServiceSeparation, "prov_common_service_separation", s => s.CommonServiceSeparation,
                ProvisioningScopeType.Management),
            TextField(ProvisioningSettingKeys.RuleZoneFromOverride, "prov_zone_from", s => s.RuleZoneFromOverride, fortinetOnly: true,
                maxLevel: ProvisioningScopeType.Management),
            TextField(ProvisioningSettingKeys.RuleZoneToOverride, "prov_zone_to", s => s.RuleZoneToOverride, fortinetOnly: true,
                maxLevel: ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.ImplementationMode, "prov_implementation_mode", s => s.ImplementationMode,
                EnumOptions(
                    (ProvisioningImplementationMode.Automatic, "prov_opt_automatic"),
                    (ProvisioningImplementationMode.Manual, "prov_opt_manual"),
                    (ProvisioningImplementationMode.RerouteToExternalTicketSystem, "prov_opt_reroute"))),
            EnumField(ProvisioningSettingKeys.ImplementationModeOverrideForRules, "prov_implementation_mode_override_for_rules", s => s.ImplementationModeOverrideForRules,
                EnumOptions(
                    (ProvisioningRuleImplementationMode.Automatic, "prov_opt_automatic"),
                    (ProvisioningRuleImplementationMode.Manual, "prov_opt_manual"),
                    (ProvisioningRuleImplementationMode.RerouteToExternalTicketSystem, "prov_opt_reroute"))),
            new()
            {
                Key = ProvisioningSettingKeys.RerouteToExternalTicketSystem,
                LabelTextKey = "prov_reroute_to_external_ticket_system",
                HelpTextKey = HelpKey("prov_reroute_to_external_ticket_system"),
                Kind = ProvisioningFieldKind.ExtTicketSystem,
                Read = s => s.RerouteToExternalTicketSystem?.ToString(CultureInfo.InvariantCulture) ?? "",
                SetOverride = SetExtTicketSystemOverride
            },
            new()
            {
                Key = ProvisioningSettingKeys.SecurityProfiles,
                LabelTextKey = "prov_security_profiles",
                HelpTextKey = "H5197",
                Kind = ProvisioningFieldKind.StringList,
                MinLevel = ProvisioningScopeType.DeviceType,
                MaxLevel = ProvisioningScopeType.Management,
                Read = s => string.Join(kListSeparator, s.SecurityProfiles),
                SetOverride = (changes, value) => changes.Set(ProvisioningSettingKeys.SecurityProfiles,
                    ParseStringList(value).Where(entry => entry.Length > 0).ToList())
            },
            TextField(ProvisioningSettingKeys.OwnerIdCustomFieldName, "prov_owner_id_custom_field_name", s => s.OwnerIdCustomFieldName, maxLevel: ProvisioningScopeType.Management),
            TextField(ProvisioningSettingKeys.ChangeIdCustomFieldName, "prov_change_id_custom_field_name", s => s.ChangeIdCustomFieldName, maxLevel: ProvisioningScopeType.Management),
            TextField(ProvisioningSettingKeys.ImporterHostName, "prov_importer_host_name", s => s.ImporterHostName, maxLevel: ProvisioningScopeType.Management),
            BoolField(ProvisioningSettingKeys.PlanImplementation, "prov_plan_implementation", s => s.PlanImplementation),
            BoolField(ProvisioningSettingKeys.PlanImplementationOverrideForRules, "prov_plan_implementation_override_for_rules", s => s.PlanImplementationOverrideForRules),
            BoolField(ProvisioningSettingKeys.ModellingCreateAppZones, "prov_modelling_create_app_zones", s => s.ModellingCreateAppZones,
                ProvisioningScopeType.Management),
            BoolField(ProvisioningSettingKeys.ModellingImplementNetworkAreaObjects, "prov_modelling_implement_network_area_objects", s => s.ModellingImplementNetworkAreaObjects,
                ProvisioningScopeType.Management),
            BoolField(ProvisioningSettingKeys.ModellingDynIpLabelsVisibleAsGroups, "prov_modelling_dyn_ip_labels_visible_as_groups", s => s.ModellingDynIpLabelsVisibleAsGroups,
                ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.ModellingConnectionIdField, "prov_modelling_connection_id_field", s => s.ModellingConnectionIdField,
                EnumOptions(
                    (ProvisioningConnectionIdField.Name, "name"),
                    (ProvisioningConnectionIdField.Comment, "comment"),
                    (ProvisioningConnectionIdField.CustomField, "prov_opt_custom_field")), maxLevel: ProvisioningScopeType.Management),
            TextField(ProvisioningSettingKeys.ModellingConnectionIdTemplate, "prov_modelling_connection_id_template", s => s.ModellingConnectionIdTemplate,
                maxLevel: ProvisioningScopeType.Management),
            TextField(ProvisioningSettingKeys.Templates, "prov_templates", s => s.Templates, ProvisioningFieldKind.TemplateText,
                minLevel: ProvisioningScopeType.DeviceType, maxLevel: ProvisioningScopeType.DeviceType),
        ];

        /// <summary>Returns the field definition of a setting key, or null if the key is not edited in the UI.</summary>
        public static ProvisioningFieldDefinition? FindField(ProvisioningSettingKey key) => Fields.FirstOrDefault(f => f.Key == key);

        /// <summary>Fields offered at the given node, honouring the level range and the Fortinet restriction.</summary>
        public static IEnumerable<ProvisioningFieldDefinition> FieldsFor(ProvisioningNode node) =>
            Fields.Where(f => f.AppliesTo(node));

        /// <summary>Splits a comma separated list, keeping empty entries so half-filled editor rows survive a re-render.</summary>
        public static List<string> ParseStringList(string value) =>
            value.Length == 0 ? [] : [.. value.Split(kListSeparator, StringSplitOptions.TrimEntries)];

        /// <summary>Creates the settings DTO of a scope type holding only the compiled defaults.</summary>
        public static GlobalProvisioningSettings CreateDefaults(ProvisioningScopeType scopeType) => scopeType switch
        {
            ProvisioningScopeType.Global => new GlobalProvisioningSettings(),
            ProvisioningScopeType.DeviceType => new DeviceTypeProvisioningSettings(),
            ProvisioningScopeType.Management => new ManagementProvisioningSettings(),
            ProvisioningScopeType.Gateway => new GatewayProvisioningSettings(),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined provisioning scope type is required.")
        };

        /// <summary>
        /// Builds the Global / DeviceType / Management / Gateway tree from the real managements and their gateways.
        /// Every tree node is linked to its persisted provisioning node if one exists, otherwise it has NodeId 0.
        /// Hidden managements and pure routing device types are left out.
        /// </summary>
        public static ProvisioningNode BuildHierarchy(IEnumerable<Management> managements, IEnumerable<ProvisioningSettingsScope> persistedScopes)
        {
            Dictionary<(ProvisioningScopeType, string), ProvisioningSettingsScope> persisted =
                persistedScopes.ToDictionary(scope => (scope.ScopeType, scope.ObjectKey));

            ProvisioningNode global = NewNode(persisted, ProvisioningScopePath.Global(), GlobalDisplayName, null, false);

            IEnumerable<IGrouping<int, Management>> byDeviceType = managements
                .Where(m => !m.HideInUi && !m.DeviceType.IsPureRoutingDevice)
                .GroupBy(m => m.DeviceType.Id)
                .OrderBy(group => group.First().DeviceType.NameVersion(), StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<int, Management> group in byDeviceType)
            {
                DeviceType deviceType = group.First().DeviceType;
                bool isFortinet = deviceType.Manufacturer == FortinetManufacturer;
                ProvisioningNode deviceTypeNode = NewNode(persisted, ProvisioningScopePath.ForDeviceType(deviceType),
                    deviceType.NameVersion(), global, isFortinet);

                foreach (Management management in group.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                {
                    ProvisioningNode managementNode = NewNode(persisted, ProvisioningScopePath.ForManagement(management),
                        management.Name, deviceTypeNode, isFortinet);

                    foreach (Device device in management.Devices.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        NewNode(persisted, ProvisioningScopePath.ForGateway(management, device), device.Name ?? ObjectKey(device.Id), managementNode, isFortinet);
                    }
                }
            }
            return global;
        }

        /// <summary>Finds the tree node for a scope, matched by scope type and object key.</summary>
        public static ProvisioningNode? FindNode(ProvisioningNode root, ProvisioningSettingsScope scope) =>
            root.SelfAndDescendants().FirstOrDefault(n => n.Level == scope.ScopeType && n.Scope.ObjectKey == scope.ObjectKey);

        private static string ObjectKey(int id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static ProvisioningNode NewNode(
            Dictionary<(ProvisioningScopeType, string), ProvisioningSettingsScope> persisted,
            ProvisioningScopePath path,
            string name,
            ProvisioningNode? parent,
            bool isFortinet)
        {
            ProvisioningSettingsScope scope = persisted.TryGetValue((path.ScopeType, path.ObjectKey), out ProvisioningSettingsScope? stored)
                ? stored
                : new() { ScopeType = path.ScopeType, ObjectKey = path.ObjectKey, DisplayName = name };

            ProvisioningNode node = new() { Scope = scope, Path = path, Name = name, Parent = parent, IsFortinet = isFortinet };
            parent?.Children.Add(node);
            return node;
        }

        private static ProvisioningFieldDefinition EnumField<TEnum>(
            ProvisioningSettingKey<TEnum> key,
            string textKey,
            Func<GlobalProvisioningSettings, TEnum> get,
            List<ProvisioningFieldOption> options,
            ProvisioningScopeType minLevel = ProvisioningScopeType.Global,
            ProvisioningScopeType maxLevel = ProvisioningScopeType.Gateway) where TEnum : struct, Enum => new()
            {
                Key = key,
                LabelTextKey = textKey,
                HelpTextKey = HelpKey(textKey),
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = options,
                MinLevel = minLevel,
                MaxLevel = maxLevel,
                Read = s => get(s).ToString(),
                SetOverride = (changes, value) => changes.Set(key, Enum.Parse<TEnum>(value))
            };

        private static ProvisioningFieldDefinition TextField(
            ProvisioningSettingKey<string> key,
            string textKey,
            Func<GlobalProvisioningSettings, string> get,
            ProvisioningFieldKind kind = ProvisioningFieldKind.Text,
            ProvisioningScopeType minLevel = ProvisioningScopeType.Global,
            bool fortinetOnly = false,
            ProvisioningScopeType maxLevel = ProvisioningScopeType.Gateway) => new()
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

        private static ProvisioningFieldDefinition BoolField(
            ProvisioningSettingKey<bool> key,
            string textKey,
            Func<GlobalProvisioningSettings, bool> get,
            ProvisioningScopeType maxLevel = ProvisioningScopeType.Gateway) => new()
            {
                Key = key,
                LabelTextKey = textKey,
                HelpTextKey = HelpKey(textKey),
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = BoolOptions,
                MaxLevel = maxLevel,
                Read = s => get(s).ToString(),
                SetOverride = (changes, value) => changes.Set(key, bool.Parse(value))
            };

        /// <summary>An empty selection means undefined, which is never stored, so the level inherits instead.</summary>
        private static void SetExtTicketSystemOverride(ProvisioningSettingsChangeSet changes, string value)
        {
            if (value.Length == 0)
            {
                changes.Remove(ProvisioningSettingKeys.RerouteToExternalTicketSystem);
            }
            else
            {
                changes.Set(ProvisioningSettingKeys.RerouteToExternalTicketSystem, (int?)int.Parse(value, CultureInfo.InvariantCulture));
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

        /// <summary>Maps labels to the sorted provisioning help entries.</summary>
        private static string HelpKey(string textKey) =>
            HelpTextKeys.TryGetValue(textKey, out string? helpKey)
                ? helpKey : throw new ArgumentException($"Missing help for {textKey}", nameof(textKey));

        /// <summary>Builds the option list of a select field from the real data-layer enum members.</summary>
        private static List<ProvisioningFieldOption> EnumOptions<TEnum>(params (TEnum Value, string TextKey)[] entries) where TEnum : struct, Enum =>
            [.. entries.Select(e => new ProvisioningFieldOption(e.Value.ToString(), e.TextKey))];

    }
}
