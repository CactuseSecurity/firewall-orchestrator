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
    /// <summary>Global, device type, management and gateway.</summary>
    private const int kMaxHierarchyDepth = 4;

    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Creates a manager that reads and writes the provisioning configuration through the given API connection.
    /// </summary>
    public ProvisioningSettingsManager(ApiConnection apiConnection)
    {
        ArgumentNullException.ThrowIfNull(apiConnection);

        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Resolves the settings in effect at the most specific level of a path, together with the overrides stored
    /// directly on that level and the source of every value. Values are inherited along the path, i.e. along the
    /// current device hierarchy, and never along the stored parent links of the provisioning nodes, which are
    /// outdated for a management or gateway moved since its node was stored. Levels without a stored node
    /// contribute no values.
    /// </summary>
    public async Task<ProvisioningSettingsLevel<TSettings>> LoadLevelAsync<TSettings>(ProvisioningScopePath path)
        where TSettings : GlobalProvisioningSettings
    {
        ArgumentNullException.ThrowIfNull(path);
        ProvisioningSettingsHierarchyValidator.ValidateSettingsType(path.ScopeType, typeof(TSettings));

        ResolvedSettings resolved = await ResolveAlongPathAsync(path);
        return new ProvisioningSettingsLevel<TSettings>(
            resolved.Scope,
            (TSettings)resolved.Settings,
            resolved.DirectOverrides,
            resolved.ValueSources);
    }

    /// <summary>
    /// Resolves the value of a single setting in effect at the most specific level of a path and the level it
    /// comes from. Like <see cref="LoadLevelAsync{TSettings}"/>, the value is inherited along the path.
    /// </summary>
    public async Task<ResolvedProvisioningValue<TValue>> LoadEffectiveValueAsync<TValue>(
        ProvisioningScopePath path,
        ProvisioningSettingKey<TValue> key)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(key);
        ValidateKeyForScope(key, path.ScopeType);

        ResolvedSettings resolved = await ResolveAlongPathAsync(path);
        TValue value = (TValue)ProvisioningSettingsMapper.GetValue(resolved.Settings, key);
        return new ResolvedProvisioningValue<TValue>(key, value, resolved.ValueSources[key]);
    }

    /// <summary>
    /// Stores an override of a single setting at a scope, creating the scope's node if needed.
    /// </summary>
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

    /// <summary>
    /// Removes the override of a single setting from a scope, so that the scope inherits the value again.
    /// Does nothing if the scope has no persisted node.
    /// </summary>
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
                configKeys = new List<string> { key.DatabaseKey }
            });
    }

    /// <summary>
    /// Applies an explicit patch of upserts and removals to one scope and returns the persisted scope.
    /// The scope's node is only created when the patch stores at least one value.
    /// </summary>
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
                ProvisioningSettingValueSerializer.Serialize(upsert.Key, upsert.Value)));
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

        List<string> removeKeys = changes.Removals
            .Select(key => key.DatabaseKey)
            .ToList();

        if (serializedUpserts.Count > 0)
        {
            List<object> upserts = serializedUpserts
                .Select(upsert => (object)new
                {
                    node_id = persistedScope.NodeId,
                    config_key = upsert.Key.DatabaseKey,
                    config_value = upsert.Value
                })
                .ToList();

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

    /// <summary>
    /// Moves a persisted node below another persisted node, e.g. after the node's management was assigned to another
    /// device type. The node keeps its own overrides; the new parent must be of the level directly above the node.
    /// Returns the canonical scope of the moved node.
    /// </summary>
    public async Task<ProvisioningSettingsScope> MoveNodeAsync(ProvisioningSettingsScope scope, long newParentNodeId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ProvisioningSettingsHierarchyValidator.ValidateLocator(scope);
        ProvisioningScopeType expectedParentType = ProvisioningSettingsHierarchyValidator.GetParentType(scope.ScopeType)
            ?? throw new ArgumentException("The global provisioning node cannot be moved.", nameof(scope));

        ProvisioningConfigNodeData node = await TryLoadNodeByNaturalKeyAsync(scope)
            ?? throw new KeyNotFoundException($"Provisioning scope '{scope.ScopeType}:{scope.ObjectKey}' has no persisted node.");
        ProvisioningSettingsScope currentScope = ProvisioningSettingsScopeFactory.Create(node);
        ProvisioningSettingsHierarchyValidator.ValidateRequestedScope(ScopeWithoutParent(scope), currentScope);

        IReadOnlyList<ProvisioningConfigNodeData> parentNodes = BuildNodeChain(await LoadNodeByIdAsync(newParentNodeId));
        IReadOnlyList<ProvisioningSettingsScope> parentScopes = CreateScopeChain(parentNodes);
        ProvisioningSettingsHierarchyValidator.ValidatePersistedChain(parentScopes, expectedParentType);

        if (currentScope.ParentNodeId == newParentNodeId)
        {
            return currentScope;
        }

        return await UpsertNodeAsync(new ProvisioningSettingsScope
        {
            ScopeType = currentScope.ScopeType,
            ObjectKey = currentScope.ObjectKey,
            DisplayName = scope.DisplayName ?? currentScope.DisplayName ?? "",
            NodeId = currentScope.NodeId,
            ParentNodeId = newParentNodeId,
            SortOrder = currentScope.SortOrder
        });
    }

    /// <summary>
    /// Returns the validated scopes of all persisted nodes, Global first and every level before the one below it.
    /// Loads them with a single query and validates the hierarchy in memory, so the cost does not grow with the number
    /// of API calls per node. Returns an empty list if nothing has been stored yet.
    /// </summary>
    public async Task<IReadOnlyList<ProvisioningSettingsScope>> GetAllNodesAsync()
    {
        List<ProvisioningConfigNodeData> nodes =
            await apiConnection.SendQueryAsync<List<ProvisioningConfigNodeData>>(ProvisioningQueries.getAllNodes);
        return ProvisioningSettingsHierarchyValidator.ValidateTree(CreateScopeChain(nodes));
    }

    /// <summary>
    /// Merges the values stored on the levels of a path, Global first, looking every level's node up by its
    /// scope type and object key.
    /// </summary>
    private async Task<ResolvedSettings> ResolveAlongPathAsync(ProvisioningScopePath path)
    {
        List<ProvisioningSettingsScope> chain = path.ToScopes();
        ProvisioningSettingsHierarchyValidator.ValidateRequestedChain(chain);
        ProvisioningSettingsScope requestedScope = chain[^1];
        Dictionary<(ProvisioningScopeType ScopeType, string ObjectKey), ProvisioningConfigNodeData> storedNodes = await LoadNodesOfChainAsync(chain);

        List<ProvisioningConfigNodeData> nodes = [];
        ProvisioningSettingsScope resolvedScope = ProvisioningSettingsScopeFactory.Copy(requestedScope);
        bool selectedNodeExists = false;
        foreach (ProvisioningSettingsScope scope in chain)
        {
            if (!storedNodes.TryGetValue((scope.ScopeType, scope.ObjectKey), out ProvisioningConfigNodeData? node))
            {
                continue;
            }

            ProvisioningSettingsScope persistedScope = ProvisioningSettingsScopeFactory.Create(node);
            ProvisioningSettingsHierarchyValidator.ValidateRequestedScope(ScopeWithoutParent(scope), persistedScope);
            ValidateSingleNodeValues(node, persistedScope);
            nodes.Add(node);
            if (ReferenceEquals(scope, requestedScope))
            {
                resolvedScope = persistedScope;
                selectedNodeExists = true;
            }
        }

        return ResolveSettings(new LoadedHierarchy(resolvedScope, nodes, selectedNodeExists));
    }

    /// <summary>
    /// Loads the stored nodes of all levels of a requested chain with one query, keyed by scope type and object key.
    /// Throws if the database returns a node twice or a node that was not requested.
    /// </summary>
    private async Task<Dictionary<(ProvisioningScopeType ScopeType, string ObjectKey), ProvisioningConfigNodeData>> LoadNodesOfChainAsync(
        List<ProvisioningSettingsScope> chain)
    {
        List<ProvisioningConfigNodeData> matches = await apiConnection.SendQueryAsync<List<ProvisioningConfigNodeData>>(
            ProvisioningQueries.getNodesByNaturalKeys,
            new
            {
                globalKeys = ObjectKeysOf(chain, ProvisioningScopeType.Global),
                deviceTypeKeys = ObjectKeysOf(chain, ProvisioningScopeType.DeviceType),
                managementKeys = ObjectKeysOf(chain, ProvisioningScopeType.Management),
                gatewayKeys = ObjectKeysOf(chain, ProvisioningScopeType.Gateway)
            });

        HashSet<(ProvisioningScopeType ScopeType, string ObjectKey)> requested = [.. chain.Select(scope => (scope.ScopeType, scope.ObjectKey))];
        Dictionary<(ProvisioningScopeType ScopeType, string ObjectKey), ProvisioningConfigNodeData> nodes = [];
        foreach (ProvisioningConfigNodeData match in matches)
        {
            (ProvisioningScopeType ScopeType, string ObjectKey) naturalKey = (ProvisioningSettingsScopeFactory.ParseNodeType(match.NodeType), match.ObjectKey);
            if (!requested.Contains(naturalKey))
            {
                throw new InvalidOperationException(
                    $"Database returned provisioning node '{match.Id}' for scope '{naturalKey.ScopeType}:{naturalKey.ObjectKey}', which was not requested.");
            }

            if (!nodes.TryAdd(naturalKey, match))
            {
                throw new InvalidOperationException(
                    $"Database returned more than one node for provisioning scope '{naturalKey.ScopeType}:{naturalKey.ObjectKey}'.");
            }
        }

        return nodes;
    }

    private static List<string> ObjectKeysOf(List<ProvisioningSettingsScope> chain, ProvisioningScopeType scopeType)
    {
        return [.. chain.Where(scope => scope.ScopeType == scopeType).Select(scope => scope.ObjectKey)];
    }

    private async Task<LoadedHierarchy?> TryLoadPersistedHierarchyAsync(
        ProvisioningSettingsScope requestedScope)
    {
        ProvisioningConfigNodeData? match = await TryLoadNodeByNaturalKeyAsync(requestedScope);
        if (match is null)
        {
            return null;
        }

        IReadOnlyList<ProvisioningConfigNodeData> nodes = BuildNodeChain(match);
        IReadOnlyList<ProvisioningSettingsScope> scopes = CreateScopeChain(nodes);
        ProvisioningSettingsHierarchyValidator.ValidatePersistedChain(scopes, requestedScope.ScopeType);
        ProvisioningSettingsHierarchyValidator.ValidateRequestedScope(requestedScope, scopes[^1]);
        ValidateNodeValues(nodes, scopes);
        return new LoadedHierarchy(scopes[^1], nodes, SelectedNodeExists: true);
    }

    /// <summary>Loads the node of a scope by node type and object key, together with its stored ancestors.</summary>
    private async Task<ProvisioningConfigNodeData?> TryLoadNodeByNaturalKeyAsync(ProvisioningSettingsScope scope)
    {
        List<ProvisioningConfigNodeData> matches = await apiConnection.SendQueryAsync<List<ProvisioningConfigNodeData>>(
            ProvisioningQueries.getNodeWithAncestors,
            new
            {
                nodeType = ProvisioningSettingsScopeFactory.ToNodeType(scope.ScopeType),
                objectKey = scope.ObjectKey
            });

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Database returned {matches.Count} nodes for provisioning scope "
                + $"'{scope.ScopeType}:{scope.ObjectKey}'.");
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>A copy of a scope that does not constrain the stored parent, for matching a node that may have been moved.</summary>
    private static ProvisioningSettingsScope ScopeWithoutParent(ProvisioningSettingsScope scope)
    {
        ProvisioningSettingsScope copy = ProvisioningSettingsScopeFactory.Copy(scope);
        copy.ParentNodeId = null;
        return copy;
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

    private static ResolvedSettings ResolveSettings(LoadedHierarchy hierarchy)
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
                object value = ProvisioningSettingValueSerializer.Deserialize(key, storedValue.ConfigValue);
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

    private static List<ProvisioningConfigNodeData> BuildNodeChain(ProvisioningConfigNodeData leaf)
    {
        List<ProvisioningConfigNodeData> reversed = [];
        ProvisioningConfigNodeData? current = leaf;

        while (current is not null)
        {
            reversed.Add(current);
            if (reversed.Count > kMaxHierarchyDepth)
            {
                throw new InvalidOperationException($"Provisioning hierarchy contains more than {kMaxHierarchyDepth} levels.");
            }
            current = current.ParentNode;
        }

        reversed.Reverse();
        return reversed;
    }

    private static List<ProvisioningSettingsScope> CreateScopeChain(
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
            ValidateSingleNodeValues(nodes[index], scopes[index]);
        }
    }

    private static void ValidateSingleNodeValues(ProvisioningConfigNodeData node, ProvisioningSettingsScope scope)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (ProvisioningConfigValueData storedValue in node.Values)
        {
            ValidateStoredValue(node, scope, storedValue, keys);
        }
    }

    private static void ValidateStoredValue(
        ProvisioningConfigNodeData node,
        ProvisioningSettingsScope scope,
        ProvisioningConfigValueData storedValue,
        HashSet<string> seenKeys)
    {
        if (storedValue.NodeId != node.Id)
        {
            throw new InvalidOperationException(
                $"Provisioning value '{storedValue.ConfigKey}' references node '{storedValue.NodeId}' "
                + $"but was returned for node '{node.Id}'.");
        }

        if (!seenKeys.Add(storedValue.ConfigKey))
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
