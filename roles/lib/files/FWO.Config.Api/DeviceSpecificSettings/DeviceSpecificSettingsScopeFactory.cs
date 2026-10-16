using FWO.Config.Api.Data;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Config.Api.DeviceSpecificSettings;

/// <summary>
/// Converts between database node metadata and the public device specific settings scope model.
/// </summary>
public static class DeviceSpecificSettingsScopeFactory
{
    public const string GlobalNodeType = "global";
    public const string DeviceTypeNodeType = "device_type";
    public const string ManagementNodeType = "management";
    public const string GatewayNodeType = "gateway";

    /// <summary>Creates the scope of a node read from the database.</summary>
    public static DeviceSpecificSettingsScope Create(DeviceSpecificConfigNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new DeviceSpecificSettingsScope
        {
            ScopeType = ParseNodeType(node.NodeType),
            ObjectKey = node.ObjectKey,
            DisplayName = node.DisplayName,
            NodeId = node.Id,
            ParentNodeId = node.ParentId,
            SortOrder = node.SortOrder
        };
    }

    /// <summary>Creates an independent copy of a scope.</summary>
    public static DeviceSpecificSettingsScope Copy(DeviceSpecificSettingsScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return new DeviceSpecificSettingsScope
        {
            ScopeType = scope.ScopeType,
            ObjectKey = scope.ObjectKey,
            DisplayName = scope.DisplayName,
            NodeId = scope.NodeId,
            ParentNodeId = scope.ParentNodeId,
            SortOrder = scope.SortOrder
        };
    }

    /// <summary>Returns the database node type of a scope type.</summary>
    public static string ToNodeType(DeviceSpecificScopeType scopeType)
    {
        return scopeType switch
        {
            DeviceSpecificScopeType.Global => GlobalNodeType,
            DeviceSpecificScopeType.DeviceType => DeviceTypeNodeType,
            DeviceSpecificScopeType.Management => ManagementNodeType,
            DeviceSpecificScopeType.Gateway => GatewayNodeType,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined device specific settings scope type is required.")
        };
    }

    /// <summary>Returns the scope type of a database node type; throws for unknown node types.</summary>
    public static DeviceSpecificScopeType ParseNodeType(string nodeType)
    {
        return nodeType switch
        {
            GlobalNodeType => DeviceSpecificScopeType.Global,
            DeviceTypeNodeType => DeviceSpecificScopeType.DeviceType,
            ManagementNodeType => DeviceSpecificScopeType.Management,
            GatewayNodeType => DeviceSpecificScopeType.Gateway,
            _ => throw new InvalidOperationException($"Unknown device specific settings node type '{nodeType}'.")
        };
    }
}
