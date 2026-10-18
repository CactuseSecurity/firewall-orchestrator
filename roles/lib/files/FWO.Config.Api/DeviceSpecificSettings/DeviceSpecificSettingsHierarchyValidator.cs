using System.Globalization;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Config.Api.DeviceSpecificSettings;

internal static class DeviceSpecificSettingsHierarchyValidator
{
    private static readonly DeviceSpecificScopeType[] OrderedScopeTypes =
    [
        DeviceSpecificScopeType.Global,
        DeviceSpecificScopeType.DeviceType,
        DeviceSpecificScopeType.Management,
        DeviceSpecificScopeType.Gateway
    ];

    public static void ValidateLocator(DeviceSpecificSettingsScope scope)
    {
        ValidateScope(scope, requireParent: false);
    }

    public static void ValidateNewNode(DeviceSpecificSettingsScope scope)
    {
        ValidateScope(scope, requireParent: scope.ScopeType != DeviceSpecificScopeType.Global);
        if (scope.NodeId != 0)
        {
            throw new ArgumentException("A new device specific settings scope cannot already have a node ID.", nameof(scope));
        }
    }

    public static void ValidatePersistedChain(
        IReadOnlyList<DeviceSpecificSettingsScope> chain,
        DeviceSpecificScopeType expectedLeafType)
    {
        ArgumentNullException.ThrowIfNull(chain);

        int expectedCount = GetLevelIndex(expectedLeafType) + 1;
        if (chain.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Device specific settings hierarchy for '{expectedLeafType}' must contain {expectedCount} node(s), but contains {chain.Count}.");
        }

        HashSet<long> nodeIds = [];
        for (int index = 0; index < chain.Count; index++)
        {
            DeviceSpecificSettingsScope scope = chain[index];
            ValidateScope(scope, requireParent: index > 0, invalidData: true);

            DeviceSpecificScopeType expectedType = OrderedScopeTypes[index];
            if (scope.ScopeType != expectedType)
            {
                throw new InvalidOperationException(
                    $"Device specific settings hierarchy position {index} must be '{expectedType}', but is '{scope.ScopeType}'.");
            }

            if (scope.NodeId <= 0)
            {
                throw new InvalidOperationException(
                    $"Persisted device specific settings scope '{scope.ScopeType}:{scope.ObjectKey}' has invalid node ID '{scope.NodeId}'.");
            }

            if (!nodeIds.Add(scope.NodeId))
            {
                throw new InvalidOperationException(
                    $"Device specific settings hierarchy contains node ID '{scope.NodeId}' more than once.");
            }

            if (index == 0)
            {
                if (scope.ParentNodeId is not null)
                {
                    throw new InvalidOperationException("The global device specific settings node cannot have a parent.");
                }
            }
            else if (scope.ParentNodeId != chain[index - 1].NodeId)
            {
                throw new InvalidOperationException(
                    $"Device specific settings node '{scope.NodeId}' does not reference the preceding hierarchy node '{chain[index - 1].NodeId}'.");
            }
        }
    }

    /// <summary>
    /// Validates a requested chain of scopes that runs from Global down to the scope to resolve,
    /// one scope per level and without gaps. Persistence and stored parent links are not checked.
    /// </summary>
    public static void ValidateRequestedChain(IReadOnlyList<DeviceSpecificSettingsScope> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (chain.Count == 0 || chain.Count > OrderedScopeTypes.Length)
        {
            throw new ArgumentException(
                $"A device specific settings scope chain must contain between 1 and {OrderedScopeTypes.Length} scopes, but contains {chain.Count}.",
                nameof(chain));
        }

        for (int index = 0; index < chain.Count; index++)
        {
            DeviceSpecificSettingsScope scope = chain[index]
                ?? throw new ArgumentException($"Device specific settings scope chain position {index} is empty.", nameof(chain));
            ValidateLocator(scope);

            if (scope.ScopeType != OrderedScopeTypes[index])
            {
                throw new ArgumentException(
                    $"Device specific settings scope chain position {index} must be '{OrderedScopeTypes[index]}', but is '{scope.ScopeType}'.",
                    nameof(chain));
            }
        }
    }

    public static void ValidateRequestedScope(
        DeviceSpecificSettingsScope requested,
        DeviceSpecificSettingsScope persisted)
    {
        if (requested.ScopeType != persisted.ScopeType
            || !string.Equals(requested.ObjectKey, persisted.ObjectKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The database returned device specific settings scope '{persisted.ScopeType}:{persisted.ObjectKey}' "
                + $"for request '{requested.ScopeType}:{requested.ObjectKey}'.");
        }

        if (requested.NodeId != 0 && requested.NodeId != persisted.NodeId)
        {
            throw new InvalidOperationException(
                $"Device specific settings scope '{requested.ScopeType}:{requested.ObjectKey}' expected node ID '{requested.NodeId}', "
                + $"but the database returned '{persisted.NodeId}'.");
        }

        if (requested.ParentNodeId.HasValue && requested.ParentNodeId != persisted.ParentNodeId)
        {
            throw new InvalidOperationException(
                $"Device specific settings scope '{requested.ScopeType}:{requested.ObjectKey}' expected parent node ID "
                + $"'{requested.ParentNodeId}', but the database returned '{persisted.ParentNodeId}'.");
        }
    }

