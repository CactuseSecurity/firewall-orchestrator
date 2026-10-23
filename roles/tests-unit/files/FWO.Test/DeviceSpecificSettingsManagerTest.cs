using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
[Parallelizable]
internal class DeviceSpecificSettingsManagerTest
{
    private static readonly List<DeviceSpecificSettingKey> kExpectedZoneToKey = [DeviceSpecificSettingKeys.RuleZoneToOverride];
    private static readonly List<string> kExpectedLoggingKeys = [DeviceSpecificSettingKeys.RuleLoggingOverride.DatabaseKey];
    private static readonly List<string> kExpectedZoneFromKeys = [DeviceSpecificSettingKeys.RuleZoneFromOverride.DatabaseKey];
    private static readonly List<long> kExpectedAllNodeIds = [1, 2, 3, 4, 5];
    private static readonly List<DeviceSpecificScopeType> kExpectedLevelOrder =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management,
            DeviceSpecificScopeType.Gateway, DeviceSpecificScopeType.Gateway];
    private static readonly List<DeviceSpecificScopePath> kNoPaths = [];
    private static readonly DeviceType kFortiGateType = new() { Id = 10, Name = "FortiGate", Version = "5ff", Manufacturer = "Fortinet" };
    private static readonly DeviceType kFortiAdomType = new() { Id = 11, Name = "FortiADOM", Version = "5ff", Manufacturer = "Fortinet" };

    [Test]
    public async Task LoadLevel_ResolvesNearestOverridesAndReportsTheirSources()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(1, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
        api.SetValue(1, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.Log);
        api.SetValue(2, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(3, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride, "dmz");
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> result =
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Manual));
            Assert.That(result.Settings.RuleLoggingOverride, Is.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(result.Settings.InstallOn, Is.EqualTo(ProvisioningInstallOn.GatewaysInPath));
            Assert.That(result.Settings.RuleZoneToOverride, Is.EqualTo("dmz"));
            Assert.That(result.Settings.Templates, Is.EqualTo("{}"));
            Assert.That(result.DirectOverrides, Is.EquivalentTo(kExpectedZoneToKey));
            Assert.That(result.ValueSources[DeviceSpecificSettingKeys.ImplementationMode].Scope?.NodeId, Is.EqualTo(1));
            Assert.That(result.ValueSources[DeviceSpecificSettingKeys.RuleLoggingOverride].Scope?.NodeId, Is.EqualTo(2));
            Assert.That(result.ValueSources[DeviceSpecificSettingKeys.InstallOn].Scope?.NodeId, Is.EqualTo(3));
            Assert.That(result.ValueSources[DeviceSpecificSettingKeys.RuleZoneToOverride].Scope?.NodeId, Is.EqualTo(4));
            Assert.That(result.ValueSources[DeviceSpecificSettingKeys.Templates].UsesCompiledDefault, Is.True);
        }
    }

    [Test]
    public async Task LoadEffectiveValue_ReturnsNearestValueAndItsSource()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(1, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.Log);
        api.SetValue(2, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        DeviceSpecificSettingsManager manager = new(api);

        ResolvedDeviceSpecificValue<ProvisioningLoggingMode> result =
            await manager.LoadEffectiveValueAsync(GatewayPath(), DeviceSpecificSettingKeys.RuleLoggingOverride);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value, Is.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(result.Source.Scope?.NodeId, Is.EqualTo(2));
            Assert.That(result.Source.UsesCompiledDefault, Is.False);
        }
    }

    [Test]
    public async Task SetOverride_WritesOnlyRequestedSettingAndPreservesOtherOverrides()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.Log);
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride, "existing-zone");
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsScope result = await manager.SetOverrideAsync(
            GatewayScope(),
            DeviceSpecificSettingKeys.RuleLoggingOverride,
            ProvisioningLoggingMode.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NodeId, Is.EqualTo(4));
            Assert.That(api.PatchCallCount, Is.EqualTo(1));
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(kExpectedLoggingKeys));
            Assert.That(api.LastPatchRemoveKeys, Is.Empty);
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride), Is.EqualTo(new JValue("none")));
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride), Is.EqualTo(new JValue("existing-zone")));
        }
    }

    [Test]
    public async Task ApplyChanges_UpsertsAndRemovesOnlyExplicitKeysInOnePatch()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.Log);
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneFromOverride, "existing-from");
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride, "existing-to");
        DeviceSpecificSettingsManager manager = new(api);
        DeviceSpecificSettingsChangeSet changes = new DeviceSpecificSettingsChangeSet(GatewayScope())
            .Set(DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack)
            .Remove(DeviceSpecificSettingKeys.RuleZoneFromOverride);

        await manager.ApplyChangesAsync(changes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.PatchCallCount, Is.EqualTo(1));
            Assert.That(api.DeleteCallCount, Is.Zero);
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(kExpectedLoggingKeys));
            Assert.That(api.LastPatchRemoveKeys, Is.EqualTo(kExpectedZoneFromKeys));
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride), Is.EqualTo(new JValue("log_track")));
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleZoneFromOverride), Is.Null);
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride), Is.EqualTo(new JValue("existing-to")));
        }
    }

    [Test]
    public async Task ApplyChanges_WithOnlyRemoval_ClearsOnlyExplicitOverride()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride, "keep-me");
        DeviceSpecificSettingsManager manager = new(api);
        DeviceSpecificSettingsChangeSet changes = new DeviceSpecificSettingsChangeSet(GatewayScope())
            .Remove(DeviceSpecificSettingKeys.RuleLoggingOverride);

        await manager.ApplyChangesAsync(changes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.PatchCallCount, Is.Zero);
            Assert.That(api.DeleteCallCount, Is.EqualTo(1));
            Assert.That(api.LastDeletedKeys, Is.EqualTo(kExpectedLoggingKeys));
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride), Is.Null);
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride), Is.EqualTo(new JValue("keep-me")));
        }
    }

    [Test]
    public async Task ClearOverride_ClearsOnlyRequestedOverrideWithoutUpsertingNode()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride, "keep-me");
        DeviceSpecificSettingsManager manager = new(api);

        await manager.ClearOverrideAsync(GatewayScope(), DeviceSpecificSettingKeys.RuleLoggingOverride);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.UpsertCallCount, Is.Zero);
            Assert.That(api.DeleteCallCount, Is.EqualTo(1));
            Assert.That(api.LastDeletedKeys, Is.EqualTo(kExpectedLoggingKeys));
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleLoggingOverride), Is.Null);
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride), Is.EqualTo(new JValue("keep-me")));
        }
    }

    [Test]
    public async Task ApplyChanges_WithEmptyChangeSet_DoesNotCallApi()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        DeviceSpecificSettingsManager manager = new(api);
        DeviceSpecificSettingsScope requested = GatewayScope();

        DeviceSpecificSettingsScope result = await manager.ApplyChangesAsync(
            new DeviceSpecificSettingsChangeSet(requested));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.Calls, Is.Empty);
            Assert.That(result, Is.Not.SameAs(requested));
            Assert.That(result.NodeId, Is.EqualTo(requested.NodeId));
            Assert.That(result.ObjectKey, Is.EqualTo(requested.ObjectKey));
        }
    }

    [Test]
    public void SetOverride_RejectsParentOfWrongHierarchyType()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.Nodes.Add(Node(1, DeviceSpecificScopeType.Global, "global"));
        DeviceSpecificSettingsManager manager = new(api);
        DeviceSpecificSettingsScope invalidManagement = new()
        {
            ScopeType = DeviceSpecificScopeType.Management,
            ObjectKey = "100",
            ParentNodeId = 1
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.SetOverrideAsync(
            invalidManagement,
            DeviceSpecificSettingKeys.RuleLoggingOverride,
            ProvisioningLoggingMode.LogTrack));

        Assert.That(api.UpsertCallCount, Is.Zero);
    }

    [Test]
    public async Task LoadLevel_MissingNodeUsesPersistedParentWithoutCreatingNode()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy(includeGateway: false);
        api.SetValue(2, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(3, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> result =
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Scope.NodeId, Is.Zero);
            Assert.That(result.Settings.RuleLoggingOverride, Is.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(result.Settings.InstallOn, Is.EqualTo(ProvisioningInstallOn.GatewaysInPath));
            Assert.That(result.DirectOverrides, Is.Empty);
            Assert.That(api.UpsertCallCount, Is.Zero);
            Assert.That(api.PatchCallCount, Is.Zero);
        }
    }

    [Test]
    public void LoadLevel_DuplicateNaturalKeyFailsClearly()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.Nodes.Add(Node(5, DeviceSpecificScopeType.Gateway, "200", parentId: 3));
        DeviceSpecificSettingsManager manager = new(api);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath()));

        Assert.That(exception?.Message, Does.Contain("more than one node for device specific settings scope 'Gateway:200'"));
    }

    [Test]
    public async Task SetOverride_CreatesMissingNodeAtFirstWrite()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy(includeGateway: false);
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsScope result = await manager.SetOverrideAsync(
            GatewayScope(nodeId: 0),
            DeviceSpecificSettingKeys.RuleZoneFromOverride,
            "internal");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NodeId, Is.EqualTo(4));
            Assert.That(result.ParentNodeId, Is.EqualTo(3));
            Assert.That(api.UpsertCallCount, Is.EqualTo(1));
            Assert.That(api.ReadValue(4, DeviceSpecificSettingKeys.RuleZoneFromOverride), Is.EqualTo(new JValue("internal")));
        }
    }

    [Test]
    public void LoadLevel_PropagatesApiFailure()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        InvalidOperationException failure = new("simulated Hasura failure");
        api.FailingQuery = DeviceSpecificSettingsQueries.getNodesByNaturalKeys;
        api.Failure = failure;
        DeviceSpecificSettingsManager manager = new(api);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath()));

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public void SetOverride_PropagatesMutationFailure()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        InvalidOperationException failure = new("simulated Hasura mutation failure");
        api.FailingQuery = DeviceSpecificSettingsQueries.applyPatch;
        api.Failure = failure;
        DeviceSpecificSettingsManager manager = new(api);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.SetOverrideAsync(
                GatewayScope(),
                DeviceSpecificSettingKeys.RuleLoggingOverride,
                ProvisioningLoggingMode.None));

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public async Task EnsureNode_CreatesMissingNodeWithoutStoringValues()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy(includeGateway: false);
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsScope result = await manager.EnsureNodeAsync(GatewayScope(nodeId: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NodeId, Is.EqualTo(4));
            Assert.That(result.ParentNodeId, Is.EqualTo(3));
            Assert.That(api.UpsertCallCount, Is.EqualTo(1));
            Assert.That(api.PatchCallCount, Is.Zero);
            Assert.That(api.Nodes.Single(node => node.Id == 4).Values, Is.Empty);
        }
    }

    [Test]
    public async Task EnsureNode_ReturnsExistingNodeWithoutWriting()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsScope result = await manager.EnsureNodeAsync(GatewayScope(nodeId: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NodeId, Is.EqualTo(4));
            Assert.That(api.UpsertCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task GetAllNodes_ReturnsEveryNodeLevelByLevelWithOneQuery()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.Nodes.Insert(0, Node(5, DeviceSpecificScopeType.Gateway, "201", parentId: 3, displayName: "Second gateway"));
        DeviceSpecificSettingsManager manager = new(api);

        IReadOnlyList<DeviceSpecificSettingsScope> nodes = await manager.GetAllNodesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes.Select(node => node.NodeId), Is.EquivalentTo(kExpectedAllNodeIds));
            Assert.That(nodes.Select(node => node.ScopeType), Is.EqualTo(kExpectedLevelOrder));
            Assert.That(api.Calls, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task GetAllNodes_WithoutPersistedNodes_ReturnsEmptyList()
    {
        DeviceSpecificSettingsManager manager = new(new InMemoryDeviceSpecificSettingsApiConnection());

        Assert.That(await manager.GetAllNodesAsync(), Is.Empty);
    }

    private static IEnumerable<TestCaseData> InvalidTreeCases()
    {
        yield return new TestCaseData((Action<InMemoryDeviceSpecificSettingsApiConnection>)(api =>
            api.Nodes.Single(node => node.Id == 4).ParentId = 2)).SetName("GetAllNodes_RejectsGatewayBelowDeviceType");
        yield return new TestCaseData((Action<InMemoryDeviceSpecificSettingsApiConnection>)(api =>
            api.Nodes.Single(node => node.Id == 3).ParentId = 99)).SetName("GetAllNodes_RejectsMissingParent");
        yield return new TestCaseData((Action<InMemoryDeviceSpecificSettingsApiConnection>)(api =>
            api.Nodes.Single(node => node.Id == 2).ParentId = 3)).SetName("GetAllNodes_RejectsCycle");
        yield return new TestCaseData((Action<InMemoryDeviceSpecificSettingsApiConnection>)(api =>
            api.Nodes.Single(node => node.Id == 3).ParentId = null)).SetName("GetAllNodes_RejectsManagementWithoutParent");
        yield return new TestCaseData((Action<InMemoryDeviceSpecificSettingsApiConnection>)(api =>
            api.Nodes.Add(Node(5, DeviceSpecificScopeType.Gateway, "200", parentId: 3))))
            .SetName("GetAllNodes_RejectsDuplicateNaturalKey");
        yield return new TestCaseData((Action<InMemoryDeviceSpecificSettingsApiConnection>)(api =>
            api.Nodes.Add(Node(4, DeviceSpecificScopeType.Gateway, "201", parentId: 3))))
            .SetName("GetAllNodes_RejectsDuplicateNodeId");
    }

    [TestCaseSource(nameof(InvalidTreeCases))]
    public void GetAllNodes_RejectsInvalidHierarchy(Action<InMemoryDeviceSpecificSettingsApiConnection> corrupt)
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        corrupt(api);
        DeviceSpecificSettingsManager manager = new(api);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.GetAllNodesAsync());
    }

    [Test]
    public async Task LoadLevel_LoadsAllLevelsOfThePathWithOneQuery()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(3, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> result =
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Settings.InstallOn, Is.EqualTo(ProvisioningInstallOn.GatewaysInPath));
            Assert.That(api.Calls, Has.Count.EqualTo(1));
            Assert.That(api.Calls[0].Query, Is.EqualTo(DeviceSpecificSettingsQueries.getNodesByNaturalKeys));
        }
    }

    [Test]
    public void LoadLevel_RejectsStoredUndefinedValue()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.Nodes.Single(node => node.Id == 3).Values.Add(new DeviceSpecificConfigValueData
        {
            NodeId = 3,
            ConfigKey = DeviceSpecificSettingKeys.RuleLoggingOverride.DatabaseKey,
            ConfigValue = new JValue(DeviceSpecificSettingValueSerializer.kUndefinedEnumName)
        });
        DeviceSpecificSettingsManager manager = new(api);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath()));
    }

    [Test]
    public async Task LoadLevel_InheritsAlongThePathInsteadOfTheStoredParent()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.Nodes.Add(Node(5, DeviceSpecificScopeType.DeviceType, "11", parentId: 1, displayName: "FortiADOM"));
        api.SetValue(2, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(5, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.None);
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride, "dmz");
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> result =
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath(kFortiAdomType));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Settings.RuleLoggingOverride, Is.EqualTo(ProvisioningLoggingMode.None));
            Assert.That(result.ValueSources[DeviceSpecificSettingKeys.RuleLoggingOverride].Scope?.NodeId, Is.EqualTo(5));
            Assert.That(result.Settings.RuleZoneToOverride, Is.EqualTo("dmz"));
            Assert.That(result.DirectOverrides, Is.EquivalentTo(kExpectedZoneToKey));
            Assert.That(result.Scope.NodeId, Is.EqualTo(4));
            Assert.That(api.UpsertCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task LoadLevel_UnpersistedLevelsContributeNothing()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy(includeGateway: false);
        api.SetValue(3, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsLevel<GatewayDeviceSpecificSettings> result =
            await manager.LoadLevelAsync<GatewayDeviceSpecificSettings>(GatewayPath());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Settings.InstallOn, Is.EqualTo(ProvisioningInstallOn.GatewaysInPath));
            Assert.That(result.Scope.NodeId, Is.Zero);
            Assert.That(result.DirectOverrides, Is.Empty);
        }
    }

    [Test]
    public void LoadLevel_RejectsSettingsOfAnotherLevel()
    {
        DeviceSpecificSettingsManager manager = new(CreateFourLevelHierarchy());

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await manager.LoadLevelAsync<ManagementDeviceSpecificSettings>(GatewayPath()));
    }

    [Test]
    public async Task MoveNode_PlacesTheNodeBelowTheNewParentAndKeepsItsValues()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.Nodes.Add(Node(5, DeviceSpecificScopeType.DeviceType, "11", parentId: 1, displayName: "FortiADOM"));
        api.SetValue(3, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsScope moved = await manager.MoveNodeAsync(ManagementScope(parentNodeId: 2), 5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved.NodeId, Is.EqualTo(3));
            Assert.That(moved.ParentNodeId, Is.EqualTo(5));
            Assert.That(api.Nodes.Single(node => node.Id == 3).ParentId, Is.EqualTo(5));
            Assert.That(api.ReadValue(3, DeviceSpecificSettingKeys.InstallOn), Is.EqualTo(new JValue("gatewaysInPath")));
        }
    }

    [Test]
    public async Task MoveNode_BelowTheCurrentParent_DoesNotWrite()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        DeviceSpecificSettingsManager manager = new(api);

        DeviceSpecificSettingsScope result = await manager.MoveNodeAsync(ManagementScope(parentNodeId: 2), 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ParentNodeId, Is.EqualTo(2));
            Assert.That(api.UpsertCallCount, Is.Zero);
        }
    }

    [Test]
    public void MoveNode_RejectsAParentOfTheWrongLevel()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        DeviceSpecificSettingsManager manager = new(api);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.MoveNodeAsync(ManagementScope(parentNodeId: 2), 1));
        Assert.That(api.UpsertCallCount, Is.Zero);
    }

    [Test]
    public void MoveNode_RejectsTheGlobalNode()
    {
        DeviceSpecificSettingsManager manager = new(CreateFourLevelHierarchy());
        DeviceSpecificSettingsScope global = new() { ScopeType = DeviceSpecificScopeType.Global, ObjectKey = "global", NodeId = 1 };

        Assert.ThrowsAsync<ArgumentException>(async () => await manager.MoveNodeAsync(global, 1));
    }

    [Test]
    public void MoveNode_WithoutPersistedNode_Fails()
    {
        DeviceSpecificSettingsManager manager = new(CreateFourLevelHierarchy(includeGateway: false));

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await manager.MoveNodeAsync(GatewayScope(nodeId: 0), 3));
    }

    [Test]
    public async Task LoadEffectiveSettings_ResolvesEveryPathWithOneQuery()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(1, DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.LogTrack);
        api.SetValue(3, DeviceSpecificSettingKeys.InstallOn, ProvisioningInstallOn.GatewaysInPath);
        api.SetValue(4, DeviceSpecificSettingKeys.RuleZoneToOverride, "dmz");
        Device gatewayWithoutNode = new() { Id = 201, Name = "Gateway without node" };
        Management management = new() { Id = 100, Name = "Management", DeviceType = kFortiGateType, Devices = [gatewayWithoutNode] };
        List<DeviceSpecificScopePath> paths =
        [
            DeviceSpecificScopePath.Global(),
            DeviceSpecificScopePath.ForManagement(management),
            GatewayPath(),
            DeviceSpecificScopePath.ForGateway(management, gatewayWithoutNode)
        ];

        List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(api).LoadEffectiveSettingsAsync(paths);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.Calls.Count(call => call.Query == DeviceSpecificSettingsQueries.getNodesByNaturalKeys), Is.EqualTo(1));
            Assert.That(settings.Select(setting => setting.RuleLoggingOverride), Is.All.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(settings[0].InstallOn, Is.EqualTo(ProvisioningInstallOn.Any), "global keeps the compiled default");
            Assert.That(settings[1].InstallOn, Is.EqualTo(ProvisioningInstallOn.GatewaysInPath));
            Assert.That(settings[2].RuleZoneToOverride, Is.EqualTo("dmz"));
            Assert.That(settings[3].InstallOn, Is.EqualTo(ProvisioningInstallOn.GatewaysInPath), "inherited from the management");
            Assert.That(settings[3].RuleZoneToOverride, Is.EqualTo("ANY"), "the override of another gateway does not apply");
        }
    }

    [Test]
    public async Task LoadEffectiveSettings_WithoutPaths_SendsNoQuery()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateFourLevelHierarchy();

        List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(api).LoadEffectiveSettingsAsync(kNoPaths);

        Assert.That(settings, Is.Empty);
        Assert.That(api.Calls, Is.Empty);
    }

    /// <summary>The path Global / device type / management 100 / gateway 200, by default below device type 10.</summary>
    private static DeviceSpecificScopePath GatewayPath(DeviceType? deviceType = null)
    {
        Device gateway = new() { Id = 200, Name = "Gateway" };
        Management management = new() { Id = 100, Name = "Management", DeviceType = deviceType ?? kFortiGateType, Devices = [gateway] };
        return DeviceSpecificScopePath.ForGateway(management, gateway);
    }

    private static DeviceSpecificSettingsScope ManagementScope(long parentNodeId)
    {
        return new DeviceSpecificSettingsScope
        {
            ScopeType = DeviceSpecificScopeType.Management,
            ObjectKey = "100",
            DisplayName = "Management",
            NodeId = 3,
            ParentNodeId = parentNodeId
        };
    }

    private static InMemoryDeviceSpecificSettingsApiConnection CreateFourLevelHierarchy(bool includeGateway = true)
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.Nodes.Add(Node(1, DeviceSpecificScopeType.Global, "global", displayName: "Global"));
        api.Nodes.Add(Node(2, DeviceSpecificScopeType.DeviceType, "10", parentId: 1, displayName: "FortiGate"));
        api.Nodes.Add(Node(3, DeviceSpecificScopeType.Management, "100", parentId: 2, displayName: "Management"));
        if (includeGateway)
        {
            api.Nodes.Add(Node(4, DeviceSpecificScopeType.Gateway, "200", parentId: 3, displayName: "Gateway"));
        }
        return api;
    }

    private static DeviceSpecificSettingsScope GatewayScope(long nodeId = 4)
    {
        return new DeviceSpecificSettingsScope
        {
            ScopeType = DeviceSpecificScopeType.Gateway,
            ObjectKey = "200",
            DisplayName = "Gateway",
            NodeId = nodeId,
            ParentNodeId = 3,
            SortOrder = 10
        };
    }

    private static DeviceSpecificConfigNodeData Node(
        long id,
        DeviceSpecificScopeType scopeType,
        string objectKey,
        long? parentId = null,
        string? displayName = null)
    {
        return new DeviceSpecificConfigNodeData
        {
            Id = id,
            NodeType = DeviceSpecificSettingsScopeFactory.ToNodeType(scopeType),
            ObjectKey = objectKey,
            ParentId = parentId,
            DisplayName = displayName ?? objectKey
        };
    }
}
