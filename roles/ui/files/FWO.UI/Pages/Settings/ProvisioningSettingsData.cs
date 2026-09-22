using FWO.Data.Provisioning;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// The three device-type groups the hierarchy currently distinguishes at the
    /// "Per Device Type" level.
    /// </summary>
    public static class ProvisioningDeviceTypeGroups
    {
        public const string CheckPoint = "CheckPoint";
        public const string FortiManager = "FortiManager";
        public const string TufinSC = "TufinSC";

        public static readonly IReadOnlyList<string> All =
        [
            CheckPoint,
            FortiManager,
            TufinSC
        ];
    }

    /// <summary>
    /// How a provisioning-setting field should be presented/edited in the UI.
    /// </summary>
    public enum ProvisioningFieldKind
    {
        SingleSelect,
        MultiSelectTags,
        Text,
        TemplateText,
        StringList
    }

    /// <summary>
    /// One selectable option for a SingleSelect/MultiSelectTags field. Value is the name of the
    /// underlying data-layer enum member.
    /// </summary>
    public sealed class ProvisioningFieldOption(string value, string displayName)
    {
        public string Value { get; } = value;
        public string DisplayName { get; } = displayName;
    }

    /// <summary>
    /// Definition (schema) of a single provisioning-settings key: how it is edited, which scopes it
    /// may be set at, and how it is read from / written to a <see cref="GlobalProvisioningSettings"/>
    /// instance of the data layer.
    /// </summary>
    public sealed class ProvisioningFieldDefinition
    {
        public required string Key { get; init; }
        public required string LabelTextKey { get; init; }
        public required string HelpTextKey { get; init; }
        public ProvisioningFieldKind Kind { get; init; } = ProvisioningFieldKind.SingleSelect;
        public List<ProvisioningFieldOption> Options { get; init; } = [];
        public string DefaultValue { get; init; } = "";

        /// <summary>Lowest (most general) scope at which this field may be configured.</summary>
        public ProvisioningScopeType MinLevel { get; init; } = ProvisioningScopeType.Global;

        /// <summary>Highest (most specific) scope at which this field may be configured.</summary>
        public ProvisioningScopeType MaxLevel { get; init; } = ProvisioningScopeType.Gateway;

        /// <summary>Only relevant/shown below (and including) DeviceType for FortiManager device types.</summary>
        public bool FortiManagerOnly { get; init; } = false;

        /// <summary>Reads the field off a settings object; "" means the field is not set at that scope.</summary>
        public required Func<GlobalProvisioningSettings, string> Read { get; init; }

        /// <summary>Writes the field into a settings object; "" clears it.</summary>
        public required Action<GlobalProvisioningSettings, string> Write { get; init; }

        /// <summary>Clears the field so the scope inherits it from its parent again.</summary>
        public required Action<GlobalProvisioningSettings> Clear { get; init; }

        /// <summary>True when this scope defines the field itself rather than inheriting it.</summary>
        public bool IsSetOn(GlobalProvisioningSettings settings) => Read(settings).Length > 0;

        public bool AppliesToLevel(ProvisioningScopeType level) => level >= MinLevel && level <= MaxLevel;
    }

    /// <summary>
    /// One node of the provisioning-settings hierarchy tree. Settings is the data-layer object for
    /// this scope - a <see cref="GlobalProvisioningSettings"/> at the root and one of its subclasses
    /// further down. A field left at its unset value there means "inherit from the parent scope".
    /// </summary>
    public sealed class ProvisioningNode
    {
        public required GlobalProvisioningSettings Settings { get; init; }

        /// <summary>Device-type group this node belongs to (set on DeviceType nodes and inherited down); empty for Global.</summary>
        public string DeviceTypeGroup { get; init; } = "";

        public string Id => Settings.Scope.ObjectKey;
        public string Name => Settings.Scope.DisplayName ?? Settings.Scope.ObjectKey;
        public ProvisioningScopeType Level => Settings.Scope.ScopeType;
        public long NodeId => Settings.Scope.NodeId;
        public long? ParentNodeId => Settings.Scope.ParentNodeId;
    }

    /// <summary>
    /// Where an effective field value came from, for display ("inherited from ..." vs "set here").
    /// SourceNode is null when no node in the chain (including Global) sets the field,
    /// i.e. the value is just the field's schema default with nothing to link to.
    /// </summary>
    public sealed record ProvisioningResolvedValue(string Value, ProvisioningNode? SourceNode, bool IsOverriddenHere);

    /// <summary>
    /// Field schema over the FWO.Data.Provisioning settings classes, plus an in-memory hierarchy
    /// standing in for the not-yet-built getAllProvisioningSettings backend. Only used to drive the
    /// settings/fwconfigprovisioning UI.
    /// </summary>
    public static class ProvisioningSettingsData
    {
        public const string GlobalNodeKey = "global";

        public const string FieldImplementationMode = "implementation_mode";
        public const string FieldInstallOn = "install_on";
        public const string FieldPathAnalysisAlgorithm = "path_analysis_algorithm";
        public const string FieldLogging = "logging";
        public const string FieldServiceObjectCreation = "service_object_creation";
        public const string FieldAddressObjectCreation = "address_object_creation";
        public const string FieldRuleType = "rule_type";
        public const string FieldTemplates = "templates";
        public const string FieldPositioningAlgorithm = "positioning_algorithm";
        public const string FieldRuleCategory = "rule_category";
        public const string FieldSecurityProfiles = "security_profiles";
        public const string FieldZoneFrom = "zone_from";
        public const string FieldZoneTo = "zone_to";

        private const char kListSeparator = ',';

        /// <summary>
        /// Field definitions. Two source keys (path_analysis_algorithm, service/address_object_creation)
        /// were listed under both "all levels" and a more specific level range in the spec;
        /// the more specific range (global-only / all-but-gateway) is treated as authoritative here.
        /// Default values mirror the constructor defaults of the data-layer settings classes.
        /// </summary>
        public static readonly IReadOnlyList<ProvisioningFieldDefinition> Fields =
        [
            new()
            {
                Key = FieldImplementationMode,
                LabelTextKey = "prov_implementation_mode",
                HelpTextKey = "prov_implementation_mode_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningImplementationMode.FwoAuto, "FWO automatic"),
                    (ProvisioningImplementationMode.Manual, "Manual"),
                    (ProvisioningImplementationMode.TufinSc, "Tufin SecureChange"),
                    (ProvisioningImplementationMode.None, "None (no implementation task)")),
                DefaultValue = nameof(ProvisioningImplementationMode.FwoAuto),
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Gateway,
                Read = ReadEnum(s => s.ImplementationMode),
                Write = WriteEnum<ProvisioningImplementationMode>((s, v) => s.ImplementationMode = v),
                Clear = ClearEnum<ProvisioningImplementationMode>((s, v) => s.ImplementationMode = v)
            },
            new()
            {
                Key = FieldInstallOn,
                LabelTextKey = "prov_install_on",
                HelpTextKey = "prov_install_on_help",
                Kind = ProvisioningFieldKind.Text,
                DefaultValue = "ANY",
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Gateway,
                Read = s => s.InstallOn,
                Write = (s, v) => s.InstallOn = v,
                Clear = s => s.InstallOn = ""
            },
            new()
            {
                Key = FieldPathAnalysisAlgorithm,
                LabelTextKey = "prov_path_analysis_algorithm",
                HelpTextKey = "prov_path_analysis_algorithm_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningPathAnalysisAlgorithm.StaticListsPerSubnet, "Static lists per subnet"),
                    (ProvisioningPathAnalysisAlgorithm.ManualPlanning, "Manual planning"),
                    (ProvisioningPathAnalysisAlgorithm.AskExternalApi, "Ask external API")),
                DefaultValue = nameof(ProvisioningPathAnalysisAlgorithm.StaticListsPerSubnet),
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Global,
                Read = ReadEnum(s => s.PathAnalysisAlgorithm),
                Write = WriteEnum<ProvisioningPathAnalysisAlgorithm>((s, v) => s.PathAnalysisAlgorithm = v),
                Clear = ClearEnum<ProvisioningPathAnalysisAlgorithm>((s, v) => s.PathAnalysisAlgorithm = v)
            },
            new()
            {
                Key = FieldLogging,
                LabelTextKey = "prov_logging",
                HelpTextKey = "prov_logging_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningLoggingMode.Log, "Log"),
                    (ProvisioningLoggingMode.LogTrack, "Log and track"),
                    (ProvisioningLoggingMode.None, "None")),
                DefaultValue = nameof(ProvisioningLoggingMode.Log),
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Gateway,
                Read = ReadEnum(s => s.Logging),
                Write = WriteEnum<ProvisioningLoggingMode>((s, v) => s.Logging = v),
                Clear = ClearEnum<ProvisioningLoggingMode>((s, v) => s.Logging = v)
            },
            new()
            {
                Key = FieldServiceObjectCreation,
                LabelTextKey = "prov_service_object_creation",
                HelpTextKey = "prov_service_object_creation_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "Supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "Submanager")),
                DefaultValue = nameof(ProvisioningObjectCreationMode.Supermanager),
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Management,
                Read = ReadEnum(s => s.ServiceObjectCreation),
                Write = WriteEnum<ProvisioningObjectCreationMode>((s, v) => s.ServiceObjectCreation = v),
                Clear = ClearEnum<ProvisioningObjectCreationMode>((s, v) => s.ServiceObjectCreation = v)
            },
            new()
            {
                Key = FieldAddressObjectCreation,
                LabelTextKey = "prov_address_object_creation",
                HelpTextKey = "prov_address_object_creation_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningObjectCreationMode.Supermanager, "Supermanager"),
                    (ProvisioningObjectCreationMode.Submanager, "Submanager")),
                DefaultValue = nameof(ProvisioningObjectCreationMode.Supermanager),
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Management,
                Read = ReadEnum(s => s.AddressObjectCreation),
                Write = WriteEnum<ProvisioningObjectCreationMode>((s, v) => s.AddressObjectCreation = v),
                Clear = ClearEnum<ProvisioningObjectCreationMode>((s, v) => s.AddressObjectCreation = v)
            },
            new()
            {
                Key = FieldRuleType,
                LabelTextKey = "prov_rule_type",
                HelpTextKey = "prov_rule_type_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningRuleType.AlwaysAccess, "Always access (standard)"),
                    (ProvisioningRuleType.HandleAccessAndNat, "Handle access and NAT"),
                    (ProvisioningRuleType.HandleAccessAndIps, "Handle access and IPS"),
                    (ProvisioningRuleType.HandleAccessNatIps, "Handle access, NAT and IPS")),
                DefaultValue = nameof(ProvisioningRuleType.AlwaysAccess),
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Gateway,
                Read = ReadEnum(s => s.RuleType),
                Write = WriteEnum<ProvisioningRuleType>((s, v) => s.RuleType = v),
                Clear = ClearEnum<ProvisioningRuleType>((s, v) => s.RuleType = v)
            },
            new()
            {
                Key = FieldTemplates,
                LabelTextKey = "prov_templates",
                HelpTextKey = "prov_templates_help",
                Kind = ProvisioningFieldKind.TemplateText,
                DefaultValue = "",
                MinLevel = ProvisioningScopeType.Global,
                MaxLevel = ProvisioningScopeType.Gateway,
                Read = s => s.Templates,
                Write = (s, v) => s.Templates = v,
                Clear = s => s.Templates = ""
            },
            new()
            {
                Key = FieldPositioningAlgorithm,
                LabelTextKey = "prov_positioning_algorithm",
                HelpTextKey = "prov_positioning_algorithm_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningPositioningAlgorithm.CheckPointInlineLayerPerZonePair, "Check Point: inline layer per zone pair"),
                    (ProvisioningPositioningAlgorithm.FortinetEndOfZone, "Fortinet: end of zone"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection, "Check Point: end of app section"),
                    (ProvisioningPositioningAlgorithm.CheckPointEndOfAppSectionDistinguishCommonServices, "Check Point: end of app section, distinguish common services"),
                    (ProvisioningPositioningAlgorithm.DefaultEndOfRulebase, "Default: end of rulebase")),
                DefaultValue = nameof(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase),
                MinLevel = ProvisioningScopeType.DeviceType,
                MaxLevel = ProvisioningScopeType.Gateway,
                Read = ReadEnum(OnDeviceType(d => d.PositioningAlgorithm)),
                Write = WriteEnum(OnDeviceType<ProvisioningPositioningAlgorithm>((d, v) => d.PositioningAlgorithm = v)),
                Clear = ClearOnDeviceType(d => d.PositioningAlgorithm = ProvisioningPositioningAlgorithm.Undefined)
            },
            new()
            {
                Key = FieldRuleCategory,
                LabelTextKey = "prov_rule_category",
                HelpTextKey = "prov_rule_category_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options = EnumOptions(
                    (ProvisioningRuleCategory.App, "Application"),
                    (ProvisioningRuleCategory.CommonService, "Common service")),
                DefaultValue = nameof(ProvisioningRuleCategory.App),
                MinLevel = ProvisioningScopeType.DeviceType,
                MaxLevel = ProvisioningScopeType.Gateway,
                Read = ReadEnum(OnDeviceType(d => d.RuleCategory)),
                Write = WriteEnum(OnDeviceType<ProvisioningRuleCategory>((d, v) => d.RuleCategory = v)),
                Clear = ClearOnDeviceType(d => d.RuleCategory = ProvisioningRuleCategory.Undefined)
            },
            new()
            {
                Key = FieldSecurityProfiles,
                LabelTextKey = "prov_security_profiles",
                HelpTextKey = "prov_security_profiles_help",
                Kind = ProvisioningFieldKind.StringList,
                DefaultValue = "",
                MinLevel = ProvisioningScopeType.DeviceType,
                MaxLevel = ProvisioningScopeType.Gateway,
                FortiManagerOnly = true,
                Read = ReadOnDeviceType(d => string.Join(kListSeparator, d.SecurityProfiles)),
                Write = OnDeviceType<string>((d, v) => d.SecurityProfiles = ParseStringList(v)),
                Clear = ClearOnDeviceType(d => d.SecurityProfiles = [])
            },
            new()
            {
                Key = FieldZoneFrom,
                LabelTextKey = "prov_zone_from",
                HelpTextKey = "prov_zone_from_help",
                Kind = ProvisioningFieldKind.Text,
                DefaultValue = "ANY",
                MinLevel = ProvisioningScopeType.DeviceType,
                MaxLevel = ProvisioningScopeType.Gateway,
                FortiManagerOnly = true,
                Read = ReadOnDeviceType(d => d.ZoneFrom),
                Write = OnDeviceType<string>((d, v) => d.ZoneFrom = v),
                Clear = ClearOnDeviceType(d => d.ZoneFrom = "")
            },
            new()
            {
                Key = FieldZoneTo,
                LabelTextKey = "prov_zone_to",
                HelpTextKey = "prov_zone_to_help",
                Kind = ProvisioningFieldKind.Text,
                DefaultValue = "ANY",
                MinLevel = ProvisioningScopeType.DeviceType,
                MaxLevel = ProvisioningScopeType.Gateway,
                FortiManagerOnly = true,
                Read = ReadOnDeviceType(d => d.ZoneTo),
                Write = OnDeviceType<string>((d, v) => d.ZoneTo = v),
                Clear = ClearOnDeviceType(d => d.ZoneTo = "")
            }
        ];

        public static ProvisioningFieldDefinition? FindField(string key) => Fields.FirstOrDefault(f => f.Key == key);

        /// <summary>Splits a comma separated list, keeping empty entries so half-filled editor rows survive a re-render.</summary>
        public static List<string> ParseStringList(string value) =>
            value.Length == 0 ? [] : [.. value.Split(kListSeparator, StringSplitOptions.TrimEntries)];

        /// <summary>Builds the option list of a select field from the real data-layer enum members.</summary>
        private static List<ProvisioningFieldOption> EnumOptions<TEnum>(params (TEnum Value, string DisplayName)[] entries) where TEnum : struct, Enum =>
            [.. entries.Select(e => new ProvisioningFieldOption(e.Value.ToString(), e.DisplayName))];

        /// <summary>Reads an enum property, mapping its Undefined member to "" (= not set at this scope).</summary>
        private static Func<GlobalProvisioningSettings, string> ReadEnum<TEnum>(Func<GlobalProvisioningSettings, TEnum> get) where TEnum : struct, Enum =>
            s => EqualityComparer<TEnum>.Default.Equals(get(s), default) ? "" : get(s).ToString();

        /// <summary>Writes an enum property from its member name; anything unparsable clears the property.</summary>
        private static Action<GlobalProvisioningSettings, string> WriteEnum<TEnum>(Action<GlobalProvisioningSettings, TEnum> set) where TEnum : struct, Enum =>
            (s, v) => set(s, Enum.TryParse(v, out TEnum parsed) ? parsed : default);

        /// <summary>Resets an enum property to its Undefined member, so the scope inherits it again.</summary>
        private static Action<GlobalProvisioningSettings> ClearEnum<TEnum>(Action<GlobalProvisioningSettings, TEnum> set) where TEnum : struct, Enum =>
            s => set(s, default);

        /// <summary>
        /// Lifts an enum getter declared on <see cref="DeviceTypeProvisioningSettings"/> to the base type
        /// the tree stores, yielding Undefined for scopes above device type, which do not carry the field.
        /// </summary>
        private static Func<GlobalProvisioningSettings, TEnum> OnDeviceType<TEnum>(Func<DeviceTypeProvisioningSettings, TEnum> get) where TEnum : struct, Enum =>
            s => s is DeviceTypeProvisioningSettings d ? get(d) : default;

        /// <summary>
        /// Lifts a text getter declared on <see cref="DeviceTypeProvisioningSettings"/>, yielding "" for
        /// scopes above device type, which do not carry the field and therefore never set it.
        /// </summary>
        private static Func<GlobalProvisioningSettings, string> ReadOnDeviceType(Func<DeviceTypeProvisioningSettings, string> get) =>
            s => s is DeviceTypeProvisioningSettings d ? get(d) : "";

        /// <summary>Lifts a setter declared on <see cref="DeviceTypeProvisioningSettings"/>, ignoring scopes above device type.</summary>
        private static Action<GlobalProvisioningSettings, T> OnDeviceType<T>(Action<DeviceTypeProvisioningSettings, T> set) =>
            (s, v) =>
            {
                if (s is DeviceTypeProvisioningSettings d)
                {
                    set(d, v);
                }
            };

        /// <summary>Lifts a reset declared on <see cref="DeviceTypeProvisioningSettings"/>, ignoring scopes above device type.</summary>
        private static Action<GlobalProvisioningSettings> ClearOnDeviceType(Action<DeviceTypeProvisioningSettings> clear) =>
            s =>
            {
                if (s is DeviceTypeProvisioningSettings d)
                {
                    clear(d);
                }
            };

        /// <summary>
        /// Creates the settings object of a scope below Global with every field cleared, so it inherits
        /// everything from its parent. The data-layer constructors seed concrete defaults, which below
        /// Global would read as "explicitly set here", so they are cleared through the field definitions.
        /// </summary>
        private static T NewInheriting<T>(string objectKey, string displayName, long nodeId, long parentNodeId) where T : GlobalProvisioningSettings, new()
        {
            T settings = new();
            foreach (ProvisioningFieldDefinition field in Fields)
            {
                field.Clear(settings);
            }
            settings.Scope.ObjectKey = objectKey;
            settings.Scope.DisplayName = displayName;
            settings.Scope.NodeId = nodeId;
            settings.Scope.ParentNodeId = parentNodeId;
            return settings;
        }

        /// <summary>
        /// Builds a small, illustrative Global / DeviceType / Management / Gateway tree with a handful
        /// of values already set below Global, so the inheritance behaviour is visible as soon as the
        /// page opens. Stands in for the not-yet-built backend query.
        /// </summary>
        public static List<ProvisioningNode> BuildSampleHierarchy()
        {
            List<ProvisioningNode> nodes = [];
            long nextNodeId = 1;

            GlobalProvisioningSettings globalSettings = new();
            globalSettings.Scope.ObjectKey = GlobalNodeKey;
            globalSettings.Scope.DisplayName = "Global";
            globalSettings.Scope.NodeId = nextNodeId++;
            ProvisioningNode global = new() { Settings = globalSettings };
            nodes.Add(global);

            AddDeviceTypeBranch(nodes, global, ref nextNodeId, "dt-checkpoint", "Check Point", ProvisioningDeviceTypeGroups.CheckPoint,
                managements:
                [
                    ("mgr-cp-mgr1", "cp-mgr1", ["cp-gw-1", "cp-gw-2"]),
                    ("mgr-cp-mgr2", "cp-mgr2", ["cp-gw-3"])
                ]);

            AddDeviceTypeBranch(nodes, global, ref nextNodeId, "dt-fortimanager", "FortiManager", ProvisioningDeviceTypeGroups.FortiManager,
                managements:
                [
                    ("mgr-forti-mgr1", "forti-mgr1", ["forti-gw-1"]),
                    ("mgr-forti-mgr2", "forti-mgr2", ["forti-gw-2", "forti-gw-3"])
                ]);

            AddDeviceTypeBranch(nodes, global, ref nextNodeId, "dt-tufinsc", "Tufin SecureChange", ProvisioningDeviceTypeGroups.TufinSC,
                managements:
                [
                    ("mgr-tufin-mgr1", "tufin-mgr1", ["tufin-gw-1"])
                ]);

            ApplySampleValues(nodes);
            return nodes;
        }

        /// <summary>A few illustrative values set below Global so the tree doesn't look flat on first load.</summary>
        private static void ApplySampleValues(List<ProvisioningNode> nodes)
        {
            DeviceTypeProvisioningSettings fortiDeviceType = (DeviceTypeProvisioningSettings)NodeById(nodes, "dt-fortimanager")!.Settings;
            fortiDeviceType.SecurityProfiles = ["default", "strict-web"];
            fortiDeviceType.ZoneFrom = "trust";
            fortiDeviceType.ZoneTo = "untrust";

            ((DeviceTypeProvisioningSettings)NodeById(nodes, "mgr-forti-mgr2")!.Settings).SecurityProfiles = ["default", "strict-web", "ips-high"];
            ((DeviceTypeProvisioningSettings)NodeById(nodes, "mgr-cp-mgr1")!.Settings).PositioningAlgorithm = ProvisioningPositioningAlgorithm.CheckPointInlineLayerPerZonePair;
            NodeById(nodes, "gw-cp-gw-1")!.Settings.Logging = ProvisioningLoggingMode.LogTrack;
            NodeById(nodes, "gw-forti-gw-2")!.Settings.ImplementationMode = ProvisioningImplementationMode.Manual;
        }

        private static void AddDeviceTypeBranch(
            List<ProvisioningNode> nodes,
            ProvisioningNode global,
            ref long nextNodeId,
            string deviceTypeId,
            string deviceTypeName,
            string deviceTypeGroup,
            (string Id, string Name, string[] Gateways)[] managements)
        {
            ProvisioningNode deviceType = new()
            {
                Settings = NewInheriting<DeviceTypeProvisioningSettings>(deviceTypeId, deviceTypeName, nextNodeId++, global.NodeId),
                DeviceTypeGroup = deviceTypeGroup
            };
            nodes.Add(deviceType);

            foreach ((string managementId, string managementName, string[] gateways) in managements)
            {
                ProvisioningNode management = new()
                {
                    Settings = NewInheriting<ManagementProvisioningSettings>(managementId, managementName, nextNodeId++, deviceType.NodeId),
                    DeviceTypeGroup = deviceTypeGroup
                };
                nodes.Add(management);

                foreach (string gatewayName in gateways)
                {
                    nodes.Add(new ProvisioningNode
                    {
                        Settings = NewInheriting<GatewayProvisioningSettings>($"gw-{gatewayName}", gatewayName, nextNodeId++, management.NodeId),
                        DeviceTypeGroup = deviceTypeGroup
                    });
                }
            }
        }

        public static ProvisioningNode? NodeById(List<ProvisioningNode> allNodes, string id) =>
            allNodes.FirstOrDefault(n => n.Id == id);

        /// <summary>Walks from <paramref name="node"/> up to Global, node itself first.</summary>
        public static List<ProvisioningNode> GetAncestryChain(List<ProvisioningNode> allNodes, ProvisioningNode node)
        {
            List<ProvisioningNode> chain = [node];
            ProvisioningNode? current = node;
            while (current?.ParentNodeId != null)
            {
                current = allNodes.FirstOrDefault(n => n.NodeId == current.ParentNodeId);
                if (current != null)
                {
                    chain.Add(current);
                }
            }
            return chain;
        }

        /// <summary>
        /// Resolves the effective value of a field for the given node by searching from the
        /// node itself up to Global and returning the first scope that sets it.
        /// </summary>
        public static ProvisioningResolvedValue Resolve(List<ProvisioningNode> allNodes, ProvisioningNode node, ProvisioningFieldDefinition field)
        {
            foreach (ProvisioningNode candidate in GetAncestryChain(allNodes, node))
            {
                if (field.IsSetOn(candidate.Settings))
                {
                    return new ProvisioningResolvedValue(field.Read(candidate.Settings), candidate, candidate.NodeId == node.NodeId);
                }
            }
            return new ProvisioningResolvedValue(field.DefaultValue, null, false);
        }

        public static List<ProvisioningNode> GetChildren(List<ProvisioningNode> allNodes, string? parentId)
        {
            ProvisioningNode? parent = parentId == null ? null : NodeById(allNodes, parentId);
            return parent == null ? [] : [.. allNodes.Where(n => n.ParentNodeId == parent.NodeId)];
        }
    }
}