    public static void ValidateChild(
        IReadOnlyList<DeviceSpecificSettingsScope> parentChain,
        DeviceSpecificSettingsScope child)
    {
        if (parentChain.Count == 0)
        {
            throw new InvalidOperationException("A child scope requires a persisted parent hierarchy.");
        }

        DeviceSpecificSettingsScope parent = parentChain[^1];
        DeviceSpecificScopeType expectedChildType = GetChildType(parent.ScopeType)
            ?? throw new InvalidOperationException("Gateway device specific settings nodes cannot have child nodes.");

        ValidateScope(child, requireParent: true, invalidData: true);
        if (child.NodeId <= 0)
        {
            throw new InvalidOperationException(
                $"Persisted child scope '{child.ScopeType}:{child.ObjectKey}' has invalid node ID '{child.NodeId}'.");
        }

        if (child.ScopeType != expectedChildType)
        {
            throw new InvalidOperationException(
                $"Device specific settings node '{parent.NodeId}' of type '{parent.ScopeType}' can only have '{expectedChildType}' children, "
                + $"but child '{child.NodeId}' is '{child.ScopeType}'.");
        }

        if (child.ParentNodeId != parent.NodeId)
        {
            throw new InvalidOperationException(
                $"Device specific settings child node '{child.NodeId}' does not reference requested parent node '{parent.NodeId}'.");
        }
    }

    public static void ValidateNewChild(
        IReadOnlyList<DeviceSpecificSettingsScope> parentChain,
        DeviceSpecificSettingsScope child)
    {
        if (parentChain.Count == 0)
        {
            throw new InvalidOperationException("A new child scope requires a persisted parent hierarchy.");
        }

        ValidateNewNode(child);

        DeviceSpecificSettingsScope parent = parentChain[^1];
        DeviceSpecificScopeType expectedChildType = GetChildType(parent.ScopeType)
            ?? throw new ArgumentException("Gateway device specific settings nodes cannot have child nodes.", nameof(child));

        if (child.ScopeType != expectedChildType)
        {
            throw new ArgumentException(
                $"Device specific settings node '{parent.NodeId}' of type '{parent.ScopeType}' can only have '{expectedChildType}' children, "
                + $"not '{child.ScopeType}'.",
                nameof(child));
        }

        if (child.ParentNodeId != parent.NodeId)
        {
            throw new ArgumentException(
                $"New device specific settings scope '{child.ScopeType}:{child.ObjectKey}' must reference parent node '{parent.NodeId}'.",
                nameof(child));
        }
    }

    public static void ValidateSettingsType(DeviceSpecificScopeType scopeType, Type settingsType)
    {
        Type expectedType = scopeType switch
        {
            DeviceSpecificScopeType.Global => typeof(GlobalDeviceSpecificSettings),
            DeviceSpecificScopeType.DeviceType => typeof(DeviceTypeDeviceSpecificSettings),
            DeviceSpecificScopeType.Management => typeof(ManagementDeviceSpecificSettings),
            DeviceSpecificScopeType.Gateway => typeof(GatewayDeviceSpecificSettings),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined device specific settings scope type is required.")
        };

        if (settingsType != expectedType)
        {
            throw new ArgumentException(
                $"Scope type '{scopeType}' must be loaded as '{expectedType.Name}', not '{settingsType.Name}'.",
                nameof(settingsType));
        }
    }

    /// <summary>
    /// Validates a complete set of persisted nodes in memory: node IDs and natural keys are unique, and every node
    /// references an unbroken chain of parents of the levels above it up to the global node. Returns the nodes ordered
    /// by level, Global first, keeping the given order within a level.
    /// </summary>
    public static List<DeviceSpecificSettingsScope> ValidateTree(IReadOnlyList<DeviceSpecificSettingsScope> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        Dictionary<long, DeviceSpecificSettingsScope> scopesById = [];
        HashSet<(DeviceSpecificScopeType ScopeType, string ObjectKey)> naturalKeys = [];
        foreach (DeviceSpecificSettingsScope scope in scopes)
        {
            if (!scopesById.TryAdd(scope.NodeId, scope))
            {
                throw new InvalidOperationException($"Device specific settings nodes contain node ID '{scope.NodeId}' more than once.");
            }

            if (!naturalKeys.Add((scope.ScopeType, scope.ObjectKey)))
            {
                throw new InvalidOperationException(
                    $"Device specific settings nodes contain scope '{scope.ScopeType}:{scope.ObjectKey}' more than once.");
            }
        }

        foreach (DeviceSpecificSettingsScope scope in scopes)
        {
            ValidatePersistedChain(BuildChain(scope, scopesById), scope.ScopeType);
        }

        return [.. scopes.OrderBy(scope => GetLevelIndex(scope.ScopeType))];
    }

