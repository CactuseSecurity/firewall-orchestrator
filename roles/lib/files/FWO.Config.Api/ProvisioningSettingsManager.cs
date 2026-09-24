using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Config.Api.Provisioning;
using FWO.Data;
using FWO.Data.Provisioning;
using Newtonsoft.Json.Linq;

namespace FWO.Config.Api;

/// <summary>
/// Loads and persists explicitly selected overrides in the hierarchical provisioning configuration.
/// </summary>
public sealed class ProvisioningSettingsManager
{
    private readonly ApiConnection apiConnection;
    private readonly ProvisioningSettingValueSerializer valueSerializer;

    public ProvisioningSettingsManager(ApiConnection apiConnection)
        : this(apiConnection, new ProvisioningSettingValueSerializer())
    {
    }

    public ProvisioningSettingsManager(
        ApiConnection apiConnection,
        ProvisioningSettingValueSerializer valueSerializer)
    {
        ArgumentNullException.ThrowIfNull(apiConnection);
        ArgumentNullException.ThrowIfNull(valueSerializer);

        this.apiConnection = apiConnection;
        this.valueSerializer = valueSerializer;
    }

    public async Task<ProvisioningSettingsLevel<TSettings>> LoadLevelAsync<TSettings>(
        ProvisioningSettingsScope scope)
        where TSettings : GlobalProvisioningSettings
    {
        ArgumentNullException.ThrowIfNull(scope);
        ProvisioningSettingsHierarchyValidator.ValidateLocator(scope);
        ProvisioningSettingsHierarchyValidator.ValidateSettingsType(scope.ScopeType, typeof(TSettings));

        LoadedHierarchy hierarchy = await LoadHierarchyAsync(scope);
        ResolvedSettings resolved = ResolveSettings(hierarchy);

        return new ProvisioningSettingsLevel<TSettings>(
            resolved.Scope,
            (TSettings)resolved.Settings,
            resolved.DirectOverrides,
            resolved.ValueSources);
    }

    public async Task<ResolvedProvisioningValue<TValue>> LoadEffectiveValueAsync<TValue>(
        ProvisioningSettingsScope scope,
        ProvisioningSettingKey<TValue> key)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(key);
        ProvisioningSettingsHierarchyValidator.ValidateLocator(scope);
        ValidateKeyForScope(key, scope.ScopeType);

        LoadedHierarchy hierarchy = await LoadHierarchyAsync(scope);
        ResolvedSettings resolved = ResolveSettings(hierarchy);
        TValue value = (TValue)ProvisioningSettingsMapper.GetValue(resolved.Settings, key);

