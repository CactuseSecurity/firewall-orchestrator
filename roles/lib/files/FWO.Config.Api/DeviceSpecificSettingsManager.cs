using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;

namespace FWO.Config.Api;

/// <summary>
/// Loads and persists explicitly selected overrides in the hierarchical device specific settings configuration.
/// </summary>
public sealed class DeviceSpecificSettingsManager
{
    /// <summary>Global, device type, management and gateway.</summary>
    private const int kMaxHierarchyDepth = 4;

    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Creates a manager that reads and writes the device specific settings configuration through the given API connection.
    /// </summary>
    public DeviceSpecificSettingsManager(ApiConnection apiConnection)
    {
        ArgumentNullException.ThrowIfNull(apiConnection);

        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Resolves the settings in effect at the most specific level of a path, together with the overrides stored
    /// directly on that level and the source of every value. Values are inherited along the path, i.e. along the
    /// current device hierarchy, and never along the stored parent links of the device specific settings nodes, which are
    /// outdated for a management or gateway moved since its node was stored. Levels without a stored node
    /// contribute no values.
    /// </summary>
    public async Task<DeviceSpecificSettingsLevel<TSettings>> LoadLevelAsync<TSettings>(DeviceSpecificScopePath path)
        where TSettings : GlobalDeviceSpecificSettings
    {
        ArgumentNullException.ThrowIfNull(path);
        DeviceSpecificSettingsHierarchyValidator.ValidateSettingsType(path.ScopeType, typeof(TSettings));

        ResolvedSettings resolved = await ResolveAlongPathAsync(path);
        return new DeviceSpecificSettingsLevel<TSettings>(
            resolved.Scope,
            (TSettings)resolved.Settings,
            resolved.DirectOverrides,
            resolved.ValueSources);
    }

    /// <summary>
    /// Resolves the value of a single setting in effect at the most specific level of a path and the level it
    /// comes from. Like <see cref="LoadLevelAsync{TSettings}"/>, the value is inherited along the path.
    /// </summary>
    public async Task<ResolvedDeviceSpecificValue<TValue>> LoadEffectiveValueAsync<TValue>(
        DeviceSpecificScopePath path,
        DeviceSpecificSettingKey<TValue> key)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(key);
        ValidateCanonicalKey(key);

        ResolvedSettings resolved = await ResolveAlongPathAsync(path);
        TValue value = (TValue)DeviceSpecificSettingsMapper.GetValue(resolved.Settings, key);
        return new ResolvedDeviceSpecificValue<TValue>(key, value, resolved.ValueSources[key]);
    }

    /// <summary>
    /// Stores an override of a single setting at a scope, creating the scope's node if needed.
    /// </summary>
    public Task<DeviceSpecificSettingsScope> SetOverrideAsync<TValue>(
        DeviceSpecificSettingsScope scope,
        DeviceSpecificSettingKey<TValue> key,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        DeviceSpecificSettingsChangeSet changes = new DeviceSpecificSettingsChangeSet(scope).Set(key, value);
        return ApplyChangesAsync(changes);
    }

    /// <summary>
    /// Removes the override of a single setting from a scope, so that the scope inherits the value again.
    /// Does nothing if the scope has no persisted node.
    /// </summary>
    public async Task ClearOverrideAsync(
        DeviceSpecificSettingsScope scope,
        DeviceSpecificSettingKey key)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(key);
        DeviceSpecificSettingsHierarchyValidator.ValidateLocator(scope);
        ValidateKeyForScope(key, scope.ScopeType);

        LoadedHierarchy? hierarchy = await TryLoadPersistedHierarchyAsync(scope);
        if (hierarchy is null)
        {
            return;
        }

