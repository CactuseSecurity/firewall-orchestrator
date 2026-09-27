using System.Globalization;
using FWO.Data.Provisioning;

namespace FWO.Config.Api.Provisioning;

internal static class ProvisioningSettingsHierarchyValidator
{
    private static readonly ProvisioningScopeType[] OrderedScopeTypes =
    [
        ProvisioningScopeType.Global,
        ProvisioningScopeType.DeviceType,
        ProvisioningScopeType.Management,
        ProvisioningScopeType.Gateway
    ];

    public static void ValidateLocator(ProvisioningSettingsScope scope)
    {
        ValidateScope(scope, requireParent: false);
    }

    public static void ValidateNewNode(ProvisioningSettingsScope scope)
    {
        ValidateScope(scope, requireParent: scope.ScopeType != ProvisioningScopeType.Global);
        if (scope.NodeId != 0)
        {
            throw new ArgumentException("A new provisioning scope cannot already have a node ID.", nameof(scope));
        }
    }

    public static void ValidatePersistedChain(
        IReadOnlyList<ProvisioningSettingsScope> chain,
        ProvisioningScopeType expectedLeafType)
    {
        ArgumentNullException.ThrowIfNull(chain);

        int expectedCount = GetLevelIndex(expectedLeafType) + 1;
        if (chain.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Provisioning hierarchy for '{expectedLeafType}' must contain {expectedCount} node(s), but contains {chain.Count}.");
        }

        HashSet<long> nodeIds = [];
        for (int index = 0; index < chain.Count; index++)
        {
            ProvisioningSettingsScope scope = chain[index];
            ValidateScope(scope, requireParent: index > 0, invalidData: true);

            ProvisioningScopeType expectedType = OrderedScopeTypes[index];
            if (scope.ScopeType != expectedType)
            {
                throw new InvalidOperationException(
                    $"Provisioning hierarchy position {index} must be '{expectedType}', but is '{scope.ScopeType}'.");
            }

            if (scope.NodeId <= 0)
            {
                throw new InvalidOperationException(
                    $"Persisted provisioning scope '{scope.ScopeType}:{scope.ObjectKey}' has invalid node ID '{scope.NodeId}'.");
            }

            if (!nodeIds.Add(scope.NodeId))
            {
                throw new InvalidOperationException(
                    $"Provisioning hierarchy contains node ID '{scope.NodeId}' more than once.");
            }

            if (index == 0)
            {
                if (scope.ParentNodeId is not null)
                {
                    throw new InvalidOperationException("The global provisioning node cannot have a parent.");
                }
            }
            else if (scope.ParentNodeId != chain[index - 1].NodeId)
            {
                throw new InvalidOperationException(
                    $"Provisioning node '{scope.NodeId}' does not reference the preceding hierarchy node '{chain[index - 1].NodeId}'.");
            }
        }
    }

    public static void ValidateRequestedScope(
        ProvisioningSettingsScope requested,
        ProvisioningSettingsScope persisted)
    {
        if (requested.ScopeType != persisted.ScopeType
            || !string.Equals(requested.ObjectKey, persisted.ObjectKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The database returned provisioning scope '{persisted.ScopeType}:{persisted.ObjectKey}' "
                + $"for request '{requested.ScopeType}:{requested.ObjectKey}'.");
        }

        if (requested.NodeId != 0 && requested.NodeId != persisted.NodeId)
        {
            throw new InvalidOperationException(
                $"Provisioning scope '{requested.ScopeType}:{requested.ObjectKey}' expected node ID '{requested.NodeId}', "
                + $"but the database returned '{persisted.NodeId}'.");
        }

        if (requested.ParentNodeId.HasValue && requested.ParentNodeId != persisted.ParentNodeId)
        {
            throw new InvalidOperationException(
                $"Provisioning scope '{requested.ScopeType}:{requested.ObjectKey}' expected parent node ID "
                + $"'{requested.ParentNodeId}', but the database returned '{persisted.ParentNodeId}'.");
        }
    }

    public static void ValidateChild(
        IReadOnlyList<ProvisioningSettingsScope> parentChain,
        ProvisioningSettingsScope child)
    {
        if (parentChain.Count == 0)
        {
            throw new InvalidOperationException("A child scope requires a persisted parent hierarchy.");
        }

        ProvisioningSettingsScope parent = parentChain[^1];
        ProvisioningScopeType expectedChildType = GetChildType(parent.ScopeType)
            ?? throw new InvalidOperationException("Gateway provisioning nodes cannot have child nodes.");

        ValidateScope(child, requireParent: true, invalidData: true);
        if (child.NodeId <= 0)
        {
            throw new InvalidOperationException(
                $"Persisted child scope '{child.ScopeType}:{child.ObjectKey}' has invalid node ID '{child.NodeId}'.");
        }

        if (child.ScopeType != expectedChildType)
        {
            throw new InvalidOperationException(
                $"Provisioning node '{parent.NodeId}' of type '{parent.ScopeType}' can only have '{expectedChildType}' children, "
                + $"but child '{child.NodeId}' is '{child.ScopeType}'.");
        }

        if (child.ParentNodeId != parent.NodeId)
        {
            throw new InvalidOperationException(
                $"Provisioning child node '{child.NodeId}' does not reference requested parent node '{parent.NodeId}'.");
        }
    }

