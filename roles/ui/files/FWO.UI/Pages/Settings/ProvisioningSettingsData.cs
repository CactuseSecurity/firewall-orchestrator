using System.Collections.Generic;
using System.Linq;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// The four levels of the provisioning-settings hierarchy, from most general (Global)
    /// to most specific (Gateway). A setting resolves by walking from the selected node
    /// upward until a level defines a value.
    /// </summary>
    public enum ProvisioningLevel
    {
        Global = 0,
        DeviceType = 1,
        Manager = 2,
        Gateway = 3
    }

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
        TemplateText
    }

    /// <summary>
    /// One selectable option for a SingleSelect/MultiSelectTags field.
    /// </summary>
    public sealed class ProvisioningFieldOption(string value, string displayName)
    {
        public string Value { get; } = value;
        public string DisplayName { get; } = displayName;
    }

    /// <summary>
    /// Definition (schema) of a single provisioning-settings key: what kind of value it holds,
    /// which hierarchy levels it may be set at, and - for the FortiManager-only fields -
    /// which device-type group it is restricted to.
    /// </summary>
    public sealed class ProvisioningFieldDefinition
    {
        public required string Key { get; init; }
        public required string LabelTextKey { get; init; }
        public required string HelpTextKey { get; init; }
        public ProvisioningFieldKind Kind { get; init; } = ProvisioningFieldKind.SingleSelect;
        public List<ProvisioningFieldOption> Options { get; init; } = [];
        public string DefaultValue { get; init; } = "";

        /// <summary>Lowest (most general) level at which this field may be configured.</summary>
        public ProvisioningLevel MinLevel { get; init; } = ProvisioningLevel.Global;

        /// <summary>Highest (most specific) level at which this field may be configured.</summary>
        public ProvisioningLevel MaxLevel { get; init; } = ProvisioningLevel.Gateway;

        /// <summary>Only relevant/shown below (and including) DeviceType for FortiManager device types.</summary>
        public bool FortiManagerOnly { get; init; } = false;

        public bool AppliesToLevel(ProvisioningLevel level) => level >= MinLevel && level <= MaxLevel;
    }

    /// <summary>
    /// One node of the provisioning-settings hierarchy tree (Global / a device type /
    /// a manager / a gateway), plus whatever field overrides are explicitly set at this node.
    /// An empty/missing entry in <see cref="Overrides"/> means "inherit from the parent node".
    /// </summary>
    public sealed class ProvisioningNode
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required ProvisioningLevel Level { get; init; }
        public string? ParentId { get; init; }

        /// <summary>Device-type group this node belongs to (set on DeviceType nodes and inherited down); empty for Global.</summary>
        public string DeviceTypeGroup { get; init; } = "";

        public Dictionary<string, string> Overrides { get; init; } = [];
    }

    /// <summary>
    /// Where an effective field value came from, for display ("inherited from ..." vs "set here").
    /// </summary>
    public sealed record ProvisioningResolvedValue(string Value, ProvisioningNode SourceNode, bool IsOverriddenHere);

    /// <summary>
    /// Field schema plus an in-memory mock hierarchy, standing in for the not-yet-built
    /// getAllProvisioningSettings backend. Only used to drive the settings/fwconfigprovisioning UI.
    /// </summary>
    public static class ProvisioningSettingsMockData
    {
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

        /// <summary>
        /// Field definitions. Two source keys (path_analysis_algorithm, service/address_object_creation)
        /// were listed under both "all levels" and a more specific level range in the spec;
        /// the more specific range (global-only / all-but-gateway) is treated as authoritative here.
        /// </summary>
        public static readonly IReadOnlyList<ProvisioningFieldDefinition> Fields =
        [
            new()
            {
                Key = FieldImplementationMode,
                LabelTextKey = "prov_implementation_mode",
                HelpTextKey = "prov_implementation_mode_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("FWO_auto", "FWO automatic"),
                    new("manual", "Manual"),
                    new("tufin_SC", "Tufin SecureChange"),
                    new("none", "None (no implementation task)")
                ],
                DefaultValue = "FWO_auto",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Gateway
            },
            new()
            {
                Key = FieldInstallOn,
                LabelTextKey = "prov_install_on",
                HelpTextKey = "prov_install_on_help",
                Kind = ProvisioningFieldKind.Text,
                DefaultValue = "ANY",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Gateway
            },
            new()
            {
                Key = FieldPathAnalysisAlgorithm,
                LabelTextKey = "prov_path_analysis_algorithm",
                HelpTextKey = "prov_path_analysis_algorithm_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("static_lists_per_subnet", "Static lists per subnet"),
                    new("manual_planning", "Manual planning"),
                    new("ask_external_api", "Ask external API")
                ],
                DefaultValue = "static_lists_per_subnet",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Global
            },
            new()
            {
                Key = FieldLogging,
                LabelTextKey = "prov_logging",
                HelpTextKey = "prov_logging_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("log", "Log"),
                    new("log_track", "Log & track"),
                    new("none", "None")
                ],
                DefaultValue = "log",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Gateway
            },
            new()
            {
                Key = FieldServiceObjectCreation,
                LabelTextKey = "prov_service_object_creation",
                HelpTextKey = "prov_service_object_creation_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("supermanager", "Supermanager"),
                    new("submanager", "Submanager")
                ],
                DefaultValue = "submanager",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Manager
            },
            new()
            {
                Key = FieldAddressObjectCreation,
                LabelTextKey = "prov_address_object_creation",
                HelpTextKey = "prov_address_object_creation_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("supermanager", "Supermanager"),
                    new("submanager", "Submanager")
                ],
                DefaultValue = "submanager",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Manager
            },
            new()
            {
                Key = FieldRuleType,
                LabelTextKey = "prov_rule_type",
                HelpTextKey = "prov_rule_type_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("always_access", "Always access (standard)"),
                    new("handle_access_and_nat", "Handle access and NAT"),
                    new("handle_access_and_IPS", "Handle access and IPS"),
                    new("handle_access_nat_IPS", "Handle access, NAT and IPS")
                ],
                DefaultValue = "always_access",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Gateway
            },
            new()
            {
                Key = FieldTemplates,
                LabelTextKey = "prov_templates",
                HelpTextKey = "prov_templates_help",
                Kind = ProvisioningFieldKind.TemplateText,
                DefaultValue = "",
                MinLevel = ProvisioningLevel.Global,
                MaxLevel = ProvisioningLevel.Gateway
            },
            new()
            {
                Key = FieldPositioningAlgorithm,
                LabelTextKey = "prov_positioning_algorithm",
                HelpTextKey = "prov_positioning_algorithm_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("cp_inline_layer_per_zone_pair", "Check Point: inline layer per zone pair"),
                    new("fortinet_end_of_zone", "Fortinet: end of zone"),
                    new("cp_end_of_app_section", "Check Point: end of app section"),
                    new("cp_end_of_app_section_distinguish_common_services", "Check Point: end of app section, distinguish common services"),
                    new("default_end_of_rulebase", "Default: end of rulebase")
                ],
                DefaultValue = "default_end_of_rulebase",
                MinLevel = ProvisioningLevel.DeviceType,
                MaxLevel = ProvisioningLevel.Gateway
            },
            new()
            {
                Key = FieldRuleCategory,
                LabelTextKey = "prov_rule_category",
                HelpTextKey = "prov_rule_category_help",
                Kind = ProvisioningFieldKind.SingleSelect,
                Options =
                [
                    new("app", "Application"),
                    new("common_service", "Common service")
                ],
                DefaultValue = "app",
                MinLevel = ProvisioningLevel.DeviceType,
                MaxLevel = ProvisioningLevel.Gateway
            },
            new()
            {
                Key = FieldSecurityProfiles,
                LabelTextKey = "prov_security_profiles",
                HelpTextKey = "prov_security_profiles_help",
                Kind = ProvisioningFieldKind.MultiSelectTags,
                DefaultValue = "",
                MinLevel = ProvisioningLevel.DeviceType,
                MaxLevel = ProvisioningLevel.Gateway,
                FortiManagerOnly = true
            },
            new()
            {
                Key = FieldZoneFrom,
                LabelTextKey = "prov_zone_from",
                HelpTextKey = "prov_zone_from_help",
                Kind = ProvisioningFieldKind.Text,
                DefaultValue = "ANY",
                MinLevel = ProvisioningLevel.DeviceType,
                MaxLevel = ProvisioningLevel.Gateway,
                FortiManagerOnly = true
            },
            new()
            {
                Key = FieldZoneTo,
                LabelTextKey = "prov_zone_to",
                HelpTextKey = "prov_zone_to_help",
                Kind = ProvisioningFieldKind.Text,
                DefaultValue = "ANY",
                MinLevel = ProvisioningLevel.DeviceType,
                MaxLevel = ProvisioningLevel.Gateway,
                FortiManagerOnly = true
            }
        ];

        public static ProvisioningFieldDefinition? FindField(string key) => Fields.FirstOrDefault(f => f.Key == key);

        /// <summary>
        /// Builds a small, illustrative Global &gt; DeviceType &gt; Manager &gt; Gateway tree
        /// with a handful of overrides already set, so the inheritance behaviour is visible
        /// as soon as the page opens.
        /// </summary>
        public static List<ProvisioningNode> BuildMockHierarchy()
        {
            List<ProvisioningNode> nodes = [];

            ProvisioningNode global = new()
            {
                Id = "global",
                Name = "Global",
                Level = ProvisioningLevel.Global,
                ParentId = null,
                Overrides = Fields
                    .Where(f => f.AppliesToLevel(ProvisioningLevel.Global))
                    .ToDictionary(f => f.Key, f => f.DefaultValue)
            };
            nodes.Add(global);

            AddDeviceTypeBranch(nodes, global, "dt-checkpoint", "Check Point", ProvisioningDeviceTypeGroups.CheckPoint,
                managers:
                [
                    ("mgr-cp-mgr1", "cp-mgr1", new[] { "cp-gw-1", "cp-gw-2" }),
                    ("mgr-cp-mgr2", "cp-mgr2", new[] { "cp-gw-3" })
                ]);

            AddDeviceTypeBranch(nodes, global, "dt-fortimanager", "FortiManager", ProvisioningDeviceTypeGroups.FortiManager,
                managers:
                [
                    ("mgr-forti-mgr1", "forti-mgr1", new[] { "forti-gw-1" }),
                    ("mgr-forti-mgr2", "forti-mgr2", new[] { "forti-gw-2", "forti-gw-3" })
                ]);

            AddDeviceTypeBranch(nodes, global, "dt-tufinsc", "Tufin SecureChange", ProvisioningDeviceTypeGroups.TufinSC,
                managers:
                [
                    ("mgr-tufin-mgr1", "tufin-mgr1", new[] { "tufin-gw-1" })
                ]);

            // A few illustrative overrides so the tree doesn't look flat on first load.
            NodeById(nodes, "dt-fortimanager")!.Overrides[FieldSecurityProfiles] = "default,strict-web";
            NodeById(nodes, "dt-fortimanager")!.Overrides[FieldZoneFrom] = "trust";
            NodeById(nodes, "dt-fortimanager")!.Overrides[FieldZoneTo] = "untrust";
            NodeById(nodes, "mgr-forti-mgr2")!.Overrides[FieldSecurityProfiles] = "default,strict-web,ips-high";
            NodeById(nodes, "mgr-cp-mgr1")!.Overrides[FieldPositioningAlgorithm] = "cp_inline_layer_per_zone_pair";
            NodeById(nodes, "gw-cp-gw-1")!.Overrides[FieldLogging] = "log_track";
            NodeById(nodes, "gw-forti-gw-2")!.Overrides[FieldImplementationMode] = "manual";

            return nodes;
        }

        private static void AddDeviceTypeBranch(
            List<ProvisioningNode> nodes,
            ProvisioningNode global,
            string deviceTypeId,
            string deviceTypeName,
            string deviceTypeGroup,
            (string id, string name, string[] gateways)[] managers)
        {
            ProvisioningNode deviceType = new()
            {
                Id = deviceTypeId,
                Name = deviceTypeName,
                Level = ProvisioningLevel.DeviceType,
                ParentId = global.Id,
                DeviceTypeGroup = deviceTypeGroup
            };
            nodes.Add(deviceType);

            foreach ((string managerId, string managerName, string[] gateways) in managers)
            {
                ProvisioningNode manager = new()
                {
                    Id = managerId,
                    Name = managerName,
                    Level = ProvisioningLevel.Manager,
                    ParentId = deviceType.Id,
                    DeviceTypeGroup = deviceTypeGroup
                };
                nodes.Add(manager);

                foreach (string gatewayName in gateways)
                {
                    nodes.Add(new ProvisioningNode
                    {
                        Id = $"gw-{gatewayName}",
                        Name = gatewayName,
                        Level = ProvisioningLevel.Gateway,
                        ParentId = manager.Id,
                        DeviceTypeGroup = deviceTypeGroup
                    });
                }
            }
        }

        private static ProvisioningNode? NodeById(List<ProvisioningNode> nodes, string id) =>
            nodes.FirstOrDefault(n => n.Id == id);

        /// <summary>Walks from <paramref name="node"/> up to Global, node itself first.</summary>
        public static List<ProvisioningNode> GetAncestryChain(List<ProvisioningNode> allNodes, ProvisioningNode node)
        {
            List<ProvisioningNode> chain = [node];
            ProvisioningNode? current = node;
            while (current?.ParentId != null)
            {
                current = allNodes.FirstOrDefault(n => n.Id == current.ParentId);
                if (current != null)
                {
                    chain.Add(current);
                }
            }
            return chain;
        }

        /// <summary>
        /// Resolves the effective value of a field for the given node by searching from the
        /// node itself up to Global and returning the first level that has it set.
        /// </summary>
        public static ProvisioningResolvedValue Resolve(List<ProvisioningNode> allNodes, ProvisioningNode node, ProvisioningFieldDefinition field)
        {
            List<ProvisioningNode> chain = GetAncestryChain(allNodes, node);
            foreach (ProvisioningNode candidate in chain)
            {
                if (candidate.Overrides.TryGetValue(field.Key, out string? value))
                {
                    return new ProvisioningResolvedValue(value, candidate, candidate.Id == node.Id);
                }
            }
            return new ProvisioningResolvedValue(field.DefaultValue, node, false);
        }

        public static List<ProvisioningNode> GetChildren(List<ProvisioningNode> allNodes, string? parentId) =>
            allNodes.Where(n => n.ParentId == parentId).ToList();
    }
}
