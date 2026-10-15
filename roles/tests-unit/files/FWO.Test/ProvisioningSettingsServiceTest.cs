using FWO.Config.Api;
using FWO.Data;
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
    private const long kFortiAdomNodeId = 5;
    private const int kManyManagementCount = 100;
    private const int kFirstManyManagementId = 1000;
    private const long kFirstManyManagementNodeId = 100;

    private static readonly (string NodeType, string ObjectKey)[] kExpectedCreatedChain =
        [("global", "global"), ("device_type", "11"), ("management", "101"), ("gateway", "210")];
    private static readonly string[] kExpectedZoneFromKeys = [ProvisioningSettingKeys.RuleZoneFromOverride.DatabaseKey];
    private static readonly string[] kExpectedLoggingKeys = [ProvisioningSettingKeys.RuleLoggingOverride.DatabaseKey];
    private static readonly string[] kExpectedPositioningKeys = [ProvisioningSettingKeys.PositioningAlgorithm.DatabaseKey];
    private static readonly string[] kExpectedInstallOnKeys = [ProvisioningSettingKeys.InstallOn.DatabaseKey];

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
            Assert.That(State(form, ProvisioningSettingKeys.RuleLoggingOverride).DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
            Assert.That(form.Fields.Any(f => f.Field.Key == ProvisioningSettingKeys.RuleZoneFromOverride), Is.False);
            Assert.That(form.Fields.All(f => !f.IsLocal && f.InheritedSource == null), Is.True);
            Assert.That(form.HasChanges, Is.False);
        }
    }

    [Test]
    public async Task LoadForm_BelowUnpersistedManagement_InheritsFromNearestPersistedAncestor()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: false);
        api.Nodes.RemoveAll(n => n.Id == kManagementNodeId);
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kCheckPointNodeId, ProvisioningSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        ProvisioningFieldState logging = State(form, ProvisioningSettingKeys.RuleLoggingOverride);
        ProvisioningFieldState installOn = State(form, ProvisioningSettingKeys.InstallOn);
        ProvisioningFieldState positioning = State(form, ProvisioningSettingKeys.PositioningAlgorithm);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logging.DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(logging.InheritedSource?.NodeId, Is.EqualTo(kGlobalNodeId));
            Assert.That(installOn.DisplayedValue, Is.EqualTo(nameof(ProvisioningInstallOn.GatewaysInPath)));
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
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kManagementNodeId, ProvisioningSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.None);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Management, "100"));

        ProvisioningFieldState logging = State(form, ProvisioningSettingKeys.RuleLoggingOverride);
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
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.None);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());

        ProvisioningLevelForm form = await service.LoadFormAsync(root);

        ProvisioningFieldState logging = State(form, ProvisioningSettingKeys.RuleLoggingOverride);
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
        ProvisioningFieldState installOn = State(form, ProvisioningSettingKeys.InstallOn);

        installOn.Override();
        installOn.LocalValue = nameof(ProvisioningInstallOn.GatewaysInPath);
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gateway.SelfAndAncestors().All(n => n.IsPersisted), Is.True);
            Assert.That(api.Nodes.Select(n => (n.NodeType, n.ObjectKey)), Is.EquivalentTo(kExpectedCreatedChain));
            Assert.That(api.Nodes.Single(n => n.Id == gateway.Scope.NodeId).DisplayName, Is.EqualTo("fgt-1"));
            Assert.That(api.Nodes.SelectMany(n => n.Values).Select(v => v.ConfigKey), Is.EqualTo(kExpectedInstallOnKeys));
        }

        ProvisioningLevelForm reloaded = await service.LoadFormAsync(gateway);
        Assert.That(State(reloaded, ProvisioningSettingKeys.InstallOn).WasLocal, Is.True);
    }

    [Test]
    public async Task Save_OverrideWithTheInheritedValue_IsStillStored()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        State(form, ProvisioningSettingKeys.RuleLoggingOverride).Override();
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(kExpectedLoggingKeys));
            Assert.That(api.ReadValue(kGatewayNodeId, ProvisioningSettingKeys.RuleLoggingOverride)?.ToString(), Is.EqualTo("log"));
        }
    }

    [Test]
    public async Task Save_WritesOnlyTheChangedFields()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);
        api.SetValue(kGatewayNodeId, ProvisioningSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        api.SetValue(kGatewayNodeId, ProvisioningSettingKeys.RuleZoneFromOverride, "remove-me");
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        ProvisioningFieldState positioning = State(form, ProvisioningSettingKeys.PositioningAlgorithm);
        positioning.Override();
        positioning.LocalValue = nameof(ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection);
        State(form, ProvisioningSettingKeys.RuleZoneFromOverride).Inherit();
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.PatchCallCount, Is.EqualTo(1));
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(kExpectedPositioningKeys));
            Assert.That(api.LastPatchRemoveKeys, Is.EqualTo(kExpectedZoneFromKeys));
            Assert.That(api.ReadValue(kGatewayNodeId, ProvisioningSettingKeys.InstallOn)?.ToString(), Is.EqualTo("gatewaysInPath"));
            Assert.That(api.ReadValue(kGatewayNodeId, ProvisioningSettingKeys.RuleZoneFromOverride), Is.Null);
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

        State(form, ProvisioningSettingKeys.RuleLoggingOverride).Override();
        State(form, ProvisioningSettingKeys.RuleLoggingOverride).Inherit();
        await service.SaveAsync(form);

        Assert.That(api.Calls, Has.Count.EqualTo(callsBeforeSave));
    }

    [Test]
    public void FieldState_TracksIntentSeparatelyFromTheValue()
    {
        ProvisioningFieldDefinition field = ProvisioningSettingsData.FindField(ProvisioningSettingKeys.RuleZoneToOverride)!;
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

    [Test]
    public async Task LoadForm_MovedManagement_InheritsFromItsCurrentDeviceType()
    {
        InMemoryProvisioningApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: true);
        api.SetValue(kCheckPointNodeId, ProvisioningSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kFortiAdomNodeId, ProvisioningSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.None);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningNode management = Node(root, ProvisioningScopeType.Management, "100");

        ProvisioningLevelForm managementForm = await service.LoadFormAsync(management);
        ProvisioningLevelForm gatewayForm = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ProvisioningSettingsService.IsMisplaced(management), Is.True);
            Assert.That(State(managementForm, ProvisioningSettingKeys.RuleLoggingOverride).DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(State(managementForm, ProvisioningSettingKeys.RuleLoggingOverride).InheritedSource?.NodeId, Is.EqualTo(kCheckPointNodeId));
            Assert.That(State(gatewayForm, ProvisioningSettingKeys.RuleLoggingOverride).DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(api.UpsertCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task Save_MovedManagement_IsMovedBelowItsCurrentDeviceType()
    {
        InMemoryProvisioningApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: false);
        api.SetValue(kManagementNodeId, ProvisioningSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningNode management = Node(root, ProvisioningScopeType.Management, "100");
        ProvisioningLevelForm form = await service.LoadFormAsync(management);
        ProvisioningFieldState ruleType = State(form, ProvisioningSettingKeys.RuleType);
        ruleType.Override();
        ruleType.LocalValue = nameof(ProvisioningRuleType.HandleAccessAndNat);

        await service.SaveAsync(form);

        long checkPointNodeId = api.Nodes.Single(n => n.NodeType == "device_type" && n.ObjectKey == "9").Id;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.Nodes.Single(n => n.Id == kManagementNodeId).ParentId, Is.EqualTo(checkPointNodeId));
            Assert.That(ProvisioningSettingsService.IsMisplaced(management), Is.False);
            Assert.That(api.ReadValue(kManagementNodeId, ProvisioningSettingKeys.InstallOn)?.ToString(), Is.EqualTo("gatewaysInPath"));
            Assert.That(api.ReadValue(kManagementNodeId, ProvisioningSettingKeys.RuleType)?.ToString(), Is.EqualTo("accessAndNat"));
        }
    }

    [Test]
    public async Task Save_OnlyRemovalOnMovedManagement_AlsoMovesIt()
    {
        InMemoryProvisioningApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: true);
        api.SetValue(kManagementNodeId, ProvisioningSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Management, "100"));
        State(form, ProvisioningSettingKeys.InstallOn).Inherit();

        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.Nodes.Single(n => n.Id == kManagementNodeId).ParentId, Is.EqualTo(kCheckPointNodeId));
            Assert.That(api.ReadValue(kManagementNodeId, ProvisioningSettingKeys.InstallOn), Is.Null);
        }
    }

    [Test]
    public async Task LoadForm_MovedAwayFromFortinet_ExposesAndClearsDormantOverride()
    {
        InMemoryProvisioningApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: true);
        api.SetValue(kManagementNodeId, ProvisioningSettingKeys.RuleZoneFromOverride, "internal");
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningNode management = Node(root, ProvisioningScopeType.Management, "100");

        ProvisioningLevelForm form = await service.LoadFormAsync(management);
        ProvisioningFieldState zoneFrom = State(form, ProvisioningSettingKeys.RuleZoneFromOverride);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zoneFrom.WasLocal, Is.True);
            Assert.That(zoneFrom.IsApplicable, Is.False);
            Assert.That(zoneFrom.IsDormant, Is.True);
            Assert.That(zoneFrom.OriginalValue, Is.EqualTo("internal"));
        }

        zoneFrom.Inherit();
        await service.SaveAsync(form);
        ProvisioningLevelForm reloaded = await service.LoadFormAsync(management);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.ReadValue(kManagementNodeId, ProvisioningSettingKeys.RuleZoneFromOverride), Is.Null);
            Assert.That(reloaded.Fields.Any(field => field.Field.Key == ProvisioningSettingKeys.RuleZoneFromOverride), Is.False);
        }
    }

    /// <summary>
    /// The API accepts object creation overrides at gateway level although the page offers them only down to
    /// manager; such an override is shown as dormant on the gateway and can be cleared there.
    /// </summary>
    [Test]
    public async Task LoadForm_OverrideBelowUiLevel_ExposesAndClearsDormantOverride()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);
        api.SetValue(kGatewayNodeId, ProvisioningSettingKeys.ServiceObjectCreation, ProvisioningObjectCreationMode.Submanager);
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningNode gateway = Node(root, ProvisioningScopeType.Gateway, "200");

        ProvisioningLevelForm form = await service.LoadFormAsync(gateway);
        ProvisioningFieldState serviceObjects = State(form, ProvisioningSettingKeys.ServiceObjectCreation);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(serviceObjects.IsDormant, Is.True);
            Assert.That(serviceObjects.OriginalValue, Is.EqualTo(nameof(ProvisioningObjectCreationMode.Submanager)));
            Assert.That(form.Fields.Any(f => f.Field.Key == ProvisioningSettingKeys.AddressObjectCreation), Is.False);
        }

        serviceObjects.Inherit();
        await service.SaveAsync(form);
        ProvisioningLevelForm reloaded = await service.LoadFormAsync(gateway);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.ReadValue(kGatewayNodeId, ProvisioningSettingKeys.ServiceObjectCreation), Is.Null);
            Assert.That(reloaded.Fields.Any(f => f.Field.Key == ProvisioningSettingKeys.ServiceObjectCreation), Is.False);
        }
    }

    [Test]
    public async Task Save_CapturesChangedValuesBeforeFirstHierarchyRequest()
    {
        InMemoryProvisioningApiConnection api = new();
        ProvisioningSettingsService service = Service(api);
        ProvisioningNode root = await service.LoadHierarchyAsync(ProvisioningSettingsDataTest.SampleManagements());
        ProvisioningNode management = Node(root, ProvisioningScopeType.Management, "101");
        ProvisioningLevelForm form = await service.LoadFormAsync(management);
        ProvisioningFieldState zoneFrom = State(form, ProvisioningSettingKeys.RuleZoneFromOverride);
        zoneFrom.Override();
        zoneFrom.LocalValue = "captured-before-save";

        TaskCompletionSource<bool> firstQueryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> continueQuery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int queryCount = 0;
        api.BeforeQueryAsync = async _ =>
        {
            if (Interlocked.Increment(ref queryCount) == 1)
            {
                firstQueryStarted.TrySetResult(true);
                await continueQuery.Task;
            }
        };

        Task saveTask = service.SaveAsync(form);
        try
        {
            await firstQueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            zoneFrom.LocalValue = "late-edit";
        }
        finally
        {
            continueQuery.TrySetResult(true);
        }
        await saveTask;

        Assert.That(api.ReadValue(management.Scope.NodeId, ProvisioningSettingKeys.RuleZoneFromOverride)?.ToString(),
            Is.EqualTo("captured-before-save"));
    }

    /// <summary>
    /// The number of API calls does not grow with the number of stored nodes: the tree is loaded with one query,
    /// and a form with a direct override needs one query for its level and one for the level above.
    /// </summary>
    [Test]
    public async Task LoadHierarchyAndForm_WithManyPersistedManagements_UseAConstantNumberOfQueries()
    {
        InMemoryProvisioningApiConnection api = PersistedCheckPointChain(includeGateway: true);
        List<Management> managements = ProvisioningSettingsDataTest.SampleManagements();
        DeviceType checkPoint = managements[0].DeviceType;
        for (int index = 0; index < kManyManagementCount; index++)
        {
            int managementId = kFirstManyManagementId + index;
            managements.Add(new Management { Id = managementId, Name = $"mgr-{managementId}", DeviceType = checkPoint });
            api.AddNode(kFirstManyManagementNodeId + index, ProvisioningScopeType.Management,
                managementId.ToString(System.Globalization.CultureInfo.InvariantCulture), kCheckPointNodeId);
        }
        api.SetValue(kGatewayNodeId, ProvisioningSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        ProvisioningSettingsService service = Service(api);

        ProvisioningNode root = await service.LoadHierarchyAsync(managements);
        int treeCalls = api.Calls.Count;
        ProvisioningLevelForm form = await service.LoadFormAsync(Node(root, ProvisioningScopeType.Gateway, "200"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Node(root, ProvisioningScopeType.Management, "1099").IsPersisted, Is.True);
            Assert.That(treeCalls, Is.EqualTo(1));
            Assert.That(api.Calls, Has.Count.EqualTo(treeCalls + 2));
            Assert.That(State(form, ProvisioningSettingKeys.InstallOn).WasLocal, Is.True);
        }
    }

    /// <summary>
    /// Management 100 is a Check Point management in the tree, but its node is still stored below the node of
    /// device type 11, as if its device type had been changed after the node was stored.
    /// </summary>
    private static InMemoryProvisioningApiConnection MovedCheckPointManagement(bool currentDeviceTypePersisted)
    {
        InMemoryProvisioningApiConnection api = new();
        api.AddNode(kGlobalNodeId, ProvisioningScopeType.Global, "global", displayName: "Global");
        if (currentDeviceTypePersisted)
        {
            api.AddNode(kCheckPointNodeId, ProvisioningScopeType.DeviceType, "9", kGlobalNodeId, "Check Point R8x");
        }
        api.AddNode(kFortiAdomNodeId, ProvisioningScopeType.DeviceType, "11", kGlobalNodeId, "FortiADOM 5ff");
        api.AddNode(kManagementNodeId, ProvisioningScopeType.Management, "100", kFortiAdomNodeId, "cp-mgr");
        return api;
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
