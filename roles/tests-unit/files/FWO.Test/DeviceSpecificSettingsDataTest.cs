using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the field schema of the device specific settings UI and building its tree from the real managements.
/// </summary>
[TestFixture]
[Parallelizable]
internal class DeviceSpecificSettingsDataTest
{
    private static readonly List<string> kBoolOptionValues = [bool.TrueString, bool.FalseString];
    private static readonly string[] ExpectedDeviceTypeNames = ["Check Point R8x", "FortiADOM 5ff"];
    private static readonly string[] ExpectedDeviceTypeKeys = ["9", "11"];
    private static readonly string[] ExpectedManagementNames = ["cp-mgr"];
    private static readonly string[] ExpectedGatewayKeys = ["200", "201"];
    private static readonly string[] ExpectedScopeChain = ["Gateway:210", "Management:101", "DeviceType:11", "Global:global"];
    private static readonly string[] ExpectedParsedList = ["a", "b"];
    private static readonly List<DeviceSpecificSettingsScope> kNoPersistedScopes = [];
    private static readonly string[] ExpectedParsedListWithEmpty = ["a", ""];

    internal static List<Management> SampleManagements() =>
    [
        new()
        {
            Id = 100,
            Name = "cp-mgr",
            DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x", Manufacturer = "Check Point" },
            Devices = [new Device { Id = 201, Name = "cp-gw-b" }, new Device { Id = 200, Name = "cp-gw-a" }]
        },
        new()
        {
            Id = 101,
            Name = "forti-adom",
            DeviceType = new DeviceType { Id = 11, Name = "FortiADOM", Version = "5ff", Manufacturer = "Fortinet" },
            Devices = [new Device { Id = 210, Name = "fgt-1" }]
        },
        new()
        {
            Id = 102,
            Name = "hidden-mgr",
            HideInUi = true,
            DeviceType = new DeviceType { Id = 9, Name = "Check Point", Version = "R8x", Manufacturer = "Check Point" },
            Devices = [new Device { Id = 220, Name = "hidden-gw" }]
        },
        new()
        {
            Id = 103,
            Name = "router-mgr",
            DeviceType = new DeviceType { Id = 17, Name = "DummyRouter Management", Version = "1", IsPureRoutingDevice = true },
            Devices = [new Device { Id = 230, Name = "router" }]
        }
    ];

    private static DeviceSpecificFieldDefinition Field(DeviceSpecificSettingKey key) =>
        DeviceSpecificSettingsData.FindField(key) ?? throw new AssertionException($"unknown field {key}");

    private static DeviceSpecificNode Node(DeviceSpecificNode root, DeviceSpecificScopeType scopeType, string objectKey) =>
        DeviceSpecificSettingsData.FindNode(root, new DeviceSpecificSettingsScope { ScopeType = scopeType, ObjectKey = objectKey })
            ?? throw new AssertionException($"unknown node {scopeType}:{objectKey}");

