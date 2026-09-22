using System.Collections;
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

    private sealed record ApiCall(string Query, object? Variables);

    private sealed class InMemoryProvisioningApiConnection : SimulatedApiConnection
    {
        public List<ProvisioningConfigNodeData> Nodes { get; } = [];

        public List<ApiCall> Calls { get; } = [];

        public string? FailingQuery { get; set; }

        public Exception? Failure { get; set; }

        public int UpsertCallCount => Calls.Count(call => call.Query == ProvisioningQueries.upsertNode);

        public int PatchCallCount => Calls.Count(call => call.Query == ProvisioningQueries.applyPatch);

        public int DeleteCallCount => Calls.Count(call => call.Query == ProvisioningQueries.deleteOverrides);

        public IReadOnlyList<string> LastPatchUpsertKeys { get; private set; } = [];

        public IReadOnlyList<string> LastPatchRemoveKeys { get; private set; } = [];

        public IReadOnlyList<string> LastDeletedKeys { get; private set; } = [];

        public void SetValue<TValue>(
            long nodeId,
            ProvisioningSettingKey<TValue> key,
            TValue value)
        {
            JToken serialized = new ProvisioningSettingValueSerializer().Serialize(key, value);
            ProvisioningConfigNodeData node = Nodes.Single(node => node.Id == nodeId);
            ProvisioningConfigValueData? existing = node.Values.SingleOrDefault(item => item.ConfigKey == key.DatabaseKey);
            if (existing is null)
            {
                node.Values.Add(new ProvisioningConfigValueData
                {
                    NodeId = nodeId,
                    ConfigKey = key.DatabaseKey,
                    ConfigValue = serialized
                });
            }
            else
            {
                existing.ConfigValue = serialized;
            }
        }

        public JToken? ReadValue(long nodeId, ProvisioningSettingKey key)
        {
            return Nodes.Single(node => node.Id == nodeId)
                .Values.SingleOrDefault(value => value.ConfigKey == key.DatabaseKey)
                ?.ConfigValue;
        }

        public override Task<T> SendQueryAsync<T>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            Calls.Add(new ApiCall(query, variables));
            if (query == FailingQuery)
            {
                throw Failure ?? new InvalidOperationException("Simulated API failure.");
            }

            RefreshParentLinks();
            object result = query switch
            {
                _ when query == ProvisioningQueries.getNodeWithAncestors => GetNodeWithAncestors(variables),
                _ when query == ProvisioningQueries.getNodeById => GetNodeById(variables),
                _ when query == ProvisioningQueries.getChildren => GetChildren(variables),
                _ when query == ProvisioningQueries.upsertNode => UpsertNode(variables),
                _ when query == ProvisioningQueries.applyPatch => ApplyPatch(variables),
                _ when query == ProvisioningQueries.deleteOverrides => DeleteOverrides(variables),
                _ => throw new InvalidOperationException("Unexpected provisioning query.")
            };

            return Task.FromResult((T)result);
        }

        private List<ProvisioningConfigNodeData> GetNodeWithAncestors(object? variables)
        {
            string nodeType = RequiredProperty<string>(RequiredVariables(variables), "nodeType");
            string objectKey = RequiredProperty<string>(RequiredVariables(variables), "objectKey");
            return Nodes
                .Where(node => node.NodeType == nodeType && node.ObjectKey == objectKey)
                .ToList();
        }

        private List<ProvisioningConfigNodeData> GetNodeById(object? variables)
        {
            long nodeId = RequiredProperty<long>(RequiredVariables(variables), "nodeId");
            return Nodes.Where(node => node.Id == nodeId).ToList();
        }

        private List<ProvisioningConfigNodeData> GetChildren(object? variables)
        {
            long parentNodeId = RequiredProperty<long>(RequiredVariables(variables), "parentNodeId");
            return Nodes.Where(node => node.ParentId == parentNodeId).ToList();
        }

        private ProvisioningConfigNodeData UpsertNode(object? variables)
        {
            object input = RequiredProperty<object>(RequiredVariables(variables), "node");
            string nodeType = RequiredProperty<string>(input, "node_type");
            string objectKey = RequiredProperty<string>(input, "object_key");
            ProvisioningConfigNodeData? node = Nodes.SingleOrDefault(item =>
                item.NodeType == nodeType && item.ObjectKey == objectKey);

            if (node is null)
            {
                node = new ProvisioningConfigNodeData
                {
                    Id = Nodes.Count == 0 ? 1 : Nodes.Max(item => item.Id) + 1,
                    NodeType = nodeType,
                    ObjectKey = objectKey
                };
                Nodes.Add(node);
            }

            node.ParentId = OptionalProperty<long>(input, "parent_id");
            node.DisplayName = RequiredProperty<string>(input, "display_name");
            node.SortOrder = OptionalProperty<int>(input, "sort_order");
            RefreshParentLinks();
            return node;
        }

        private ReturnId ApplyPatch(object? variables)
        {
            object vars = RequiredVariables(variables);
            IEnumerable upserts = RequiredProperty<IEnumerable>(vars, "upserts");
            List<string> upsertKeys = [];

            foreach (object upsert in upserts)
            {
                long nodeId = RequiredProperty<long>(upsert, "node_id");
                string configKey = RequiredProperty<string>(upsert, "config_key");
                JToken configValue = RequiredProperty<JToken>(upsert, "config_value");
                ProvisioningConfigNodeData node = Nodes.Single(item => item.Id == nodeId);
                ProvisioningConfigValueData? existing = node.Values.SingleOrDefault(item => item.ConfigKey == configKey);
                if (existing is null)
                {
                    node.Values.Add(new ProvisioningConfigValueData
                    {
                        NodeId = nodeId,
                        ConfigKey = configKey,
                        ConfigValue = configValue.DeepClone()
                    });
                }
                else
                {
                    existing.ConfigValue = configValue.DeepClone();
                }
                upsertKeys.Add(configKey);
            }

            long removalNodeId = RequiredProperty<long>(vars, "nodeId");
            string[] removeKeys = RequiredProperty<string[]>(vars, "removeKeys");
            RemoveValues(removalNodeId, removeKeys);
            LastPatchUpsertKeys = upsertKeys;
            LastPatchRemoveKeys = removeKeys;
            return new ReturnId { AffectedRows = upsertKeys.Count + removeKeys.Length };
        }

        private ReturnId DeleteOverrides(object? variables)
        {
            object vars = RequiredVariables(variables);
            long nodeId = RequiredProperty<long>(vars, "nodeId");
            string[] configKeys = RequiredProperty<string[]>(vars, "configKeys");
            int removed = RemoveValues(nodeId, configKeys);
            LastDeletedKeys = configKeys;
            return new ReturnId { AffectedRows = removed };
        }

        private int RemoveValues(long nodeId, IReadOnlyCollection<string> configKeys)
        {
            ProvisioningConfigNodeData node = Nodes.Single(item => item.Id == nodeId);
            return node.Values.RemoveAll(value => configKeys.Contains(value.ConfigKey));
        }

        private void RefreshParentLinks()
        {
            foreach (ProvisioningConfigNodeData node in Nodes)
            {
                node.ParentNode = node.ParentId is long parentId
                    ? Nodes.SingleOrDefault(parent => parent.Id == parentId)
                    : null;
            }
        }

        private static object RequiredVariables(object? variables)
        {
            return variables ?? throw new InvalidOperationException("Expected query variables.");
        }

        private static T RequiredProperty<T>(object source, string propertyName)
        {
            object? value = source.GetType().GetProperty(propertyName)?.GetValue(source);
            return value is T typed
                ? typed
                : throw new InvalidOperationException($"Expected variable property '{propertyName}' of type '{typeof(T).Name}'.");
        }

        private static T? OptionalProperty<T>(object source, string propertyName)
            where T : struct
        {
            object? value = source.GetType().GetProperty(propertyName)?.GetValue(source);
            return value is null ? null : (T)value;
        }
    }
}
