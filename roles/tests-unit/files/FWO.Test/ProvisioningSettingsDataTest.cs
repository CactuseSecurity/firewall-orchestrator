using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the field schema of the provisioning settings UI and building its tree from the real managements.
/// </summary>
[TestFixture]
[Parallelizable]
internal class ProvisioningSettingsDataTest
{
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

    private static ProvisioningFieldDefinition Field(ProvisioningSettingKey key) =>
        ProvisioningSettingsData.FindField(key) ?? throw new AssertionException($"unknown field {key}");

    private static ProvisioningNode Node(ProvisioningNode root, ProvisioningScopeType scopeType, string objectKey) =>
        ProvisioningSettingsData.FindNode(root, new ProvisioningSettingsScope { ScopeType = scopeType, ObjectKey = objectKey })
            ?? throw new AssertionException($"unknown node {scopeType}:{objectKey}");

    [Test]
    public void BuildHierarchy_MirrorsVisibleManagementsGroupedByDeviceType()
    {
        ProvisioningNode root = ProvisioningSettingsData.BuildHierarchy(SampleManagements(), []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Level, Is.EqualTo(ProvisioningScopeType.Global));
            Assert.That(root.Scope.ObjectKey, Is.EqualTo(ProvisioningSettingsData.GlobalObjectKey));
            Assert.That(root.Children.Select(n => n.Name), Is.EqualTo(new[] { "Check Point R8x", "FortiADOM 5ff" }));
            Assert.That(root.Children.Select(n => n.Scope.ObjectKey), Is.EqualTo(new[] { "9", "11" }));
            Assert.That(root.Children[0].Children.Select(n => n.Name), Is.EqualTo(new[] { "cp-mgr" }));
            Assert.That(root.Children[0].Children[0].Children.Select(n => n.Scope.ObjectKey), Is.EqualTo(new[] { "200", "201" }));
            Assert.That(root.SelfAndDescendants().Any(n => n.Name is "hidden-mgr" or "router-mgr"), Is.False);
            Assert.That(root.SelfAndDescendants().All(n => !n.IsPersisted), Is.True);
        }
    }

    [Test]
    public void BuildHierarchy_MarksEverythingBelowAFortinetDeviceType()
    {
        ProvisioningNode root = ProvisioningSettingsData.BuildHierarchy(SampleManagements(), []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Node(root, ProvisioningScopeType.Gateway, "210").IsFortinet, Is.True);
            Assert.That(Node(root, ProvisioningScopeType.Management, "101").IsFortinet, Is.True);
            Assert.That(Node(root, ProvisioningScopeType.Gateway, "200").IsFortinet, Is.False);
            Assert.That(root.IsFortinet, Is.False);
        }
    }

    [Test]
    public void BuildHierarchy_LinksPersistedScopesByTypeAndObjectKey()
    {
        ProvisioningSettingsScope persistedManagement = new()
        {
            ScopeType = ProvisioningScopeType.Management,
            ObjectKey = "100",
            DisplayName = "old name",
            NodeId = 3,
            ParentNodeId = 2
        };
        ProvisioningSettingsScope sameKeyOtherType = new()
        {
            ScopeType = ProvisioningScopeType.Gateway,
            ObjectKey = "101",
            NodeId = 9,
            ParentNodeId = 3
        };

        ProvisioningNode root = ProvisioningSettingsData.BuildHierarchy(SampleManagements(), [persistedManagement, sameKeyOtherType]);
        ProvisioningNode management = Node(root, ProvisioningScopeType.Management, "100");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(management.Scope.NodeId, Is.EqualTo(3));
            Assert.That(management.Name, Is.EqualTo("cp-mgr"));
            Assert.That(Node(root, ProvisioningScopeType.Management, "101").IsPersisted, Is.False);
        }
    }

    [Test]
    public void SelfAndAncestors_RunsFromTheNodeUpToGlobal()
    {
        ProvisioningNode root = ProvisioningSettingsData.BuildHierarchy(SampleManagements(), []);

        IEnumerable<string> chain = Node(root, ProvisioningScopeType.Gateway, "210").SelfAndAncestors().Select(n => n.Id);

        Assert.That(chain, Is.EqualTo(new[] { "Gateway:210", "Management:101", "DeviceType:11", "Global:global" }));
    }

    [Test]
    public void FieldsFor_HonoursLevelRangeAndFortinetRestriction()
    {
        ProvisioningNode root = ProvisioningSettingsData.BuildHierarchy(SampleManagements(), []);

        List<ProvisioningSettingKey> global = [.. ProvisioningSettingsData.FieldsFor(root).Select(f => f.Key)];
        List<ProvisioningSettingKey> checkPointGateway = [.. ProvisioningSettingsData.FieldsFor(Node(root, ProvisioningScopeType.Gateway, "200")).Select(f => f.Key)];
        List<ProvisioningSettingKey> fortiGateway = [.. ProvisioningSettingsData.FieldsFor(Node(root, ProvisioningScopeType.Gateway, "210")).Select(f => f.Key)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(global, Does.Contain(ProvisioningSettingKeys.PathAnalysisAlgorithm));
            Assert.That(global, Does.Not.Contain(ProvisioningSettingKeys.PositioningAlgorithm));
            Assert.That(checkPointGateway, Does.Not.Contain(ProvisioningSettingKeys.PathAnalysisAlgorithm));
            Assert.That(checkPointGateway, Does.Not.Contain(ProvisioningSettingKeys.ServiceObjectCreation));
            Assert.That(checkPointGateway, Does.Contain(ProvisioningSettingKeys.PositioningAlgorithm));
            Assert.That(checkPointGateway, Does.Not.Contain(ProvisioningSettingKeys.ZoneFrom));
            Assert.That(fortiGateway, Does.Contain(ProvisioningSettingKeys.ZoneFrom));
            Assert.That(fortiGateway, Does.Contain(ProvisioningSettingKeys.SecurityProfiles));
        }
    }

    [Test]
    public void Fields_AreOnlyOfferedWhereTheirKeyMayBeStored()
    {
        foreach (ProvisioningFieldDefinition field in ProvisioningSettingsData.Fields)
        {
            foreach (ProvisioningScopeType level in Enum.GetValues<ProvisioningScopeType>().Where(field.AppliesToLevel))
            {
                Assert.That(field.Key.IsAllowedAt(level), Is.True, $"{field.Id}@{level}");
            }
        }
    }

    [Test]
    public void Read_FormatsTheCompiledDefaults()
    {
        GlobalProvisioningSettings defaults = ProvisioningSettingsData.CreateDefaults(ProvisioningScopeType.Gateway);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Field(ProvisioningSettingKeys.Logging).Read(defaults), Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
            Assert.That(Field(ProvisioningSettingKeys.ZoneFrom).Read(defaults), Is.EqualTo("ANY"));
            Assert.That(Field(ProvisioningSettingKeys.SecurityProfiles).Read(defaults), Is.Empty);
        }
    }

    [Test]
    public void Read_OfADeviceLevelFieldOnGlobalSettingsFails()
    {
        Assert.Throws<InvalidOperationException>(() => Field(ProvisioningSettingKeys.ZoneTo).Read(new GlobalProvisioningSettings()));
    }

    [Test]
    public void SetOverride_ConvertsTheEditorValueToTheTypedSetting()
    {
        ProvisioningSettingsChangeSet changes = new(new ProvisioningSettingsScope { ScopeType = ProvisioningScopeType.Gateway, ObjectKey = "200" });

        Field(ProvisioningSettingKeys.Logging).SetOverride(changes, nameof(ProvisioningLoggingMode.LogTrack));
        Field(ProvisioningSettingKeys.ZoneFrom).SetOverride(changes, "dmz");
        Field(ProvisioningSettingKeys.SecurityProfiles).SetOverride(changes, "a,,b,");

        Dictionary<string, object> values = changes.Upserts.ToDictionary(u => u.Key.DatabaseKey, u => u.Value);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(values[ProvisioningSettingKeys.Logging.DatabaseKey], Is.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(values[ProvisioningSettingKeys.ZoneFrom.DatabaseKey], Is.EqualTo("dmz"));
            Assert.That(values[ProvisioningSettingKeys.SecurityProfiles.DatabaseKey], Is.EqualTo(new List<string> { "a", "b" }));
        }
    }

    [Test]
    public void SelectOptions_UseTheDataLayerEnumMemberNames()
    {
        ProvisioningSettingsScope scope = new() { ScopeType = ProvisioningScopeType.Gateway, ObjectKey = "200" };
        foreach (ProvisioningFieldDefinition field in ProvisioningSettingsData.Fields.Where(f => f.Kind == ProvisioningFieldKind.SingleSelect))
        {
            Assert.That(field.Options, Is.Not.Empty, field.Id);
            foreach (ProvisioningFieldOption option in field.Options)
            {
                ProvisioningSettingsScope fieldScope = field.Key.IsAllowedAt(scope.ScopeType)
                    ? scope
                    : new() { ScopeType = ProvisioningScopeType.Global, ObjectKey = ProvisioningSettingsData.GlobalObjectKey };
                ProvisioningSettingsChangeSet changes = new(fieldScope);
                field.SetOverride(changes, option.Value);
                Assert.That(changes.Upserts.Single().Value.ToString(), Is.EqualTo(option.Value), $"{field.Id}/{option.Value}");
            }
        }
    }

    [Test]
    public void ParseStringList_KeepsEmptyEntriesButNotForAnEmptyValue()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ProvisioningSettingsData.ParseStringList(""), Is.Empty);
            Assert.That(ProvisioningSettingsData.ParseStringList("a, b"), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(ProvisioningSettingsData.ParseStringList("a,"), Is.EqualTo(new[] { "a", "" }));
        }
    }

    [Test]
    public void CreateDefaults_ReturnsTheDtoOfTheScopeType()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ProvisioningSettingsData.CreateDefaults(ProvisioningScopeType.Global), Is.TypeOf<GlobalProvisioningSettings>());
            Assert.That(ProvisioningSettingsData.CreateDefaults(ProvisioningScopeType.DeviceType), Is.TypeOf<DeviceTypeProvisioningSettings>());
            Assert.That(ProvisioningSettingsData.CreateDefaults(ProvisioningScopeType.Management), Is.TypeOf<ManagementProvisioningSettings>());
            Assert.That(ProvisioningSettingsData.CreateDefaults(ProvisioningScopeType.Gateway), Is.TypeOf<GatewayProvisioningSettings>());
            Assert.Throws<ArgumentOutOfRangeException>(() => ProvisioningSettingsData.CreateDefaults(ProvisioningScopeType.Undefined));
        }
    }
}