    public static void ValidateNewChild(
        IReadOnlyList<ProvisioningSettingsScope> parentChain,
        ProvisioningSettingsScope child)
    {
        if (parentChain.Count == 0)
        {
            throw new InvalidOperationException("A new child scope requires a persisted parent hierarchy.");
        }

        ValidateNewNode(child);

        ProvisioningSettingsScope parent = parentChain[^1];
        ProvisioningScopeType expectedChildType = GetChildType(parent.ScopeType)
            ?? throw new ArgumentException("Gateway provisioning nodes cannot have child nodes.", nameof(child));

        if (child.ScopeType != expectedChildType)
        {
            throw new ArgumentException(
                $"Provisioning node '{parent.NodeId}' of type '{parent.ScopeType}' can only have '{expectedChildType}' children, "
                + $"not '{child.ScopeType}'.",
                nameof(child));
        }

        if (child.ParentNodeId != parent.NodeId)
        {
            throw new ArgumentException(
                $"New provisioning scope '{child.ScopeType}:{child.ObjectKey}' must reference parent node '{parent.NodeId}'.",
                nameof(child));
        }
    }

    public static void ValidateSettingsType(ProvisioningScopeType scopeType, Type settingsType)
    {
        Type expectedType = scopeType switch
        {
            ProvisioningScopeType.Global => typeof(GlobalProvisioningSettings),
            ProvisioningScopeType.DeviceType => typeof(DeviceTypeProvisioningSettings),
            ProvisioningScopeType.Management => typeof(ManagementProvisioningSettings),
            ProvisioningScopeType.Gateway => typeof(GatewayProvisioningSettings),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined provisioning scope type is required.")
        };

        if (settingsType != expectedType)
        {
            throw new ArgumentException(
                $"Scope type '{scopeType}' must be loaded as '{expectedType.Name}', not '{settingsType.Name}'.",
                nameof(settingsType));
        }
    }

    public static ProvisioningScopeType? GetParentType(ProvisioningScopeType scopeType)
    {
        return scopeType switch
        {
            ProvisioningScopeType.Global => null,
            ProvisioningScopeType.DeviceType => ProvisioningScopeType.Global,
            ProvisioningScopeType.Management => ProvisioningScopeType.DeviceType,
            ProvisioningScopeType.Gateway => ProvisioningScopeType.Management,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined provisioning scope type is required.")
        };
    }

    private static ProvisioningScopeType? GetChildType(ProvisioningScopeType scopeType)
    {
        return scopeType switch
        {
            ProvisioningScopeType.Global => ProvisioningScopeType.DeviceType,
            ProvisioningScopeType.DeviceType => ProvisioningScopeType.Management,
            ProvisioningScopeType.Management => ProvisioningScopeType.Gateway,
            ProvisioningScopeType.Gateway => null,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined provisioning scope type is required.")
        };
    }

    private static int GetLevelIndex(ProvisioningScopeType scopeType)
    {
        int index = Array.IndexOf(OrderedScopeTypes, scopeType);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined provisioning scope type is required.");
        }
        return index;
    }

    private static void ValidateScope(
        ProvisioningSettingsScope scope,
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

    private static string? GetScopeError(ProvisioningSettingsScope scope, bool requireParent)
    {
        if (!Enum.IsDefined(scope.ScopeType) || scope.ScopeType == ProvisioningScopeType.Undefined)
        {
            return $"Provisioning scope type '{scope.ScopeType}' is not defined.";
        }

        if (string.IsNullOrWhiteSpace(scope.ObjectKey))
        {
            return "A provisioning scope requires a non-empty object key.";
        }

        if (!string.Equals(scope.ObjectKey, scope.ObjectKey.Trim(), StringComparison.Ordinal))
        {
            return "A provisioning scope object key cannot have leading or trailing whitespace.";
        }

        if (scope.NodeId < 0)
        {
            return $"Provisioning node ID '{scope.NodeId}' cannot be negative.";
        }

        if (scope.ParentNodeId <= 0)
        {
            return $"Provisioning parent node ID '{scope.ParentNodeId}' must be positive.";
        }

        return GetParentError(scope, requireParent) ?? GetObjectKeyFormatError(scope);
    }

    private static string? GetParentError(ProvisioningSettingsScope scope, bool requireParent)
    {
        if (scope.ScopeType == ProvisioningScopeType.Global)
        {
            if (!string.Equals(scope.ObjectKey, ProvisioningSettingsScopeFactory.GlobalNodeType, StringComparison.Ordinal))
            {
                return $"The global provisioning scope must use object key '{ProvisioningSettingsScopeFactory.GlobalNodeType}'.";
            }

            if (scope.ParentNodeId is not null)
            {
                return "The global provisioning scope cannot have a parent.";
            }
        }
        else if (requireParent && scope.ParentNodeId is null)
        {
            return $"Provisioning scope type '{scope.ScopeType}' requires a parent node ID.";
        }

        return null;
    }

    private static string? GetObjectKeyFormatError(ProvisioningSettingsScope scope)
    {
        if (scope.ScopeType is ProvisioningScopeType.Management or ProvisioningScopeType.Gateway
            && (!long.TryParse(scope.ObjectKey, NumberStyles.None, CultureInfo.InvariantCulture, out long objectId)
                || objectId <= 0
                || !string.Equals(objectId.ToString(CultureInfo.InvariantCulture), scope.ObjectKey, StringComparison.Ordinal)))
        {
            return $"Provisioning scope type '{scope.ScopeType}' requires a positive numeric object key.";
        }

        return null;
    }
}