    [Test]
    public void BuildHierarchy_MirrorsVisibleManagementsGroupedByDeviceType()
    {
        DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(SampleManagements(), []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Level, Is.EqualTo(DeviceSpecificScopeType.Global));
            Assert.That(root.Scope.ObjectKey, Is.EqualTo(DeviceSpecificSettingsData.GlobalObjectKey));
            Assert.That(root.Children.Select(n => n.Name), Is.EqualTo(ExpectedDeviceTypeNames));
            Assert.That(root.Children.Select(n => n.Scope.ObjectKey), Is.EqualTo(ExpectedDeviceTypeKeys));
            Assert.That(root.Children[0].Children.Select(n => n.Name), Is.EqualTo(ExpectedManagementNames));
            Assert.That(root.Children[0].Children[0].Children.Select(n => n.Scope.ObjectKey), Is.EqualTo(ExpectedGatewayKeys));
            Assert.That(root.SelfAndDescendants().Any(n => n.Name is "hidden-mgr" or "router-mgr"), Is.False);
            Assert.That(root.SelfAndDescendants().All(n => !n.IsPersisted), Is.True);
        }
    }

    [Test]
    public void BuildHierarchy_MarksEverythingBelowAFortinetDeviceType()
    {
        DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(SampleManagements(), []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Node(root, DeviceSpecificScopeType.Gateway, "210").IsFortinet, Is.True);
            Assert.That(Node(root, DeviceSpecificScopeType.Management, "101").IsFortinet, Is.True);
            Assert.That(Node(root, DeviceSpecificScopeType.Gateway, "200").IsFortinet, Is.False);
            Assert.That(root.IsFortinet, Is.False);
        }
    }

    [Test]
    public void BuildHierarchy_LinksPersistedScopesByTypeAndObjectKey()
    {
        DeviceSpecificSettingsScope persistedManagement = new()
        {
            ScopeType = DeviceSpecificScopeType.Management,
            ObjectKey = "100",
            DisplayName = "old name",
            NodeId = 3,
            ParentNodeId = 2
        };
        DeviceSpecificSettingsScope sameKeyOtherType = new()
        {
            ScopeType = DeviceSpecificScopeType.Gateway,
            ObjectKey = "101",
            NodeId = 9,
            ParentNodeId = 3
        };

        DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(SampleManagements(), [persistedManagement, sameKeyOtherType]);
        DeviceSpecificNode management = Node(root, DeviceSpecificScopeType.Management, "100");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(management.Scope.NodeId, Is.EqualTo(3));
            Assert.That(management.Name, Is.EqualTo("cp-mgr"));
            Assert.That(Node(root, DeviceSpecificScopeType.Management, "101").IsPersisted, Is.False);
        }
    }

    [Test]
    public void BuildHierarchy_GivesEveryNodeThePathOfItsPlaceInTheTree()
    {
        DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(SampleManagements(), kNoPersistedScopes);

        foreach (DeviceSpecificNode node in root.SelfAndDescendants())
        {
            List<DeviceSpecificSettingsScope> path = node.Path.ToScopes();
            List<DeviceSpecificNode> treePath = [.. node.SelfAndAncestors().Reverse()];
            Assert.That(path.Select(scope => (scope.ScopeType, scope.ObjectKey)),
                Is.EqualTo(treePath.Select(treeNode => (treeNode.Level, treeNode.Scope.ObjectKey))), node.Id);
        }
    }

    [Test]
    public void SelfAndAncestors_RunsFromTheNodeUpToGlobal()
    {
        DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(SampleManagements(), []);

        IEnumerable<string> chain = Node(root, DeviceSpecificScopeType.Gateway, "210").SelfAndAncestors().Select(n => n.Id);

        Assert.That(chain, Is.EqualTo(ExpectedScopeChain));
    }

    [Test]
    public void FieldsFor_HonoursLevelRangeAndFortinetRestriction()
    {
        DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(SampleManagements(), []);

        List<DeviceSpecificSettingKey> global = [.. DeviceSpecificSettingsData.FieldsFor(root).Select(f => f.Key)];
        List<DeviceSpecificSettingKey> checkPointGateway = [.. DeviceSpecificSettingsData.FieldsFor(Node(root, DeviceSpecificScopeType.Gateway, "200")).Select(f => f.Key)];
        List<DeviceSpecificSettingKey> fortiGateway = [.. DeviceSpecificSettingsData.FieldsFor(Node(root, DeviceSpecificScopeType.Gateway, "210")).Select(f => f.Key)];
        List<DeviceSpecificSettingKey> fortiManagement = [.. DeviceSpecificSettingsData.FieldsFor(Node(root, DeviceSpecificScopeType.Management, "101")).Select(f => f.Key)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(global, Does.Contain(DeviceSpecificSettingKeys.ServiceObjectCreation));
            Assert.That(global, Does.Contain(DeviceSpecificSettingKeys.PositioningAlgorithm));
            Assert.That(checkPointGateway, Does.Not.Contain(DeviceSpecificSettingKeys.ServiceObjectCreation));
            Assert.That(checkPointGateway, Does.Contain(DeviceSpecificSettingKeys.PositioningAlgorithm));
            Assert.That(checkPointGateway, Does.Not.Contain(DeviceSpecificSettingKeys.RuleZoneFromOverride));
            Assert.That(fortiManagement, Does.Contain(DeviceSpecificSettingKeys.RuleZoneFromOverride));
            Assert.That(fortiManagement, Does.Contain(DeviceSpecificSettingKeys.SecurityProfiles));
            Assert.That(fortiGateway, Does.Not.Contain(DeviceSpecificSettingKeys.RuleZoneFromOverride));
            Assert.That(fortiGateway, Does.Not.Contain(DeviceSpecificSettingKeys.SecurityProfiles));
            Assert.That(fortiGateway, Does.Contain(DeviceSpecificSettingKeys.AutoPlanImplementation));
        }
    }

    [Test]
    public void Fields_AreOnlyOfferedWhereTheirKeyMayBeStored()
    {
        foreach (DeviceSpecificFieldDefinition field in DeviceSpecificSettingsData.Fields)
        {
            foreach (DeviceSpecificScopeType level in Enum.GetValues<DeviceSpecificScopeType>().Where(field.AppliesToLevel))
            {
                Assert.That(field.Key.IsAllowedAt(level), Is.True, $"{field.Id}@{level}");
            }
        }
    }

    [Test]
    public void Read_FormatsTheCompiledDefaults()
    {
        GlobalDeviceSpecificSettings defaults = DeviceSpecificSettingsData.CreateDefaults(DeviceSpecificScopeType.Gateway);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Field(DeviceSpecificSettingKeys.RuleLoggingOverride).Read(defaults), Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
            Assert.That(Field(DeviceSpecificSettingKeys.RuleZoneFromOverride).Read(defaults), Is.EqualTo("ANY"));
            Assert.That(Field(DeviceSpecificSettingKeys.SecurityProfiles).Read(defaults), Is.Empty);
        }
    }

    [Test]
    public void Read_OfAZoneFieldOnGlobalSettingsUsesTheDefault()
    {
        Assert.That(Field(DeviceSpecificSettingKeys.RuleZoneToOverride).Read(new GlobalDeviceSpecificSettings()), Is.EqualTo("ANY"));
    }

    [Test]
    public void SetOverride_ConvertsTheEditorValueToTheTypedSetting()
    {
        DeviceSpecificSettingsChangeSet changes = new(new DeviceSpecificSettingsScope { ScopeType = DeviceSpecificScopeType.Gateway, ObjectKey = "200" });

        Field(DeviceSpecificSettingKeys.RuleLoggingOverride).SetOverride(changes, nameof(ProvisioningLoggingMode.LogTrack));
        Field(DeviceSpecificSettingKeys.RuleZoneFromOverride).SetOverride(changes, "dmz");
        Field(DeviceSpecificSettingKeys.SecurityProfiles).SetOverride(changes, "a,,b,");

        Dictionary<string, object> values = changes.Upserts.ToDictionary(u => u.Key.DatabaseKey, u => u.Value);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values[DeviceSpecificSettingKeys.RuleLoggingOverride.DatabaseKey], Is.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(values[DeviceSpecificSettingKeys.RuleZoneFromOverride.DatabaseKey], Is.EqualTo("dmz"));
            Assert.That(values[DeviceSpecificSettingKeys.SecurityProfiles.DatabaseKey], Is.EqualTo(new List<string> { "a", "b" }));
        }
    }

    [Test]
    public void SelectOptions_UseTheDataLayerEnumMemberNames()
    {
        DeviceSpecificSettingsScope scope = new() { ScopeType = DeviceSpecificScopeType.Gateway, ObjectKey = "200" };
        foreach (DeviceSpecificFieldDefinition field in DeviceSpecificSettingsData.Fields.Where(f => f.Kind == DeviceSpecificFieldKind.SingleSelect))
        {
            Assert.That(field.Options, Is.Not.Empty, field.Id);
            foreach (DeviceSpecificFieldOption option in field.Options)
            {
                DeviceSpecificSettingsScope fieldScope = field.Key.IsAllowedAt(scope.ScopeType)
                    ? scope
                    : new() { ScopeType = DeviceSpecificScopeType.Global, ObjectKey = DeviceSpecificSettingsData.GlobalObjectKey };
                DeviceSpecificSettingsChangeSet changes = new(fieldScope);
                field.SetOverride(changes, option.Value);
                Assert.That(changes.Upserts.Single().Value.ToString(), Is.EqualTo(option.Value), $"{field.Id}/{option.Value}");
            }
        }
    }

    [Test]
    public void PositioningAlgorithm_OffersCustomerOptionAtEveryScope()
    {
        DeviceSpecificFieldDefinition field = DeviceSpecificSettingsData.Fields.Single(item =>
            item.Key == DeviceSpecificSettingKeys.PositioningAlgorithm);

        Assert.Multiple(() =>
        {
            Assert.That(field.MinLevel, Is.EqualTo(DeviceSpecificScopeType.Global));
            Assert.That(field.MaxLevel, Is.EqualTo(DeviceSpecificScopeType.Gateway));
            Assert.That(field.Options.Single(option => option.Value ==
                    nameof(ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection)).TextKey,
                Is.EqualTo("dss_opt_cp_layer_guard_end_of_zone_pair_section"));
        });
    }

    [Test]
    public void ParseStringList_KeepsEmptyEntriesButNotForAnEmptyValue()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DeviceSpecificSettingsData.ParseStringList(""), Is.Empty);
            Assert.That(DeviceSpecificSettingsData.ParseStringList("a, b"), Is.EqualTo(ExpectedParsedList));
            Assert.That(DeviceSpecificSettingsData.ParseStringList("a,"), Is.EqualTo(ExpectedParsedListWithEmpty));
        }
    }

    [Test]
    public void CreateDefaults_ReturnsTheDtoOfTheScopeType()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DeviceSpecificSettingsData.CreateDefaults(DeviceSpecificScopeType.Global), Is.TypeOf<GlobalDeviceSpecificSettings>());
            Assert.That(DeviceSpecificSettingsData.CreateDefaults(DeviceSpecificScopeType.DeviceType), Is.TypeOf<DeviceTypeDeviceSpecificSettings>());
            Assert.That(DeviceSpecificSettingsData.CreateDefaults(DeviceSpecificScopeType.Management), Is.TypeOf<ManagementDeviceSpecificSettings>());
            Assert.That(DeviceSpecificSettingsData.CreateDefaults(DeviceSpecificScopeType.Gateway), Is.TypeOf<GatewayDeviceSpecificSettings>());
            Assert.Throws<ArgumentOutOfRangeException>(() => DeviceSpecificSettingsData.CreateDefaults(DeviceSpecificScopeType.Undefined));
        }
    }

    /// <summary>
    /// A select field offers every value the serializer can store and nothing else, so any stored override has a
    /// matching option and every option can be saved. The Undefined placeholder is neither stored nor offered.
    /// </summary>
    [Test]
    public void SelectFields_OfferExactlyTheStorableEnumValues()
    {
        List<DeviceSpecificFieldDefinition> selectFields =
            [.. DeviceSpecificSettingsData.Fields.Where(f => f.Kind == DeviceSpecificFieldKind.SingleSelect)];

        Assert.That(selectFields, Is.Not.Empty);
        using (Assert.EnterMultipleScope())
        {
            foreach (DeviceSpecificFieldDefinition field in selectFields)
            {
                List<string> storable = field.Key.ValueType == typeof(bool) ? [.. kBoolOptionValues]
                    : [.. Enum.GetNames(field.Key.ValueType).Where(name => name != DeviceSpecificSettingValueSerializer.kUndefinedEnumName)];
                if (field.Key == DeviceSpecificSettingKeys.ServiceObjectCreation || field.Key == DeviceSpecificSettingKeys.AddressObjectCreation)
                {
                    storable.Remove(nameof(ProvisioningObjectCreationMode.Ignore));
                }
                Assert.That(field.Options.Select(o => o.Value), Is.EquivalentTo(storable), field.Id);
            }
        }
    }
}
