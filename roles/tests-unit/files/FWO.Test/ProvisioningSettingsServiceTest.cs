using FWO.Config.Api;
using FWO.Data.Provisioning;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers loading and saving the provisioning settings UI through <see cref="ProvisioningSettingsManager"/>.
/// </summary>
[TestFixture]
[Parallelizable]
internal class ProvisioningSettingsServiceTest
{
    private const long kGlobalNodeId = 1;
    private const long kCheckPointNodeId = 2;
    private const long kManagementNodeId = 3;
    private const long kGatewayNodeId = 4;

    [Test]
    public async Task LoadHierarchy_WithoutPersistedNodes_BuildsUnpersistedTree()
    {
        InMemoryProvisioningApiConnection api = new();

        ProvisioningNode root = await Service(api).LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.SelfAndDescendants().Count(), Is.EqualTo(8));
            Assert.That(root.SelfAndDescendants().All(n => !n.IsPersisted), Is.True);
            Assert.That(api.UpsertCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task LoadHierarchy_LinksAllPersistedLevels()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);

        ProvisioningNode root = await Service(api).LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Scope.NodeId, Is.EqualTo(kGlobalNodeId));
            Assert.That(Node(root, ProvisioningScopeType.DeviceType, "9").Scope.NodeId, Is.EqualTo(kCheckPointNodeId));
            Assert.That(Node(root, ProvisioningScopeType.Management, "100").Scope.NodeId, Is.EqualTo(kManagementNodeId));
            Assert.That(Node(root, ProvisioningScopeType.Gateway, "200").Scope.NodeId, Is.EqualTo(kGatewayNodeId));
            Assert.That(Node(root, ProvisioningScopeType.Gateway, "201").IsPersisted, Is.False);
            Assert.That(Node(root, ProvisioningScopeType.DeviceType, "11").IsPersisted, Is.False);
        }
    }

    [Test]
    public async Task LoadForm_NothingPersisted_ShowsCompiledDefaults()
    {
        InMemoryProvisioningApiConnection api = new();
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "210"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(State(form, ProvisioningSettingKeys.Logging).DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
            Assert.That(State(form, ProvisioningSettingKeys.ZoneFrom).DisplayedValue, Is.EqualTo("ANY"));
            Assert.That(form.Fields.All(f => !f.IsLocal && f.InheritedSource == null), Is.True);
            Assert.That(form.HasChanges, Is.False);
        }
    }

    [Test]
    public async Task LoadForm_BelowUnpersistedManagement_InheritsFromNearestPersistedAncestor()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: false);
        api.Nodes.RemoveAll(n => n.Id == kManagementNodeId);
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kCheckPointNodeId, ProvisioningSettingKeys.InstallOn, "device-type-target");
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        ProvisioningFieldState logging = State(form, ProvisioningSettingKeys.Logging);
        ProvisioningFieldState installOn = State(form, ProvisioningSettingKeys.InstallOn);
        ProvisioningFieldState positioning = State(form, ProvisioningSettingKeys.PositioningAlgorithm);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logging.DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(logging.InheritedSource?.NodeId, Is.EqualTo(kGlobalNodeId));
            Assert.That(installOn.DisplayedValue, Is.EqualTo("device-type-target"));
            Assert.That(installOn.InheritedSource?.NodeId, Is.EqualTo(kCheckPointNodeId));
            Assert.That(positioning.DisplayedValue, Is.EqualTo(nameof(ProvisioningPositioningAlgorithm.DefaultEndOfRulebase)));
            Assert.That(positioning.InheritedSource, Is.Null);
            Assert.That(form.Fields.Any(f => f.WasLocal), Is.False);
        }
    }

    [Test]
    public async Task LoadForm_LocalOverride_KnowsTheValueItWouldInherit()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: false);
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kManagementNodeId, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.None);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Management, "100"));

        ProvisioningFieldState logging = State(form, ProvisioningSettingKeys.Logging);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logging.WasLocal, Is.True);
            Assert.That(logging.IsLocal, Is.True);
            Assert.That(logging.DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.None)));
            Assert.That(logging.InheritedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(logging.InheritedSource?.NodeId, Is.EqualTo(kGlobalNodeId));
        }
    }

    [Test]
    public async Task LoadForm_GlobalOverride_InheritsTheCompiledDefault()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: false);
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.None);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        ProvisioningLevelForm form = await service.LoadFormAsync(root);

        ProvisioningFieldState logging = State(form, ProvisioningSettingKeys.Logging);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logging.WasLocal, Is.True);
            Assert.That(logging.InheritedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
            Assert.That(logging.InheritedSource, Is.Null);
        }
    }

    [Test]
    public async Task Save_FirstOverrideBelowNothingPersisted_CreatesTheWholeChain()
    {
        InMemoryProvisioningApiConnection api = new();
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningNode gateway = Node(root, ProvisioningScopeType.Gateway, "210");
        ProvisioningLevelForm form = await service.LoadFormAsync(gateway);
        ProvisioningFieldState zoneFrom = State(form, ProvisioningSettingKeys.ZoneFrom);

        zoneFrom.Override();
        zoneFrom.LocalValue = "dmz";
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gateway.SelfAndAncestors().All(n => n.IsPersisted), Is.True);
            Assert.That(api.Nodes.Select(n => (n.NodeType, n.ObjectKey)), Is.EquivalentTo(new[]
            {
                ("global", "global"), ("device_type", "11"), ("management", "101"), ("gateway", "210")
            }));
            Assert.That(api.Nodes.Single(n => n.Id == gateway.Scope.NodeId).DisplayName, Is.EqualTo("fgt-1"));
            Assert.That(api.Nodes.SelectMany(n => n.Values).Select(v => v.ConfigKey), Is.EqualTo(new[] { ProvisioningSettingKeys.ZoneFrom.DatabaseKey }));
        }

        ProvisioningLevelForm reloaded = await service.LoadFormAsync(gateway);
        Assert.That(State(reloaded, ProvisioningSettingKeys.ZoneFrom).WasLocal, Is.True);
    }

    [Test]
    public async Task Save_OverrideWithTheInheritedValue_IsStillStored()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        State(form, ProvisioningSettingKeys.Logging).Override();
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.Logging.DatabaseKey }));
            Assert.That(api.ReadValue(kGatewayNodeId, ProvisioningSettingKeys.Logging)?.ToString(), Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
        }
    }

    [Test]
    public async Task Save_WritesOnlyTheChangedFields()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);
        api.SetValue(kGatewayNodeId, ProvisioningSettingKeys.InstallOn, "keep-me");
        api.SetValue(kGatewayNodeId, ProvisioningSettingKeys.Templates, "remove-me");
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        ProvisioningFieldState ruleType = State(form, ProvisioningSettingKeys.RuleType);
        ruleType.Override();
        ruleType.LocalValue = nameof(ProvisioningRuleType.HandleAccessAndNat);
        State(form, ProvisioningSettingKeys.Templates).Inherit();
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.PatchCallCount, Is.EqualTo(1));
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.RuleType.DatabaseKey }));
            Assert.That(api.LastPatchRemoveKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.Templates.DatabaseKey }));
            Assert.That(api.ReadValue(kGatewayNodeId, ProvisioningSettingKeys.InstallOn)?.ToString(), Is.EqualTo("keep-me"));
            Assert.That(api.ReadValue(kGatewayNodeId, ProvisioningSettingKeys.Templates), Is.Null);
        }
    }

    [Test]
    public async Task Save_WithoutChanges_DoesNotCallTheApi()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));
        int callsBeforeSave = api.Calls.Count;

        State(form, ProvisioningSettingKeys.Logging).Override();
        State(form, ProvisioningSettingKeys.Logging).Inherit();
        await service.SaveAsync(form);

        Assert.That(api.Calls, Has.Count.EqualTo(callsBeforeSave));
    }

    [Test]
    public void FieldState_TracksIntentSeparatelyFromTheValue()
    {
        ProvisioningFieldDefinition field = ProvisioningSettingsData.FindField(ProvisioningSettingKeys.ZoneTo)!;
        ProvisioningFieldState local = ProvisioningFieldState.Create(field, true, "stored", "parent", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(local.IsChanged, Is.False);
            local.LocalValue = "edited";
            Assert.That(local.IsChanged, Is.True);
            local.Inherit();
            Assert.That(local.DisplayedValue, Is.EqualTo("parent"));
            local.Override();
            Assert.That(local.LocalValue, Is.EqualTo("stored"));
            Assert.That(local.IsChanged, Is.False);
            local.LocalValue = "edited";
            local.Reset();
            Assert.That(local.DisplayedValue, Is.EqualTo("stored"));
        }
    }

    private static ProvisioningSettingsService Service(InMemoryProvisioningApiConnection api) => new(new ProvisioningSettingsManager(api));

    private static InMemoryProvisioningApiConnection PersistedCheckPointChain(bool includeGateway)
    {
        InMemoryProvisioningApiConnection api = new();
        api.AddNode(kGlobalNodeId, ProvisioningScopeType.Global, "global", displayName: "Global");
        api.AddNode(kCheckPointNodeId, ProvisioningScopeType.DeviceType, "9", kGlobalNodeId, "Check Point R8x");
        api.AddNode(kManagementNodeId, ProvisioningScopeType.Management, "100", kCheckPointNodeId, "cp-mgr");
        if (includeGateway)
        {
            api.AddNode(kGatewayNodeId, ProvisioningScopeType.Gateway, "200", kManagementNodeId, "cp-gw-a");
        }
        return api;
    }

    private static ProvisioningNode Node(ProvisioningNode root, ProvisioningScopeType scopeType, string objectKey) =>
        ProvisioningSettingsData.FindNode(root, new ProvisioningSettingsScope { ScopeType = scopeType, ObjectKey = objectKey })
            ?? throw new AssertionException($"unknown node {scopeType}:{objectKey}");

    private static ProvisioningFieldState State(ProvisioningLevelForm form, ProvisioningSettingKey key) =>
        form.Fields.SingleOrDefault(f => f.Field.Key == key) ?? throw new AssertionException($"field {key} not on form");
}
