using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Config.Api.Provisioning;
using FWO.Data;
using FWO.Data.Provisioning;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
[Parallelizable]
internal class ProvisioningSettingsManagerTest
{
    [Test]
    public async Task LoadLevel_ResolvesNearestOverridesAndReportsTheirSources()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(1, ProvisioningSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
        api.SetValue(1, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.Log);
        api.SetValue(2, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        api.SetValue(3, ProvisioningSettingKeys.InstallOn, "management-cluster");
        api.SetValue(4, ProvisioningSettingKeys.ZoneTo, "dmz");
        ProvisioningSettingsManager manager = new(api);

        ProvisioningSettingsLevel<GatewayProvisioningSettings> result =
            await manager.LoadLevelAsync<GatewayProvisioningSettings>(GatewayScope());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Manual));
            Assert.That(result.Settings.Logging, Is.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(result.Settings.InstallOn, Is.EqualTo("management-cluster"));
            Assert.That(result.Settings.ZoneTo, Is.EqualTo("dmz"));
            Assert.That(result.Settings.Templates, Is.Empty);
            Assert.That(result.DirectOverrides, Is.EquivalentTo(new[] { ProvisioningSettingKeys.ZoneTo }));
            Assert.That(result.ValueSources[ProvisioningSettingKeys.ImplementationMode].Scope?.NodeId, Is.EqualTo(1));
            Assert.That(result.ValueSources[ProvisioningSettingKeys.Logging].Scope?.NodeId, Is.EqualTo(2));
            Assert.That(result.ValueSources[ProvisioningSettingKeys.InstallOn].Scope?.NodeId, Is.EqualTo(3));
            Assert.That(result.ValueSources[ProvisioningSettingKeys.ZoneTo].Scope?.NodeId, Is.EqualTo(4));
            Assert.That(result.ValueSources[ProvisioningSettingKeys.Templates].UsesCompiledDefault, Is.True);
        }
    }

    [Test]
    public async Task LoadEffectiveValue_ReturnsNearestValueAndItsSource()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(1, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.Log);
        api.SetValue(2, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        ProvisioningSettingsManager manager = new(api);

        ResolvedProvisioningValue<ProvisioningLoggingMode> result =
            await manager.LoadEffectiveValueAsync(GatewayScope(), ProvisioningSettingKeys.Logging);

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
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.Log);
        api.SetValue(4, ProvisioningSettingKeys.ZoneTo, "existing-zone");
        ProvisioningSettingsManager manager = new(api);

        ProvisioningSettingsScope result = await manager.SetOverrideAsync(
            GatewayScope(),
            ProvisioningSettingKeys.Logging,
            ProvisioningLoggingMode.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NodeId, Is.EqualTo(4));
            Assert.That(api.PatchCallCount, Is.EqualTo(1));
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.Logging.DatabaseKey }));
            Assert.That(api.LastPatchRemoveKeys, Is.Empty);
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.Logging), Is.EqualTo(new JValue("None")));
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.ZoneTo), Is.EqualTo(new JValue("existing-zone")));
        }
    }

    [Test]
    public async Task ApplyChanges_UpsertsAndRemovesOnlyExplicitKeysInOnePatch()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.Log);
        api.SetValue(4, ProvisioningSettingKeys.ZoneFrom, "existing-from");
        api.SetValue(4, ProvisioningSettingKeys.ZoneTo, "existing-to");
        ProvisioningSettingsManager manager = new(api);
        ProvisioningSettingsChangeSet changes = new ProvisioningSettingsChangeSet(GatewayScope())
            .Set(ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack)
            .Remove(ProvisioningSettingKeys.ZoneFrom);

        await manager.ApplyChangesAsync(changes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.PatchCallCount, Is.EqualTo(1));
            Assert.That(api.DeleteCallCount, Is.Zero);
            Assert.That(api.LastPatchUpsertKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.Logging.DatabaseKey }));
            Assert.That(api.LastPatchRemoveKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.ZoneFrom.DatabaseKey }));
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.Logging), Is.EqualTo(new JValue("LogTrack")));
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.ZoneFrom), Is.Null);
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.ZoneTo), Is.EqualTo(new JValue("existing-to")));
        }
    }

    [Test]
    public async Task ApplyChanges_WithOnlyRemoval_ClearsOnlyExplicitOverride()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        api.SetValue(4, ProvisioningSettingKeys.ZoneTo, "keep-me");
        ProvisioningSettingsManager manager = new(api);
        ProvisioningSettingsChangeSet changes = new ProvisioningSettingsChangeSet(GatewayScope())
            .Remove(ProvisioningSettingKeys.Logging);

        await manager.ApplyChangesAsync(changes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.PatchCallCount, Is.Zero);
            Assert.That(api.DeleteCallCount, Is.EqualTo(1));
            Assert.That(api.LastDeletedKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.Logging.DatabaseKey }));
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.Logging), Is.Null);
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.ZoneTo), Is.EqualTo(new JValue("keep-me")));
        }
    }

    [Test]
    public async Task ClearOverride_ClearsOnlyRequestedOverrideWithoutUpsertingNode()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.SetValue(4, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        api.SetValue(4, ProvisioningSettingKeys.ZoneTo, "keep-me");
        ProvisioningSettingsManager manager = new(api);

        await manager.ClearOverrideAsync(GatewayScope(), ProvisioningSettingKeys.Logging);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(api.UpsertCallCount, Is.Zero);
            Assert.That(api.DeleteCallCount, Is.EqualTo(1));
            Assert.That(api.LastDeletedKeys, Is.EqualTo(new[] { ProvisioningSettingKeys.Logging.DatabaseKey }));
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.Logging), Is.Null);
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.ZoneTo), Is.EqualTo(new JValue("keep-me")));
        }
    }

    [Test]
    public async Task ApplyChanges_WithEmptyChangeSet_DoesNotCallApi()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        ProvisioningSettingsManager manager = new(api);
        ProvisioningSettingsScope requested = GatewayScope();

        ProvisioningSettingsScope result = await manager.ApplyChangesAsync(
            new ProvisioningSettingsChangeSet(requested));

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
        InMemoryProvisioningApiConnection api = new();
        api.Nodes.Add(Node(1, ProvisioningScopeType.Global, "global"));
        ProvisioningSettingsManager manager = new(api);
        ProvisioningSettingsScope invalidManagement = new()
        {
            ScopeType = ProvisioningScopeType.Management,
            ObjectKey = "100",
            ParentNodeId = 1
        };

        Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.SetOverrideAsync(
            invalidManagement,
            ProvisioningSettingKeys.Logging,
            ProvisioningLoggingMode.LogTrack));

        Assert.That(api.UpsertCallCount, Is.Zero);
    }

    [Test]
    public async Task LoadLevel_MissingNodeUsesPersistedParentWithoutCreatingNode()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy(includeGateway: false);
        api.SetValue(2, ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.LogTrack);
        api.SetValue(3, ProvisioningSettingKeys.InstallOn, "parent-management");
        ProvisioningSettingsManager manager = new(api);
        ProvisioningSettingsScope missingGateway = GatewayScope(nodeId: 0);

        ProvisioningSettingsLevel<GatewayProvisioningSettings> result =
            await manager.LoadLevelAsync<GatewayProvisioningSettings>(missingGateway);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Scope.NodeId, Is.Zero);
            Assert.That(result.Settings.Logging, Is.EqualTo(ProvisioningLoggingMode.LogTrack));
            Assert.That(result.Settings.InstallOn, Is.EqualTo("parent-management"));
            Assert.That(result.DirectOverrides, Is.Empty);
            Assert.That(api.UpsertCallCount, Is.Zero);
            Assert.That(api.PatchCallCount, Is.Zero);
        }
    }

    [Test]
    public void LoadLevel_DuplicateNaturalKeyFailsClearly()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.Nodes.Add(Node(5, ProvisioningScopeType.Gateway, "200", parentId: 3));
        ProvisioningSettingsManager manager = new(api);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.LoadLevelAsync<GatewayProvisioningSettings>(GatewayScope()));

        Assert.That(exception?.Message, Does.Contain("returned 2 nodes"));
    }

    [Test]
    public async Task SetOverride_CreatesMissingNodeAtFirstWrite()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy(includeGateway: false);
        ProvisioningSettingsManager manager = new(api);

        ProvisioningSettingsScope result = await manager.SetOverrideAsync(
            GatewayScope(nodeId: 0),
            ProvisioningSettingKeys.ZoneFrom,
            "internal");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NodeId, Is.EqualTo(4));
            Assert.That(result.ParentNodeId, Is.EqualTo(3));
            Assert.That(api.UpsertCallCount, Is.EqualTo(1));
            Assert.That(api.ReadValue(4, ProvisioningSettingKeys.ZoneFrom), Is.EqualTo(new JValue("internal")));
        }
    }

    [Test]
    public void LoadLevel_PropagatesApiFailure()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        InvalidOperationException failure = new("simulated Hasura failure");
        api.FailingQuery = ProvisioningQueries.getNodeWithAncestors;
        api.Failure = failure;
        ProvisioningSettingsManager manager = new(api);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.LoadLevelAsync<GatewayProvisioningSettings>(GatewayScope()));

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public void SetOverride_PropagatesMutationFailure()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        InvalidOperationException failure = new("simulated Hasura mutation failure");
        api.FailingQuery = ProvisioningQueries.applyPatch;
        api.Failure = failure;
        ProvisioningSettingsManager manager = new(api);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await manager.SetOverrideAsync(
                GatewayScope(),
                ProvisioningSettingKeys.Logging,
                ProvisioningLoggingMode.None));

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public async Task EnsureNode_CreatesMissingNodeWithoutStoringValues()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy(includeGateway: false);
        ProvisioningSettingsManager manager = new(api);

        ProvisioningSettingsScope result = await manager.EnsureNodeAsync(GatewayScope(nodeId: 0));

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
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        ProvisioningSettingsManager manager = new(api);

        ProvisioningSettingsScope result = await manager.EnsureNodeAsync(GatewayScope(nodeId: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NodeId, Is.EqualTo(4));
            Assert.That(api.UpsertCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task GetChildren_ReturnsValidatedImmediateChildren()
    {
        InMemoryProvisioningApiConnection api = CreateFourLevelHierarchy();
        api.Nodes.Add(Node(5, ProvisioningScopeType.Gateway, "201", parentId: 3, displayName: "Second gateway"));
        ProvisioningSettingsManager manager = new(api);

        IReadOnlyList<ProvisioningSettingsScope> children = await manager.GetChildrenAsync(3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(children.Select(child => child.NodeId), Is.EquivalentTo(new long[] { 4, 5 }));
            Assert.That(children, Has.All.Property(nameof(ProvisioningSettingsScope.ScopeType)).EqualTo(ProvisioningScopeType.Gateway));
            Assert.That(children, Has.All.Property(nameof(ProvisioningSettingsScope.ParentNodeId)).EqualTo(3));
        }
    }

    private static InMemoryProvisioningApiConnection CreateFourLevelHierarchy(bool includeGateway = true)
    {
        InMemoryProvisioningApiConnection api = new();
        api.Nodes.Add(Node(1, ProvisioningScopeType.Global, "global", displayName: "Global"));
        api.Nodes.Add(Node(2, ProvisioningScopeType.DeviceType, "fortigate", parentId: 1, displayName: "FortiGate"));
        api.Nodes.Add(Node(3, ProvisioningScopeType.Management, "100", parentId: 2, displayName: "Management"));
        if (includeGateway)
        {
            api.Nodes.Add(Node(4, ProvisioningScopeType.Gateway, "200", parentId: 3, displayName: "Gateway"));
        }
        return api;
    }

    private static ProvisioningSettingsScope GatewayScope(long nodeId = 4)
    {
        return new ProvisioningSettingsScope
        {
            ScopeType = ProvisioningScopeType.Gateway,
            ObjectKey = "200",
            DisplayName = "Gateway",
            NodeId = nodeId,
            ParentNodeId = 3,
            SortOrder = 10
        };
    }

    private static ProvisioningConfigNodeData Node(
        long id,
        ProvisioningScopeType scopeType,
        string objectKey,
        long? parentId = null,
        string? displayName = null)
    {
        return new ProvisioningConfigNodeData
        {
            Id = id,
            NodeType = ProvisioningSettingsScopeFactory.ToNodeType(scopeType),
            ObjectKey = objectKey,
            ParentId = parentId,
            DisplayName = displayName ?? objectKey
        };
    }
}
