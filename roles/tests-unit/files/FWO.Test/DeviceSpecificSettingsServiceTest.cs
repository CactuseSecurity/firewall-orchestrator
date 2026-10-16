using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers loading and saving the device specific settings UI through <see cref="DeviceSpecificSettingsManager"/>.
/// </summary>
[TestFixture]
[Parallelizable]
internal class DeviceSpecificSettingsServiceTest
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
    private static readonly string[] kExpectedZoneFromKeys = [DeviceSpecificSettingKeys.RuleZoneFromOverride.DatabaseKey];
    private static readonly string[] kExpectedLoggingKeys = [DeviceSpecificSettingKeys.RuleLoggingOverride.DatabaseKey];
    private static readonly string[] kExpectedPositioningKeys = [DeviceSpecificSettingKeys.PositioningAlgorithm.DatabaseKey];
    private static readonly string[] kExpectedInstallOnKeys = [DeviceSpecificSettingKeys.InstallOn.DatabaseKey];

    [Test]
    public async Task LoadHierarchy_WithoutPersistedNodes_BuildsUnpersistedTree()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();

        DeviceSpecificNode root = await Service(api).LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());

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
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: true);

        DeviceSpecificNode root = await Service(api).LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Scope.NodeId, Is.EqualTo(kGlobalNodeId));
            Assert.That(Node(root, DeviceSpecificScopeType.DeviceType, "9").Scope.NodeId, Is.EqualTo(kCheckPointNodeId));
            Assert.That(Node(root, DeviceSpecificScopeType.Management, "100").Scope.NodeId, Is.EqualTo(kManagementNodeId));
            Assert.That(Node(root, DeviceSpecificScopeType.Gateway, "200").Scope.NodeId, Is.EqualTo(kGatewayNodeId));
            Assert.That(Node(root, DeviceSpecificScopeType.Gateway, "201").IsPersisted, Is.False);
            Assert.That(Node(root, DeviceSpecificScopeType.DeviceType, "11").IsPersisted, Is.False);
        }
    }

    [Test]
    public async Task LoadForm_NothingPersisted_ShowsCompiledDefaults()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());

        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Gateway, "210"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(State(form, DeviceSpecificSettingKeys.RuleLoggingOverride).DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.Log)));
            Assert.That(form.Fields.Any(f => f.Field.Key == DeviceSpecificSettingKeys.RuleZoneFromOverride), Is.False);
            Assert.That(form.Fields.All(f => !f.IsLocal && f.InheritedSource == null), Is.True);
            Assert.That(form.HasChanges, Is.False);
        }
    }

    [Test]
    public async Task LoadForm_BelowUnpersistedManagement_InheritsFromNearestPersistedAncestor()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: false);
        api.Nodes.RemoveAll(n => n.Id == kManagementNodeId);
        api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kCheckPointNodeId, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());

        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Gateway, "200"));

        DeviceSpecificFieldState logging = State(form, DeviceSpecificSettingKeys.RuleLoggingOverride);
        DeviceSpecificFieldState installOn = State(form, DeviceSpecificSettingKeys.InstallOn);
        DeviceSpecificFieldState positioning = State(form, DeviceSpecificSettingKeys.PositioningAlgorithm);
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
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: false);
        api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.None);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());

        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Management, "100"));

        DeviceSpecificFieldState logging = State(form, DeviceSpecificSettingKeys.RuleLoggingOverride);
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
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: false);
        api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.None);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());

        DeviceSpecificLevelForm form = await service.LoadFormAsync(root);

        DeviceSpecificFieldState logging = State(form, DeviceSpecificSettingKeys.RuleLoggingOverride);
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
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificNode gateway = Node(root, DeviceSpecificScopeType.Gateway, "210");
        DeviceSpecificLevelForm form = await service.LoadFormAsync(gateway);
        DeviceSpecificFieldState installOn = State(form, DeviceSpecificSettingKeys.InstallOn);

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

        DeviceSpecificLevelForm reloaded = await service.LoadFormAsync(gateway);
        Assert.That(State(reloaded, DeviceSpecificSettingKeys.InstallOn).WasLocal, Is.True);
    }

    [Test]
    public async Task Save_OverrideWithTheInheritedValue_IsStillStored()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: true);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Gateway, "200"));

        State(form, DeviceSpecificSettingKeys.RuleLoggingOverride).Override();
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(kExpectedLoggingKeys));
            Assert.That(api.ReadValue(kGatewayNodeId, DeviceSpecificSettingKeys.RuleLoggingOverride)?.ToString(), Is.EqualTo("log"));
        }
    }

    [Test]
    public async Task Save_WritesOnlyTheChangedFields()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: true);
        api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.RuleZoneFromOverride, "remove-me");
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Gateway, "200"));

        DeviceSpecificFieldState positioning = State(form, DeviceSpecificSettingKeys.PositioningAlgorithm);
        positioning.Override();
        positioning.LocalValue = nameof(ProvisioningPositioningAlgorithm.CheckPointEndOfAppSection);
        State(form, DeviceSpecificSettingKeys.RuleZoneFromOverride).Inherit();
        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.PatchCallCount, Is.EqualTo(1));
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(kExpectedPositioningKeys));
            Assert.That(api.LastPatchRemoveKeys, Is.EqualTo(kExpectedZoneFromKeys));
            Assert.That(api.ReadValue(kGatewayNodeId, DeviceSpecificSettingKeys.InstallOn)?.ToString(), Is.EqualTo("gatewaysInPath"));
            Assert.That(api.ReadValue(kGatewayNodeId, DeviceSpecificSettingKeys.RuleZoneFromOverride), Is.Null);
        }
    }

    [Test]
    public async Task Save_WithoutChanges_DoesNotCallTheApi()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: true);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Gateway, "200"));
        int callsBeforeSave = api.Calls.Count;

        State(form, DeviceSpecificSettingKeys.RuleLoggingOverride).Override();
        State(form, DeviceSpecificSettingKeys.RuleLoggingOverride).Inherit();
        await service.SaveAsync(form);

        Assert.That(api.Calls, Has.Count.EqualTo(callsBeforeSave));
    }

    [Test]
    public void FieldState_TracksIntentSeparatelyFromTheValue()
    {
        DeviceSpecificFieldDefinition field = DeviceSpecificSettingsData.FindField(DeviceSpecificSettingKeys.RuleZoneToOverride)!;
        DeviceSpecificFieldState local = DeviceSpecificFieldState.Create(field, true, "stored", "parent", null);

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
        InMemoryDeviceSpecificSettingsApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: true);
        api.SetValue(kCheckPointNodeId, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(kFortiAdomNodeId, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.None);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificNode management = Node(root, DeviceSpecificScopeType.Management, "100");

        DeviceSpecificLevelForm managementForm = await service.LoadFormAsync(management);
        DeviceSpecificLevelForm gatewayForm = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Gateway, "200"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DeviceSpecificSettingsService.IsMisplaced(management), Is.True);
            Assert.That(State(managementForm, DeviceSpecificSettingKeys.RuleLoggingOverride).DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(State(managementForm, DeviceSpecificSettingKeys.RuleLoggingOverride).InheritedSource?.NodeId, Is.EqualTo(kCheckPointNodeId));
            Assert.That(State(gatewayForm, DeviceSpecificSettingKeys.RuleLoggingOverride).DisplayedValue, Is.EqualTo(nameof(ProvisioningLoggingMode.LogTrack)));
            Assert.That(api.UpsertCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task Save_MovedManagement_IsMovedBelowItsCurrentDeviceType()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: false);
        api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificNode management = Node(root, DeviceSpecificScopeType.Management, "100");
        DeviceSpecificLevelForm form = await service.LoadFormAsync(management);
        DeviceSpecificFieldState ruleType = State(form, DeviceSpecificSettingKeys.RuleType);
        ruleType.Override();
        ruleType.LocalValue = nameof(ProvisioningRuleType.HandleAccessAndNat);

        await service.SaveAsync(form);

        long checkPointNodeId = api.Nodes.Single(n => n.NodeType == "device_type" && n.ObjectKey == "9").Id;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.Nodes.Single(n => n.Id == kManagementNodeId).ParentId, Is.EqualTo(checkPointNodeId));
            Assert.That(DeviceSpecificSettingsService.IsMisplaced(management), Is.False);
            Assert.That(api.ReadValue(kManagementNodeId, DeviceSpecificSettingKeys.InstallOn)?.ToString(), Is.EqualTo("gatewaysInPath"));
            Assert.That(api.ReadValue(kManagementNodeId, DeviceSpecificSettingKeys.RuleType)?.ToString(), Is.EqualTo("accessAndNat"));
        }
    }

    [Test]
    public async Task Save_OnlyRemovalOnMovedManagement_AlsoMovesIt()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: true);
        api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Management, "100"));
        State(form, DeviceSpecificSettingKeys.InstallOn).Inherit();

        await service.SaveAsync(form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.Nodes.Single(n => n.Id == kManagementNodeId).ParentId, Is.EqualTo(kCheckPointNodeId));
            Assert.That(api.ReadValue(kManagementNodeId, DeviceSpecificSettingKeys.InstallOn), Is.Null);
        }
    }

    [Test]
    public async Task LoadForm_MovedAwayFromFortinet_ExposesAndClearsDormantOverride()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = MovedCheckPointManagement(currentDeviceTypePersisted: true);
        api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.RuleZoneFromOverride, "internal");
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificNode management = Node(root, DeviceSpecificScopeType.Management, "100");

        DeviceSpecificLevelForm form = await service.LoadFormAsync(management);
        DeviceSpecificFieldState zoneFrom = State(form, DeviceSpecificSettingKeys.RuleZoneFromOverride);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zoneFrom.WasLocal, Is.True);
            Assert.That(zoneFrom.IsApplicable, Is.False);
            Assert.That(zoneFrom.IsDormant, Is.True);
            Assert.That(zoneFrom.OriginalValue, Is.EqualTo("internal"));
        }

        zoneFrom.Inherit();
        await service.SaveAsync(form);
        DeviceSpecificLevelForm reloaded = await service.LoadFormAsync(management);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.ReadValue(kManagementNodeId, DeviceSpecificSettingKeys.RuleZoneFromOverride), Is.Null);
            Assert.That(reloaded.Fields.Any(field => field.Field.Key == DeviceSpecificSettingKeys.RuleZoneFromOverride), Is.False);
        }
    }

    /// <summary>
    /// The API accepts object creation overrides at gateway level although the page offers them only down to
    /// manager; such an override is shown as dormant on the gateway and can be cleared there.
    /// </summary>
    [Test]
    public async Task LoadForm_OverrideBelowUiLevel_ExposesAndClearsDormantOverride()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: true);
        api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.ServiceObjectCreation, ProvisioningObjectCreationMode.Submanager);
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificNode gateway = Node(root, DeviceSpecificScopeType.Gateway, "200");

        DeviceSpecificLevelForm form = await service.LoadFormAsync(gateway);
        DeviceSpecificFieldState serviceObjects = State(form, DeviceSpecificSettingKeys.ServiceObjectCreation);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(serviceObjects.IsDormant, Is.True);
            Assert.That(serviceObjects.OriginalValue, Is.EqualTo(nameof(ProvisioningObjectCreationMode.Submanager)));
            Assert.That(form.Fields.Any(f => f.Field.Key == DeviceSpecificSettingKeys.AddressObjectCreation), Is.False);
        }

        serviceObjects.Inherit();
        await service.SaveAsync(form);
        DeviceSpecificLevelForm reloaded = await service.LoadFormAsync(gateway);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.ReadValue(kGatewayNodeId, DeviceSpecificSettingKeys.ServiceObjectCreation), Is.Null);
            Assert.That(reloaded.Fields.Any(f => f.Field.Key == DeviceSpecificSettingKeys.ServiceObjectCreation), Is.False);
        }
    }

    [Test]
    public async Task Save_CapturesChangedValuesBeforeFirstHierarchyRequest()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        DeviceSpecificSettingsService service = Service(api);
        DeviceSpecificNode root = await service.LoadHierarchyAsync(DeviceSpecificSettingsDataTest.SampleManagements());
        DeviceSpecificNode management = Node(root, DeviceSpecificScopeType.Management, "101");
        DeviceSpecificLevelForm form = await service.LoadFormAsync(management);
        DeviceSpecificFieldState zoneFrom = State(form, DeviceSpecificSettingKeys.RuleZoneFromOverride);
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

        Assert.That(api.ReadValue(management.Scope.NodeId, DeviceSpecificSettingKeys.RuleZoneFromOverride)?.ToString(),
            Is.EqualTo("captured-before-save"));
    }

    /// <summary>
    /// The number of API calls does not grow with the number of stored nodes: the tree is loaded with one query,
    /// and a form with a direct override needs one query for its level and one for the level above.
    /// </summary>
    [Test]
    public async Task LoadHierarchyAndForm_WithManyPersistedManagements_UseAConstantNumberOfQueries()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = PersistedCheckPointChain(includeGateway: true);
        List<Management> managements = DeviceSpecificSettingsDataTest.SampleManagements();
        DeviceType checkPoint = managements[0].DeviceType;
        for (int index = 0; index < kManyManagementCount; index++)
        {
            int managementId = kFirstManyManagementId + index;
            managements.Add(new Management { Id = managementId, Name = $"mgr-{managementId}", DeviceType = checkPoint });
            api.AddNode(kFirstManyManagementNodeId + index, DeviceSpecificScopeType.Management,
                managementId.ToString(System.Globalization.CultureInfo.InvariantCulture), kCheckPointNodeId);
        }
        api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsService service = Service(api);

        DeviceSpecificNode root = await service.LoadHierarchyAsync(managements);
        int treeCalls = api.Calls.Count;
        DeviceSpecificLevelForm form = await service.LoadFormAsync(Node(root, DeviceSpecificScopeType.Gateway, "200"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Node(root, DeviceSpecificScopeType.Management, "1099").IsPersisted, Is.True);
            Assert.That(treeCalls, Is.EqualTo(1));
            Assert.That(api.Calls, Has.Count.EqualTo(treeCalls + 2));
            Assert.That(State(form, DeviceSpecificSettingKeys.InstallOn).WasLocal, Is.True);
        }
    }

    /// <summary>
    /// Management 100 is a Check Point management in the tree, but its node is still stored below the node of
    /// device type 11, as if its device type had been changed after the node was stored.
    /// </summary>
    private static InMemoryDeviceSpecificSettingsApiConnection MovedCheckPointManagement(bool currentDeviceTypePersisted)
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, "global", displayName: "Global");
        if (currentDeviceTypePersisted)
        {
            api.AddNode(kCheckPointNodeId, DeviceSpecificScopeType.DeviceType, "9", kGlobalNodeId, "Check Point R8x");
        }
        api.AddNode(kFortiAdomNodeId, DeviceSpecificScopeType.DeviceType, "11", kGlobalNodeId, "FortiADOM 5ff");
        api.AddNode(kManagementNodeId, DeviceSpecificScopeType.Management, "100", kFortiAdomNodeId, "cp-mgr");
        return api;
    }

    private static DeviceSpecificSettingsService Service(InMemoryDeviceSpecificSettingsApiConnection api) => new(new DeviceSpecificSettingsManager(api));

    private static InMemoryDeviceSpecificSettingsApiConnection PersistedCheckPointChain(bool includeGateway)
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, "global", displayName: "Global");
        api.AddNode(kCheckPointNodeId, DeviceSpecificScopeType.DeviceType, "9", kGlobalNodeId, "Check Point R8x");
        api.AddNode(kManagementNodeId, DeviceSpecificScopeType.Management, "100", kCheckPointNodeId, "cp-mgr");
        if (includeGateway)
        {
            api.AddNode(kGatewayNodeId, DeviceSpecificScopeType.Gateway, "200", kManagementNodeId, "cp-gw-a");
        }
        return api;
    }

    private static DeviceSpecificNode Node(DeviceSpecificNode root, DeviceSpecificScopeType scopeType, string objectKey) =>
        DeviceSpecificSettingsData.FindNode(root, new DeviceSpecificSettingsScope { ScopeType = scopeType, ObjectKey = objectKey })
            ?? throw new AssertionException($"unknown node {scopeType}:{objectKey}");

    private static DeviceSpecificFieldState State(DeviceSpecificLevelForm form, DeviceSpecificSettingKey key) =>
        form.Fields.SingleOrDefault(f => f.Field.Key == key) ?? throw new AssertionException($"field {key} not on form");
}