    public static DeviceSpecificScopeType? GetParentType(DeviceSpecificScopeType scopeType)
    {
        return scopeType switch
        {
            DeviceSpecificScopeType.Global => null,
            DeviceSpecificScopeType.DeviceType => DeviceSpecificScopeType.Global,
            DeviceSpecificScopeType.Management => DeviceSpecificScopeType.DeviceType,
            DeviceSpecificScopeType.Gateway => DeviceSpecificScopeType.Management,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined device specific settings scope type is required.")
        };
    }

    private static DeviceSpecificScopeType? GetChildType(DeviceSpecificScopeType scopeType)
    {
        return scopeType switch
        {
            DeviceSpecificScopeType.Global => DeviceSpecificScopeType.DeviceType,
            DeviceSpecificScopeType.DeviceType => DeviceSpecificScopeType.Management,
            DeviceSpecificScopeType.Management => DeviceSpecificScopeType.Gateway,
            DeviceSpecificScopeType.Gateway => null,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined device specific settings scope type is required.")
        };
    }

    /// <summary>
    /// Follows the parent links of a node, Global first. Stops one level beyond the deepest possible chain, so that a
    /// cycle ends in a chain of the wrong length instead of an endless loop.
    /// </summary>
    private static List<DeviceSpecificSettingsScope> BuildChain(
        DeviceSpecificSettingsScope leaf,
        Dictionary<long, DeviceSpecificSettingsScope> scopesById)
    {
        List<DeviceSpecificSettingsScope> chain = [leaf];
        DeviceSpecificSettingsScope current = leaf;
        while (current.ParentNodeId is long parentId && chain.Count <= OrderedScopeTypes.Length)
        {
            if (!scopesById.TryGetValue(parentId, out DeviceSpecificSettingsScope? parent))
            {
                throw new InvalidOperationException(
                    $"Device specific settings node '{current.NodeId}' references parent node '{parentId}', which does not exist.");
            }

            chain.Add(parent);
            current = parent;
        }

        chain.Reverse();
        return chain;
    }

    private static int GetLevelIndex(DeviceSpecificScopeType scopeType)
    {
        int index = Array.IndexOf(OrderedScopeTypes, scopeType);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined device specific settings scope type is required.");
        }
        return index;
    }

    private static void ValidateScope(
        DeviceSpecificSettingsScope scope,
        bool requireParent,
        bool invalidData = false)
    {
        ArgumentNullException.ThrowIfNull(scope);

        string? error = GetScopeError(scope, requireParent);
        if (error is null)
        {
            return;
        }

        if (invalidData)
        {
            throw new InvalidOperationException(error);
        }

        throw new ArgumentException(error, nameof(scope));
    }

    private static string? GetScopeError(DeviceSpecificSettingsScope scope, bool requireParent)
    {
        if (!Enum.IsDefined(scope.ScopeType) || scope.ScopeType == DeviceSpecificScopeType.Undefined)
        {
            return $"Device specific settings scope type '{scope.ScopeType}' is not defined.";
        }

        if (string.IsNullOrWhiteSpace(scope.ObjectKey))
        {
            return "A device specific settings scope requires a non-empty object key.";
        }

        if (!string.Equals(scope.ObjectKey, scope.ObjectKey.Trim(), StringComparison.Ordinal))
        {
            return "A device specific settings scope object key cannot have leading or trailing whitespace.";
        }

        if (scope.NodeId < 0)
        {
            return $"Device specific settings node ID '{scope.NodeId}' cannot be negative.";
        }

        if (scope.ParentNodeId <= 0)
        {
            return $"Device specific settings parent node ID '{scope.ParentNodeId}' must be positive.";
        }

        return GetParentError(scope, requireParent) ?? GetObjectKeyFormatError(scope);
    }

    private static string? GetParentError(DeviceSpecificSettingsScope scope, bool requireParent)
    {
        if (scope.ScopeType == DeviceSpecificScopeType.Global)
        {
            if (!string.Equals(scope.ObjectKey, DeviceSpecificSettingsScopeFactory.GlobalNodeType, StringComparison.Ordinal))
            {
                return $"The global device specific settings scope must use object key '{DeviceSpecificSettingsScopeFactory.GlobalNodeType}'.";
            }

            if (scope.ParentNodeId is not null)
            {
                return "The global device specific settings scope cannot have a parent.";
            }
        }
        else if (requireParent && scope.ParentNodeId is null)
        {
            return $"Device specific settings scope type '{scope.ScopeType}' requires a parent node ID.";
        }

        return null;
    }

    private static string? GetObjectKeyFormatError(DeviceSpecificSettingsScope scope)
    {
        if (scope.ScopeType is DeviceSpecificScopeType.Management or DeviceSpecificScopeType.Gateway
            && (!long.TryParse(scope.ObjectKey, NumberStyles.None, CultureInfo.InvariantCulture, out long objectId)
                || objectId <= 0
                || !string.Equals(objectId.ToString(CultureInfo.InvariantCulture), scope.ObjectKey, StringComparison.Ordinal)))
        {
            return $"Device specific settings scope type '{scope.ScopeType}' requires a positive numeric object key.";
        }

        return null;
    }
}