        await apiConnection.SendQueryAsync<ReturnId>(
            DeviceSpecificSettingsQueries.deleteOverrides,
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
    public async Task<DeviceSpecificSettingsScope> ApplyChangesAsync(
        DeviceSpecificSettingsChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.IsEmpty)
        {
            return DeviceSpecificSettingsScopeFactory.Copy(changes.Scope);
        }

        DeviceSpecificSettingsHierarchyValidator.ValidateLocator(changes.Scope);

        List<SerializedOverride> serializedUpserts = [];
        foreach (DeviceSpecificSettingOverride upsert in changes.Upserts)
        {
            ValidateKeyForScope(upsert.Key, changes.Scope.ScopeType);
            serializedUpserts.Add(new SerializedOverride(
                upsert.Key,
                DeviceSpecificSettingValueSerializer.Serialize(upsert.Key, upsert.Value)));
        }

        foreach (DeviceSpecificSettingKey removal in changes.Removals)
        {
            ValidateKeyForScope(removal, changes.Scope.ScopeType);
        }

        LoadedHierarchy? persistedHierarchy = await TryLoadPersistedHierarchyAsync(changes.Scope);
        if (persistedHierarchy is null && serializedUpserts.Count == 0)
        {
            return DeviceSpecificSettingsScopeFactory.Copy(changes.Scope);
        }

        LoadedHierarchy hierarchy = persistedHierarchy ?? await LoadUnpersistedHierarchyAsync(changes.Scope);

        DeviceSpecificSettingsScope nodeToUpsert = CreateNodeForUpsert(changes.Scope, hierarchy);
        DeviceSpecificSettingsScope persistedScope = await UpsertNodeAsync(nodeToUpsert);

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
                DeviceSpecificSettingsQueries.applyPatch,
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
                DeviceSpecificSettingsQueries.deleteOverrides,
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
    public async Task<DeviceSpecificSettingsScope> EnsureNodeAsync(DeviceSpecificSettingsScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        DeviceSpecificSettingsHierarchyValidator.ValidateLocator(scope);

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
    public async Task<DeviceSpecificSettingsScope> MoveNodeAsync(DeviceSpecificSettingsScope scope, long newParentNodeId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        DeviceSpecificSettingsHierarchyValidator.ValidateLocator(scope);
        DeviceSpecificScopeType expectedParentType = DeviceSpecificSettingsHierarchyValidator.GetParentType(scope.ScopeType)
            ?? throw new ArgumentException("The global device specific settings node cannot be moved.", nameof(scope));

        DeviceSpecificConfigNodeData node = await TryLoadNodeByNaturalKeyAsync(scope)
            ?? throw new KeyNotFoundException($"Device specific settings scope '{scope.ScopeType}:{scope.ObjectKey}' has no persisted node.");
        DeviceSpecificSettingsScope currentScope = DeviceSpecificSettingsScopeFactory.Create(node);
        DeviceSpecificSettingsHierarchyValidator.ValidateRequestedScope(ScopeWithoutParent(scope), currentScope);

        IReadOnlyList<DeviceSpecificConfigNodeData> parentNodes = BuildNodeChain(await LoadNodeByIdAsync(newParentNodeId));
        IReadOnlyList<DeviceSpecificSettingsScope> parentScopes = CreateScopeChain(parentNodes);
        DeviceSpecificSettingsHierarchyValidator.ValidatePersistedChain(parentScopes, expectedParentType);

        if (currentScope.ParentNodeId == newParentNodeId)
        {
            return currentScope;
        }

        return await UpsertNodeAsync(new DeviceSpecificSettingsScope
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
    public async Task<IReadOnlyList<DeviceSpecificSettingsScope>> GetAllNodesAsync()
    {
        List<DeviceSpecificConfigNodeData> nodes =
            await apiConnection.SendQueryAsync<List<DeviceSpecificConfigNodeData>>(DeviceSpecificSettingsQueries.getAllNodes);
        return DeviceSpecificSettingsHierarchyValidator.ValidateTree(CreateScopeChain(nodes));
    }

    /// <summary>
    /// Merges the values stored on the levels of a path, Global first, looking every level's node up by its
    /// scope type and object key.
    /// </summary>
    private async Task<ResolvedSettings> ResolveAlongPathAsync(DeviceSpecificScopePath path)
    {
        List<DeviceSpecificSettingsScope> chain = path.ToScopes();
        DeviceSpecificSettingsHierarchyValidator.ValidateRequestedChain(chain);
        Dictionary<(DeviceSpecificScopeType ScopeType, string ObjectKey), DeviceSpecificConfigNodeData> storedNodes = await LoadNodesOfChainAsync(chain);
        return ResolveChain(chain, storedNodes);
    }

    /// <summary>
    /// Resolves the settings in effect at the most specific level of every given path, inherited like in
    /// <see cref="LoadLevelAsync{TSettings}"/>, but loads the stored nodes of all paths with a single query, so the
    /// cost does not grow with the number of paths. The result is in the order of the paths.
    /// </summary>
    /// <param name="paths">Paths to resolve; levels shared by several paths, e.g. Global, are loaded once.</param>
    public async Task<List<GlobalDeviceSpecificSettings>> LoadEffectiveSettingsAsync(IReadOnlyList<DeviceSpecificScopePath> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return [];
        }

        List<List<DeviceSpecificSettingsScope>> chains = [.. paths.Select(path => path.ToScopes())];
        foreach (List<DeviceSpecificSettingsScope> chain in chains)
        {
            DeviceSpecificSettingsHierarchyValidator.ValidateRequestedChain(chain);
        }

        List<DeviceSpecificSettingsScope> distinctScopes = [.. chains.SelectMany(chain => chain).DistinctBy(scope => (scope.ScopeType, scope.ObjectKey))];
        Dictionary<(DeviceSpecificScopeType ScopeType, string ObjectKey), DeviceSpecificConfigNodeData> storedNodes = await LoadNodesOfChainAsync(distinctScopes);
        return [.. chains.Select(chain => ResolveChain(chain, storedNodes).Settings)];
    }

    /// <summary>
    /// Merges the values of the stored nodes of a requested chain, Global first. Levels without a stored node
    /// contribute no values.
    /// </summary>
    private static ResolvedSettings ResolveChain(List<DeviceSpecificSettingsScope> chain,
        Dictionary<(DeviceSpecificScopeType ScopeType, string ObjectKey), DeviceSpecificConfigNodeData> storedNodes)
    {
        DeviceSpecificSettingsScope requestedScope = chain[^1];
        List<DeviceSpecificConfigNodeData> nodes = [];
        DeviceSpecificSettingsScope resolvedScope = DeviceSpecificSettingsScopeFactory.Copy(requestedScope);
        bool selectedNodeExists = false;
        foreach (DeviceSpecificSettingsScope scope in chain)
        {
            if (!storedNodes.TryGetValue((scope.ScopeType, scope.ObjectKey), out DeviceSpecificConfigNodeData? node))
            {
                continue;
            }

            DeviceSpecificSettingsScope persistedScope = DeviceSpecificSettingsScopeFactory.Create(node);
            DeviceSpecificSettingsHierarchyValidator.ValidateRequestedScope(ScopeWithoutParent(scope), persistedScope);
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
    private async Task<Dictionary<(DeviceSpecificScopeType ScopeType, string ObjectKey), DeviceSpecificConfigNodeData>> LoadNodesOfChainAsync(
        List<DeviceSpecificSettingsScope> chain)
    {
        List<DeviceSpecificConfigNodeData> matches = await apiConnection.SendQueryAsync<List<DeviceSpecificConfigNodeData>>(
            DeviceSpecificSettingsQueries.getNodesByNaturalKeys,
            new
            {
                globalKeys = ObjectKeysOf(chain, DeviceSpecificScopeType.Global),
                deviceTypeKeys = ObjectKeysOf(chain, DeviceSpecificScopeType.DeviceType),
                managementKeys = ObjectKeysOf(chain, DeviceSpecificScopeType.Management),
                gatewayKeys = ObjectKeysOf(chain, DeviceSpecificScopeType.Gateway)
            });

        HashSet<(DeviceSpecificScopeType ScopeType, string ObjectKey)> requested = [.. chain.Select(scope => (scope.ScopeType, scope.ObjectKey))];
        Dictionary<(DeviceSpecificScopeType ScopeType, string ObjectKey), DeviceSpecificConfigNodeData> nodes = [];
        foreach (DeviceSpecificConfigNodeData match in matches)
        {
            (DeviceSpecificScopeType ScopeType, string ObjectKey) naturalKey = (DeviceSpecificSettingsScopeFactory.ParseNodeType(match.NodeType), match.ObjectKey);
            if (!requested.Contains(naturalKey))
            {
                throw new InvalidOperationException(
                    $"Database returned device specific settings node '{match.Id}' for scope '{naturalKey.ScopeType}:{naturalKey.ObjectKey}', which was not requested.");
            }

            if (!nodes.TryAdd(naturalKey, match))
            {
                throw new InvalidOperationException(
                    $"Database returned more than one node for device specific settings scope '{naturalKey.ScopeType}:{naturalKey.ObjectKey}'.");
            }
        }

        return nodes;
    }

    private static List<string> ObjectKeysOf(List<DeviceSpecificSettingsScope> chain, DeviceSpecificScopeType scopeType)
    {
        return [.. chain.Where(scope => scope.ScopeType == scopeType).Select(scope => scope.ObjectKey)];
    }

    private async Task<LoadedHierarchy?> TryLoadPersistedHierarchyAsync(
        DeviceSpecificSettingsScope requestedScope)
    {
        DeviceSpecificConfigNodeData? match = await TryLoadNodeByNaturalKeyAsync(requestedScope);
        if (match is null)
        {
            return null;
        }

        IReadOnlyList<DeviceSpecificConfigNodeData> nodes = BuildNodeChain(match);
        IReadOnlyList<DeviceSpecificSettingsScope> scopes = CreateScopeChain(nodes);
        DeviceSpecificSettingsHierarchyValidator.ValidatePersistedChain(scopes, requestedScope.ScopeType);
        DeviceSpecificSettingsHierarchyValidator.ValidateRequestedScope(requestedScope, scopes[^1]);
        ValidateNodeValues(nodes, scopes);
        return new LoadedHierarchy(scopes[^1], nodes, SelectedNodeExists: true);
    }

    /// <summary>Loads the node of a scope by node type and object key, together with its stored ancestors.</summary>
    private async Task<DeviceSpecificConfigNodeData?> TryLoadNodeByNaturalKeyAsync(DeviceSpecificSettingsScope scope)
    {
        List<DeviceSpecificConfigNodeData> matches = await apiConnection.SendQueryAsync<List<DeviceSpecificConfigNodeData>>(
            DeviceSpecificSettingsQueries.getNodeWithAncestors,
            new
            {
                nodeType = DeviceSpecificSettingsScopeFactory.ToNodeType(scope.ScopeType),
                objectKey = scope.ObjectKey
            });

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Database returned {matches.Count} nodes for device specific settings scope "
                + $"'{scope.ScopeType}:{scope.ObjectKey}'.");
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>A copy of a scope that does not constrain the stored parent, for matching a node that may have been moved.</summary>
    private static DeviceSpecificSettingsScope ScopeWithoutParent(DeviceSpecificSettingsScope scope)
    {
        DeviceSpecificSettingsScope copy = DeviceSpecificSettingsScopeFactory.Copy(scope);
        copy.ParentNodeId = null;
        return copy;
    }

    private async Task<LoadedHierarchy> LoadUnpersistedHierarchyAsync(
        DeviceSpecificSettingsScope requestedScope)
    {
        if (requestedScope.NodeId != 0)
        {
            throw new KeyNotFoundException(
                $"Device specific settings node '{requestedScope.NodeId}' for scope "
                + $"'{requestedScope.ScopeType}:{requestedScope.ObjectKey}' does not exist.");
        }

        DeviceSpecificSettingsScope unpersistedScope = DeviceSpecificSettingsScopeFactory.Copy(requestedScope);
        if (requestedScope.ScopeType == DeviceSpecificScopeType.Global)
        {
            DeviceSpecificSettingsHierarchyValidator.ValidateNewNode(unpersistedScope);
            return new LoadedHierarchy(unpersistedScope, [], SelectedNodeExists: false);
        }

        if (requestedScope.ParentNodeId is null)
        {
            throw new ArgumentException(
                $"Unpersisted device specific settings scope '{requestedScope.ScopeType}:{requestedScope.ObjectKey}' requires a parent node ID.",
                nameof(requestedScope));
        }

        DeviceSpecificConfigNodeData parentNode = await LoadNodeByIdAsync(requestedScope.ParentNodeId.Value);
        IReadOnlyList<DeviceSpecificConfigNodeData> parentNodes = BuildNodeChain(parentNode);
        IReadOnlyList<DeviceSpecificSettingsScope> parentScopes = CreateScopeChain(parentNodes);
        DeviceSpecificScopeType expectedParentType = DeviceSpecificSettingsHierarchyValidator.GetParentType(requestedScope.ScopeType)
            ?? throw new InvalidOperationException("Only non-global scopes can reach parent validation.");

        DeviceSpecificSettingsHierarchyValidator.ValidatePersistedChain(parentScopes, expectedParentType);
        DeviceSpecificSettingsHierarchyValidator.ValidateNewChild(parentScopes, unpersistedScope);
        ValidateNodeValues(parentNodes, parentScopes);

        return new LoadedHierarchy(unpersistedScope, parentNodes, SelectedNodeExists: false);
    }

    private async Task<DeviceSpecificConfigNodeData> LoadNodeByIdAsync(long nodeId)
    {
        List<DeviceSpecificConfigNodeData> matches = await apiConnection.SendQueryAsync<List<DeviceSpecificConfigNodeData>>(
            DeviceSpecificSettingsQueries.getNodeById,
            new { nodeId });

        if (matches.Count == 0)
        {
            throw new KeyNotFoundException($"Device specific settings node '{nodeId}' does not exist.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Database returned {matches.Count} device specific settings nodes for ID '{nodeId}'.");
        }

        if (matches[0].Id != nodeId)
        {
            throw new InvalidOperationException(
                $"Database returned device specific settings node '{matches[0].Id}' for requested ID '{nodeId}'.");
        }

        return matches[0];
    }

    private static ResolvedSettings ResolveSettings(LoadedHierarchy hierarchy)
    {
        GlobalDeviceSpecificSettings settings = DeviceSpecificSettingsMapper.CreateDefaults(hierarchy.Scope.ScopeType);
        settings.Scope = hierarchy.Scope;

        Dictionary<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource> sources = DeviceSpecificSettingKeys.All
            .ToDictionary(key => key, _ => new DeviceSpecificSettingValueSource(null));

        HashSet<DeviceSpecificSettingKey> directOverrides = [];

        foreach (DeviceSpecificConfigNodeData node in hierarchy.Nodes)
        {
            DeviceSpecificSettingsScope sourceScope = DeviceSpecificSettingsScopeFactory.Create(node);
            foreach (DeviceSpecificConfigValueData storedValue in node.Values)
            {
                DeviceSpecificSettingKey key = DeviceSpecificSettingKeys.ByDatabaseKey[storedValue.ConfigKey];
                object value = DeviceSpecificSettingValueSerializer.Deserialize(key, storedValue.ConfigValue);
                DeviceSpecificSettingsMapper.SetValue(settings, key, value);
                sources[key] = new DeviceSpecificSettingValueSource(sourceScope);

                if (hierarchy.SelectedNodeExists && node.Id == hierarchy.Scope.NodeId)
                {
                    directOverrides.Add(key);
                }
            }
        }

        return new ResolvedSettings(hierarchy.Scope, settings, directOverrides, sources);
    }

    private async Task<DeviceSpecificSettingsScope> UpsertNodeAsync(DeviceSpecificSettingsScope scope)
    {
        DeviceSpecificConfigNodeData result = await apiConnection.SendQueryAsync<DeviceSpecificConfigNodeData>(
            DeviceSpecificSettingsQueries.upsertNode,
            new
            {
                node = new
                {
                    node_type = DeviceSpecificSettingsScopeFactory.ToNodeType(scope.ScopeType),
                    object_key = scope.ObjectKey,
                    parent_id = scope.ParentNodeId,
                    display_name = scope.DisplayName ?? "",
                    sort_order = scope.SortOrder
                }
            });

        DeviceSpecificSettingsScope persisted = DeviceSpecificSettingsScopeFactory.Create(result);
        if (persisted.NodeId <= 0)
        {
            throw new InvalidOperationException("Node upsert returned an invalid device specific settings node ID.");
        }

        DeviceSpecificSettingsHierarchyValidator.ValidateRequestedScope(scope, persisted);
        return persisted;
    }

    private static DeviceSpecificSettingsScope CreateNodeForUpsert(
        DeviceSpecificSettingsScope requested,
        LoadedHierarchy hierarchy)
    {
        return new DeviceSpecificSettingsScope
        {
            ScopeType = requested.ScopeType,
            ObjectKey = requested.ObjectKey,
            DisplayName = requested.DisplayName ?? hierarchy.Scope.DisplayName ?? "",
            NodeId = hierarchy.Scope.NodeId,
            ParentNodeId = hierarchy.Scope.ParentNodeId,
            SortOrder = requested.SortOrder
        };
    }

    private static List<DeviceSpecificConfigNodeData> BuildNodeChain(DeviceSpecificConfigNodeData leaf)
    {
        List<DeviceSpecificConfigNodeData> reversed = [];
        DeviceSpecificConfigNodeData? current = leaf;

        while (current is not null)
        {
            reversed.Add(current);
            if (reversed.Count > kMaxHierarchyDepth)
            {
                throw new InvalidOperationException($"Device specific settings hierarchy contains more than {kMaxHierarchyDepth} levels.");
            }
            current = current.ParentNode;
        }

        reversed.Reverse();
        return reversed;
    }

    private static List<DeviceSpecificSettingsScope> CreateScopeChain(
        IReadOnlyList<DeviceSpecificConfigNodeData> nodes)
    {
        return nodes.Select(DeviceSpecificSettingsScopeFactory.Create).ToList();
    }

    private static void ValidateNodeValues(
        IReadOnlyList<DeviceSpecificConfigNodeData> nodes,
        IReadOnlyList<DeviceSpecificSettingsScope> scopes)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            ValidateSingleNodeValues(nodes[index], scopes[index]);
        }
    }

    private static void ValidateSingleNodeValues(DeviceSpecificConfigNodeData node, DeviceSpecificSettingsScope scope)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (DeviceSpecificConfigValueData storedValue in node.Values)
        {
            ValidateStoredValue(node, scope, storedValue, keys);
        }
    }

    private static void ValidateStoredValue(
        DeviceSpecificConfigNodeData node,
        DeviceSpecificSettingsScope scope,
        DeviceSpecificConfigValueData storedValue,
        HashSet<string> seenKeys)
    {
        if (storedValue.NodeId != node.Id)
        {
            throw new InvalidOperationException(
                $"Device specific setting value '{storedValue.ConfigKey}' references node '{storedValue.NodeId}' "
                + $"but was returned for node '{node.Id}'.");
        }

        if (!seenKeys.Add(storedValue.ConfigKey))
        {
            throw new InvalidOperationException(
                $"Device specific settings node '{node.Id}' contains duplicate setting key '{storedValue.ConfigKey}'.");
        }

        if (!DeviceSpecificSettingKeys.ByDatabaseKey.TryGetValue(storedValue.ConfigKey, out DeviceSpecificSettingKey? key))
        {
            throw new InvalidOperationException(
                $"Device specific settings node '{node.Id}' contains unknown setting key '{storedValue.ConfigKey}'.");
        }

        if (!key.IsAllowedAt(scope.ScopeType))
        {
            throw new InvalidOperationException(
                $"Device specific setting '{key.DatabaseKey}' is not valid at scope type '{scope.ScopeType}'.");
        }

        if (storedValue.ConfigValue is null)
        {
            throw new InvalidOperationException(
                $"Device specific setting '{key.DatabaseKey}' on node '{node.Id}' has no JSON value.");
        }
    }

    private static void ValidateKeyForScope(DeviceSpecificSettingKey key, DeviceSpecificScopeType scopeType)
    {
        if (!key.IsAllowedAt(scopeType))
        {
            throw new ArgumentException(
                $"Setting '{key.DatabaseKey}' cannot be overridden at scope '{scopeType}'.",
                nameof(key));
        }

        ValidateCanonicalKey(key);
    }

    /// <summary>Rejects keys outside the shared typed catalog.</summary>
    private static void ValidateCanonicalKey(DeviceSpecificSettingKey key)
    {
        if (!DeviceSpecificSettingKeys.ByDatabaseKey.TryGetValue(key.DatabaseKey, out DeviceSpecificSettingKey? knownKey)
            || !ReferenceEquals(knownKey, key))
        {
            throw new ArgumentException(
                $"Setting key '{key.DatabaseKey}' is not part of the canonical device specific setting catalog.",
                nameof(key));
        }
    }

    private sealed record LoadedHierarchy(
        DeviceSpecificSettingsScope Scope,
        IReadOnlyList<DeviceSpecificConfigNodeData> Nodes,
        bool SelectedNodeExists);

    private sealed record ResolvedSettings(
        DeviceSpecificSettingsScope Scope,
        GlobalDeviceSpecificSettings Settings,
        IReadOnlySet<DeviceSpecificSettingKey> DirectOverrides,
        IReadOnlyDictionary<DeviceSpecificSettingKey, DeviceSpecificSettingValueSource> ValueSources);

    private sealed record SerializedOverride(DeviceSpecificSettingKey Key, JToken Value);
}
