using FWO.Data;
using FWO.Data.Provisioning;

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
        StringList
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

        public bool AppliesToLevel(ProvisioningScopeType level) => level >= MinLevel && level <= MaxLevel;
    }

    /// <summary>
    /// One node of the provisioning-settings tree shown in the UI. The tree mirrors the real device
    /// types, managements and gateways; Scope carries the persisted provisioning node (NodeId 0 while
    /// nothing has been stored for this node yet).
    /// </summary>
    public sealed class ProvisioningNode
    {
        public required ProvisioningSettingsScope Scope { get; set; }

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
        public const string GlobalObjectKey = "global";
        public const string GlobalDisplayName = "Global";
        public const string FortinetManufacturer = "Fortinet";

        private const char kListSeparator = ',';

        /// <summary>
        /// Field definitions. Path analysis is only offered globally and object creation only down to
        /// management level, even though the setting keys would technically allow more specific scopes.
        /// </summary>
        public static readonly IReadOnlyList<ProvisioningFieldDefinition> Fields =
        [
            EnumField(ProvisioningSettingKeys.ImplementationMode, "prov_implementation_mode", s => s.ImplementationMode,
                EnumOptions(
                    (ProvisioningImplementationMode.FwoAuto, "prov_opt_fwo_auto"),
                    (ProvisioningImplementationMode.Manual, "prov_opt_manual"),
                    (ProvisioningImplementationMode.TufinSc, "prov_opt_tufin_sc"),
                    (ProvisioningImplementationMode.None, "prov_opt_no_implementation"))),
            TextField(ProvisioningSettingKeys.InstallOn, "prov_install_on", s => s.InstallOn),
            EnumField(ProvisioningSettingKeys.PathAnalysisAlgorithm, "prov_path_analysis_algorithm", s => s.PathAnalysisAlgorithm,
                EnumOptions(
                    (ProvisioningPathAnalysisAlgorithm.StaticListsPerSubnet, "prov_opt_static_lists_per_subnet"),
                    (ProvisioningPathAnalysisAlgorithm.ManualPlanning, "prov_opt_manual_planning"),
                    (ProvisioningPathAnalysisAlgorithm.AskExternalApi, "prov_opt_ask_external_api")),
                maxLevel: ProvisioningScopeType.Global),
            EnumField(ProvisioningSettingKeys.Logging, "prov_logging", s => s.Logging,
                EnumOptions(
                    (ProvisioningLoggingMode.Log, "prov_opt_log"),
                    (ProvisioningLoggingMode.LogTrack, "prov_opt_log_track"),
                    (ProvisioningLoggingMode.None, "prov_opt_none"))),
            EnumField(ProvisioningSettingKeys.ServiceObjectCreation, "prov_service_object_creation", s => s.ServiceObjectCreation,
                ObjectCreationOptions(), maxLevel: ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.AddressObjectCreation, "prov_address_object_creation", s => s.AddressObjectCreation,
                ObjectCreationOptions(), maxLevel: ProvisioningScopeType.Management),
            EnumField(ProvisioningSettingKeys.RuleType, "prov_rule_type", s => s.RuleType,
                EnumOptions(
                    (ProvisioningRuleType.AlwaysAccess, "prov_opt_always_access"),
                    (ProvisioningRuleType.HandleAccessAndNat, "prov_opt_access_nat"),
                    (ProvisioningRuleType.HandleAccessAndIps, "prov_opt_access_ips"),
                    (ProvisioningRuleType.HandleAccessNatIps, "prov_opt_access_nat_ips"))),
            TextField(ProvisioningSettingKeys.Templates, "prov_templates", s => s.Templates, ProvisioningFieldKind.TemplateText),
            EnumField(ProvisioningSettingKeys.PositioningAlgorithm, "prov_positioning_algorithm", s => AsDevice(s).PositioningAlgorithm,
                EnumOptions(
                    (ProvisioningPositioningAlgorithm.CheckPointInlineLayerPerZonePair, "prov_opt_cp_inline_layer_zone_pair"),
                    (ProvisioningPositioningAlgorithm.FortinetEndOfZone, "prov_opt_fortinet_end_of_zone"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection, "prov_opt_cp_end_of_app_section"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSectionDistinguishCommonServices, "prov_opt_cp_end_of_app_section_common_svc"),
                    (ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "prov_opt_default_end_of_rulebase")),
                minLevel: ProvisioningScopeType.DeviceType),
            EnumField(ProvisioningSettingKeys.RuleCategory, "prov_rule_category", s => AsDevice(s).RuleCategory,
                EnumOptions(
                    (ProvisioningRuleCategory.App, "prov_opt_app"),
                    (ProvisioningRuleCategory.CommonService, "prov_opt_common_service")),
                minLevel: ProvisioningScopeType.DeviceType),
            new()
            {
                Key = ProvisioningSettingKeys.SecurityProfiles,
                LabelTextKey = "prov_security_profiles",
                HelpTextKey = "prov_security_profiles_help",
                Kind = ProvisioningFieldKind.StringList,
                MinLevel = ProvisioningScopeType.DeviceType,
                FortinetOnly = true,
                Read = s => string.Join(kListSeparator, AsDevice(s).SecurityProfiles),
                SetOverride = (changes, value) => changes.Set(ProvisioningSettingKeys.SecurityProfiles,
                    ParseStringList(value).Where(entry => entry.Length > 0).ToList())
            },
            TextField(ProvisioningSettingKeys.ZoneFrom, "prov_zone_from", s => AsDevice(s).ZoneFrom,
                minLevel: ProvisioningScopeType.DeviceType, fortinetOnly: true),
            TextField(ProvisioningSettingKeys.ZoneTo, "prov_zone_to", s => AsDevice(s).ZoneTo,
                minLevel: ProvisioningScopeType.DeviceType, fortinetOnly: true)
        ];

        public static ProvisioningFieldDefinition? FindField(ProvisioningSettingKey key) => Fields.FirstOrDefault(f => f.Key == key);

        /// <summary>Fields offered at the given node, honouring the level range and the Fortinet restriction.</summary>
        public static IEnumerable<ProvisioningFieldDefinition> FieldsFor(ProvisioningNode node) =>
            Fields.Where(f => f.AppliesToLevel(node.Level) && (!f.FortinetOnly || node.IsFortinet));

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

            ProvisioningNode global = NewNode(persisted, ProvisioningScopeType.Global, GlobalObjectKey, GlobalDisplayName, null, false);

            IEnumerable<IGrouping<int, Management>> byDeviceType = managements
                .Where(m => !m.HideInUi && !m.DeviceType.IsPureRoutingDevice)
                .GroupBy(m => m.DeviceType.Id)
                .OrderBy(group => group.First().DeviceType.NameVersion(), StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<int, Management> group in byDeviceType)
            {
                DeviceType deviceType = group.First().DeviceType;
                bool isFortinet = deviceType.Manufacturer == FortinetManufacturer;
                ProvisioningNode deviceTypeNode = NewNode(persisted, ProvisioningScopeType.DeviceType,
                    ObjectKey(deviceType.Id), deviceType.NameVersion(), global, isFortinet);

                foreach (Management management in group.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                {
                    ProvisioningNode managementNode = NewNode(persisted, ProvisioningScopeType.Management,
                        ObjectKey(management.Id), management.Name, deviceTypeNode, isFortinet);

                    foreach (Device device in management.Devices.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        NewNode(persisted, ProvisioningScopeType.Gateway, ObjectKey(device.Id), device.Name ?? ObjectKey(device.Id), managementNode, isFortinet);
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
            ProvisioningScopeType scopeType,
            string objectKey,
            string name,
            ProvisioningNode? parent,
            bool isFortinet)
        {
            ProvisioningSettingsScope scope = persisted.TryGetValue((scopeType, objectKey), out ProvisioningSettingsScope? stored)
                ? stored
                : new() { ScopeType = scopeType, ObjectKey = objectKey, DisplayName = name };

            ProvisioningNode node = new() { Scope = scope, Name = name, Parent = parent, IsFortinet = isFortinet };
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
                HelpTextKey = textKey + "_help",
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
            bool fortinetOnly = false) => new()
            {
                Key = key,
                LabelTextKey = textKey,
                HelpTextKey = textKey + "_help",
                Kind = kind,
                MinLevel = minLevel,
                FortinetOnly = fortinetOnly,
                Read = get,
                SetOverride = (changes, value) => changes.Set(key, value)
            };

        /// <summary>Builds the option list of a select field from the real data-layer enum members.</summary>
        private static List<ProvisioningFieldOption> EnumOptions<TEnum>(params (TEnum Value, string TextKey)[] entries) where TEnum : struct, Enum =>
            [.. entries.Select(e => new ProvisioningFieldOption(e.Value.ToString(), e.TextKey))];

        private static List<ProvisioningFieldOption> ObjectCreationOptions() =>
            EnumOptions(
                (ProvisioningObjectCreationMode.Supermanager, "prov_opt_supermanager"),
                (ProvisioningObjectCreationMode.Submanager, "prov_opt_submanager"));

        /// <summary>Device-level fields are only offered from device type downwards, where the DTO always carries them.</summary>
        private static DeviceTypeProvisioningSettings AsDevice(GlobalProvisioningSettings settings) =>
            settings as DeviceTypeProvisioningSettings
                ?? throw new InvalidOperationException($"'{settings.GetType().Name}' does not carry device-level provisioning settings.");
    }
}