        return new ResolvedProvisioningValue<TValue>(key, value, resolved.ValueSources[key]);
    }

    public Task<ProvisioningSettingsScope> SetOverrideAsync<TValue>(
        ProvisioningSettingsScope scope,
        ProvisioningSettingKey<TValue> key,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        ProvisioningSettingsChangeSet changes = new ProvisioningSettingsChangeSet(scope).Set(key, value);
        return ApplyChangesAsync(changes);
    }

    public async Task ClearOverrideAsync(
        ProvisioningSettingsScope scope,
        ProvisioningSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(key);
        ProvisioningSettingsHierarchyValidator.ValidateLocator(scope);
        ValidateKeyForScope(key, scope.ScopeType);

        LoadedHierarchy? hierarchy = await TryLoadPersistedHierarchyAsync(scope);
        if (hierarchy is null)
        {
            return;
        }

        await apiConnection.SendQueryAsync<ReturnId>(
            ProvisioningQueries.deleteOverrides,
            new
            {
                nodeId = hierarchy.Scope.NodeId,
                configKeys = new[] { key.DatabaseKey }
            });
    }

    public async Task<ProvisioningSettingsScope> ApplyChangesAsync(
        ProvisioningSettingsChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.IsEmpty)
        {
            return ProvisioningSettingsScopeFactory.Copy(changes.Scope);
        }

        ProvisioningSettingsHierarchyValidator.ValidateLocator(changes.Scope);

        List<SerializedOverride> serializedUpserts = [];
        foreach (ProvisioningSettingOverride upsert in changes.Upserts)
        {
            ValidateKeyForScope(upsert.Key, changes.Scope.ScopeType);
            serializedUpserts.Add(new SerializedOverride(
                upsert.Key,
                valueSerializer.Serialize(upsert.Key, upsert.Value)));
        }

        foreach (ProvisioningSettingKey removal in changes.Removals)
        {
            ValidateKeyForScope(removal, changes.Scope.ScopeType);
        }

        LoadedHierarchy? persistedHierarchy = await TryLoadPersistedHierarchyAsync(changes.Scope);
        if (persistedHierarchy is null && serializedUpserts.Count == 0)
        {
            return ProvisioningSettingsScopeFactory.Copy(changes.Scope);
        }

        LoadedHierarchy hierarchy = persistedHierarchy ?? await LoadUnpersistedHierarchyAsync(changes.Scope);

        ProvisioningSettingsScope nodeToUpsert = CreateNodeForUpsert(changes.Scope, hierarchy);
        ProvisioningSettingsScope persistedScope = await UpsertNodeAsync(nodeToUpsert);

        string[] removeKeys = changes.Removals
            .Select(key => key.DatabaseKey)
            .ToArray();

        if (serializedUpserts.Count > 0)
        {
            object[] upserts = serializedUpserts
                .Select(upsert => (object)new
                {
                    node_id = persistedScope.NodeId,
                    config_key = upsert.Key.DatabaseKey,
                    config_value = upsert.Value
                })
                .ToArray();

            await apiConnection.SendQueryAsync<ReturnId>(
                ProvisioningQueries.applyPatch,
                new
                {
                    upserts,
                    nodeId = persistedScope.NodeId,
                    removeKeys
                });
        }
        else
        {
            await apiConnection.SendQueryAsync<ReturnId>(
                ProvisioningQueries.deleteOverrides,
                new
                {
                    nodeId = persistedScope.NodeId,
                    configKeys = removeKeys
                });
        }

        return persistedScope;
    }

    /// <summary>
    /// Persists the node of a scope without storing any override, so that child scopes can reference it
    /// as their parent. Returns the canonical scope of the existing or newly created node.
    /// </summary>
    public async Task<ProvisioningSettingsScope> EnsureNodeAsync(ProvisioningSettingsScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ProvisioningSettingsHierarchyValidator.ValidateLocator(scope);

        LoadedHierarchy? persistedHierarchy = await TryLoadPersistedHierarchyAsync(scope);
        if (persistedHierarchy is not null)
        {
            return persistedHierarchy.Scope;
        }

        LoadedHierarchy hierarchy = await LoadUnpersistedHierarchyAsync(scope);
        return await UpsertNodeAsync(CreateNodeForUpsert(scope, hierarchy));
    }

    public async Task<IReadOnlyList<ProvisioningSettingsScope>> GetChildrenAsync(long parentNodeId)
    {
        if (parentNodeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parentNodeId), parentNodeId, "A positive parent node ID is required.");
        }

        ProvisioningConfigNodeData parentNode = await LoadNodeByIdAsync(parentNodeId);
        IReadOnlyList<ProvisioningConfigNodeData> parentNodes = BuildNodeChain(parentNode);
        IReadOnlyList<ProvisioningSettingsScope> parentScopes = CreateScopeChain(parentNodes);
        ProvisioningSettingsHierarchyValidator.ValidatePersistedChain(parentScopes, parentScopes[^1].ScopeType);
        ValidateNodeValues(parentNodes, parentScopes);

        List<ProvisioningConfigNodeData> childNodes = await apiConnection.SendQueryAsync<List<ProvisioningConfigNodeData>>(
            ProvisioningQueries.getChildren,
            new { parentNodeId });

        List<ProvisioningSettingsScope> children = [];
        HashSet<long> nodeIds = [];
        HashSet<(ProvisioningScopeType ScopeType, string ObjectKey)> naturalKeys = [];

        foreach (ProvisioningConfigNodeData childNode in childNodes)
        {
            ProvisioningSettingsScope child = ProvisioningSettingsScopeFactory.Create(childNode);
            ProvisioningSettingsHierarchyValidator.ValidateChild(parentScopes, child);

            if (!nodeIds.Add(child.NodeId))
            {
                throw new InvalidOperationException($"Child query returned node ID '{child.NodeId}' more than once.");
            }

            if (!naturalKeys.Add((child.ScopeType, child.ObjectKey)))
            {
                throw new InvalidOperationException(
                    $"Child query returned scope '{child.ScopeType}:{child.ObjectKey}' more than once.");
            }

            children.Add(child);
        }

        return children;
    }

    private async Task<LoadedHierarchy> LoadHierarchyAsync(ProvisioningSettingsScope requestedScope)
    {
        return await TryLoadPersistedHierarchyAsync(requestedScope)
            ?? await LoadUnpersistedHierarchyAsync(requestedScope);
    }

    private async Task<LoadedHierarchy?> TryLoadPersistedHierarchyAsync(
        ProvisioningSettingsScope requestedScope)
    {
        List<ProvisioningConfigNodeData> matches = await apiConnection.SendQueryAsync<List<ProvisioningConfigNodeData>>(
            ProvisioningQueries.getNodeWithAncestors,
            new
            {
                nodeType = ProvisioningSettingsScopeFactory.ToNodeType(requestedScope.ScopeType),
                objectKey = requestedScope.ObjectKey
            });

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Database returned {matches.Count} nodes for provisioning scope "
                + $"'{requestedScope.ScopeType}:{requestedScope.ObjectKey}'.");
        }

        if (matches.Count == 1)
        {
            IReadOnlyList<ProvisioningConfigNodeData> nodes = BuildNodeChain(matches[0]);
            IReadOnlyList<ProvisioningSettingsScope> scopes = CreateScopeChain(nodes);
            ProvisioningSettingsHierarchyValidator.ValidatePersistedChain(scopes, requestedScope.ScopeType);
            ProvisioningSettingsHierarchyValidator.ValidateRequestedScope(requestedScope, scopes[^1]);
            ValidateNodeValues(nodes, scopes);
            return new LoadedHierarchy(scopes[^1], nodes, SelectedNodeExists: true);
        }

        return null;
    }

    private async Task<LoadedHierarchy> LoadUnpersistedHierarchyAsync(
        ProvisioningSettingsScope requestedScope)
    {
        if (requestedScope.NodeId != 0)
        {
            throw new KeyNotFoundException(
                $"Provisioning node '{requestedScope.NodeId}' for scope "
                + $"'{requestedScope.ScopeType}:{requestedScope.ObjectKey}' does not exist.");
        }

        ProvisioningSettingsScope unpersistedScope = ProvisioningSettingsScopeFactory.Copy(requestedScope);
        if (requestedScope.ScopeType == ProvisioningScopeType.Global)
        {
            ProvisioningSettingsHierarchyValidator.ValidateNewNode(unpersistedScope);
            return new LoadedHierarchy(unpersistedScope, [], SelectedNodeExists: false);
        }

        if (requestedScope.ParentNodeId is null)
        {
            throw new ArgumentException(
                $"Unpersisted provisioning scope '{requestedScope.ScopeType}:{requestedScope.ObjectKey}' requires a parent node ID.",
                nameof(requestedScope));
        }

        ProvisioningConfigNodeData parentNode = await LoadNodeByIdAsync(requestedScope.ParentNodeId.Value);
        IReadOnlyList<ProvisioningConfigNodeData> parentNodes = BuildNodeChain(parentNode);
        IReadOnlyList<ProvisioningSettingsScope> parentScopes = CreateScopeChain(parentNodes);
        ProvisioningScopeType expectedParentType = ProvisioningSettingsHierarchyValidator.GetParentType(requestedScope.ScopeType)
            ?? throw new InvalidOperationException("Only non-global scopes can reach parent validation.");

        ProvisioningSettingsHierarchyValidator.ValidatePersistedChain(parentScopes, expectedParentType);
        ProvisioningSettingsHierarchyValidator.ValidateNewChild(parentScopes, unpersistedScope);
        ValidateNodeValues(parentNodes, parentScopes);

        return new LoadedHierarchy(unpersistedScope, parentNodes, SelectedNodeExists: false);
    }

    private async Task<ProvisioningConfigNodeData> LoadNodeByIdAsync(long nodeId)
    {
        List<ProvisioningConfigNodeData> matches = await apiConnection.SendQueryAsync<List<ProvisioningConfigNodeData>>(
            ProvisioningQueries.getNodeById,
            new { nodeId });

        if (matches.Count == 0)
        {
            throw new KeyNotFoundException($"Provisioning node '{nodeId}' does not exist.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Database returned {matches.Count} provisioning nodes for ID '{nodeId}'.");
        }

        if (matches[0].Id != nodeId)
        {
            throw new InvalidOperationException(
                $"Database returned provisioning node '{matches[0].Id}' for requested ID '{nodeId}'.");
        }

        return matches[0];
    }

    private ResolvedSettings ResolveSettings(LoadedHierarchy hierarchy)
    {
        GlobalProvisioningSettings settings = ProvisioningSettingsMapper.CreateDefaults(hierarchy.Scope.ScopeType);
        settings.Scope = hierarchy.Scope;

        Dictionary<ProvisioningSettingKey, ProvisioningSettingValueSource> sources = ProvisioningSettingKeys.All
            .Where(key => key.IsAllowedAt(hierarchy.Scope.ScopeType))
            .ToDictionary(key => key, _ => new ProvisioningSettingValueSource(null));

        HashSet<ProvisioningSettingKey> directOverrides = [];

        foreach (ProvisioningConfigNodeData node in hierarchy.Nodes)
        {
            ProvisioningSettingsScope sourceScope = ProvisioningSettingsScopeFactory.Create(node);
            foreach (ProvisioningConfigValueData storedValue in node.Values)
            {
                ProvisioningSettingKey key = ProvisioningSettingKeys.ByDatabaseKey[storedValue.ConfigKey];
                object value = valueSerializer.Deserialize(key, storedValue.ConfigValue);
                ProvisioningSettingsMapper.SetValue(settings, key, value);
                sources[key] = new ProvisioningSettingValueSource(sourceScope);

                if (hierarchy.SelectedNodeExists && node.Id == hierarchy.Scope.NodeId)
                {
                    directOverrides.Add(key);
                }
            }
        }

        return new ResolvedSettings(hierarchy.Scope, settings, directOverrides, sources);
    }

    private async Task<ProvisioningSettingsScope> UpsertNodeAsync(ProvisioningSettingsScope scope)
    {
        ProvisioningConfigNodeData result = await apiConnection.SendQueryAsync<ProvisioningConfigNodeData>(
            ProvisioningQueries.upsertNode,
            new
            {
                node = new
                {
                    node_type = ProvisioningSettingsScopeFactory.ToNodeType(scope.ScopeType),
                    object_key = scope.ObjectKey,
                    parent_id = scope.ParentNodeId,
                    display_name = scope.DisplayName ?? "",
                    sort_order = scope.SortOrder
                }
            });

        ProvisioningSettingsScope persisted = ProvisioningSettingsScopeFactory.Create(result);
        if (persisted.NodeId <= 0)
        {
            throw new InvalidOperationException("Node upsert returned an invalid provisioning node ID.");
        }

        ProvisioningSettingsHierarchyValidator.ValidateRequestedScope(scope, persisted);
        return persisted;
    }

    private static ProvisioningSettingsScope CreateNodeForUpsert(
        ProvisioningSettingsScope requested,
        LoadedHierarchy hierarchy)
    {
        return new ProvisioningSettingsScope
        {
            ScopeType = requested.ScopeType,
            ObjectKey = requested.ObjectKey,
            DisplayName = requested.DisplayName ?? hierarchy.Scope.DisplayName ?? "",
            NodeId = hierarchy.Scope.NodeId,
            ParentNodeId = hierarchy.Scope.ParentNodeId,
            SortOrder = requested.SortOrder
        };
    }

    private static IReadOnlyList<ProvisioningConfigNodeData> BuildNodeChain(ProvisioningConfigNodeData leaf)
    {
        List<ProvisioningConfigNodeData> reversed = [];
        ProvisioningConfigNodeData? current = leaf;

        while (current is not null)
        {
            reversed.Add(current);
            if (reversed.Count > 4)
            {
                throw new InvalidOperationException("Provisioning hierarchy contains more than four levels.");
            }
            current = current.ParentNode;
        }

        reversed.Reverse();
        return reversed;
    }

    private static IReadOnlyList<ProvisioningSettingsScope> CreateScopeChain(
        IReadOnlyList<ProvisioningConfigNodeData> nodes)
    {
        return nodes.Select(ProvisioningSettingsScopeFactory.Create).ToList();
    }

    private static void ValidateNodeValues(
        IReadOnlyList<ProvisioningConfigNodeData> nodes,
        IReadOnlyList<ProvisioningSettingsScope> scopes)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            ProvisioningConfigNodeData node = nodes[index];
            ProvisioningSettingsScope scope = scopes[index];
            HashSet<string> keys = new(StringComparer.Ordinal);

            foreach (ProvisioningConfigValueData storedValue in node.Values)
            {
                if (storedValue.NodeId != node.Id)
                {
                    throw new InvalidOperationException(
                        $"Provisioning value '{storedValue.ConfigKey}' references node '{storedValue.NodeId}' "
                        + $"but was returned for node '{node.Id}'.");
                }

                if (!keys.Add(storedValue.ConfigKey))
                {
                    throw new InvalidOperationException(
                        $"Provisioning node '{node.Id}' contains duplicate setting key '{storedValue.ConfigKey}'.");
                }

                if (!ProvisioningSettingKeys.ByDatabaseKey.TryGetValue(storedValue.ConfigKey, out ProvisioningSettingKey? key))
                {
                    throw new InvalidOperationException(
                        $"Provisioning node '{node.Id}' contains unknown setting key '{storedValue.ConfigKey}'.");
                }

                if (!key.IsAllowedAt(scope.ScopeType))
                {
                    throw new InvalidOperationException(
                        $"Provisioning setting '{key.DatabaseKey}' is not valid at scope type '{scope.ScopeType}'.");
                }

                if (storedValue.ConfigValue is null)
                {
                    throw new InvalidOperationException(
                        $"Provisioning setting '{key.DatabaseKey}' on node '{node.Id}' has no JSON value.");
                }
            }
        }
    }

    private static void ValidateKeyForScope(ProvisioningSettingKey key, ProvisioningScopeType scopeType)
    {
        if (!key.IsAllowedAt(scopeType))
        {
            throw new ArgumentException(
                $"Setting '{key.DatabaseKey}' cannot be overridden at scope '{scopeType}'.",
                nameof(key));
        }

        if (!ProvisioningSettingKeys.ByDatabaseKey.TryGetValue(key.DatabaseKey, out ProvisioningSettingKey? knownKey)
            || !ReferenceEquals(knownKey, key))
        {
            throw new ArgumentException(
                $"Setting key '{key.DatabaseKey}' is not part of the canonical provisioning setting catalog.",
                nameof(key));
        }
    }

    private sealed record LoadedHierarchy(
        ProvisioningSettingsScope Scope,
        IReadOnlyList<ProvisioningConfigNodeData> Nodes,
        bool SelectedNodeExists);

    private sealed record ResolvedSettings(
        ProvisioningSettingsScope Scope,
        GlobalProvisioningSettings Settings,
        IReadOnlySet<ProvisioningSettingKey> DirectOverrides,
        IReadOnlyDictionary<ProvisioningSettingKey, ProvisioningSettingValueSource> ValueSources);

    private sealed record SerializedOverride(ProvisioningSettingKey Key, JToken Value);
}
