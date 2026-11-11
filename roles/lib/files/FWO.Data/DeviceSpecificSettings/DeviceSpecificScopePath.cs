using System.Globalization;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// The path of one level of the device specific settings hierarchy from Global down to that level, built from the current
/// device hierarchy: the device type of a management and the management of a gateway are taken from the given
/// objects, never from stored device specific settings nodes. Settings are resolved along this path only, so that a management
/// whose device type changed or a gateway moved to another management inherits from its current parent levels.
/// </summary>
public sealed class DeviceSpecificScopePath
{
    /// <summary>Object key of the single global level.</summary>
    public const string GlobalObjectKey = "global";

    /// <summary>Display name stored for the global level.</summary>
    public const string GlobalDisplayName = "Global";

    private readonly List<Level> levels;

    private DeviceSpecificScopePath(params List<Level> levels)
    {
        this.levels = levels;
    }

    /// <summary>Most specific level of the path.</summary>
    public DeviceSpecificScopeType ScopeType => levels[^1].ScopeType;

    /// <summary>Object key of the most specific level of the path.</summary>
    public string ObjectKey => levels[^1].ObjectKey;

    /// <summary>
    /// Returns new scope objects for the levels of the path, Global first. The scopes carry no node IDs; the stored
    /// nodes are looked up by scope type and object key.
    /// </summary>
    public List<DeviceSpecificSettingsScope> ToScopes()
    {
        return [.. levels.Select(level => new DeviceSpecificSettingsScope
        {
            ScopeType = level.ScopeType,
            ObjectKey = level.ObjectKey,
            DisplayName = level.DisplayName
        })];
    }

    /// <summary>The path of the global level.</summary>
    public static DeviceSpecificScopePath Global()
    {
        return new DeviceSpecificScopePath(GlobalLevel());
    }

    /// <summary>The path Global &gt; device type.</summary>
    public static DeviceSpecificScopePath ForDeviceType(DeviceType deviceType)
    {
        ArgumentNullException.ThrowIfNull(deviceType);
        return new DeviceSpecificScopePath(GlobalLevel(), DeviceTypeLevel(deviceType));
    }

    /// <summary>The path Global &gt; the management's current device type &gt; management.</summary>
    public static DeviceSpecificScopePath ForManagement(Management management)
    {
        ArgumentNullException.ThrowIfNull(management);
        return new DeviceSpecificScopePath(GlobalLevel(), DeviceTypeLevel(management.DeviceType), ManagementLevel(management));
    }

    /// <summary>
    /// The path Global &gt; the management's current device type &gt; management &gt; gateway. The gateway must belong
    /// to the management, either listed in its devices or referencing it.
    /// </summary>
    public static DeviceSpecificScopePath ForGateway(Management management, Device device)
    {
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(device);
        if (device.Management?.Id != management.Id && !management.Devices.Any(member => member.Id == device.Id))
        {
            throw new ArgumentException(
                $"Gateway '{device.Id}' does not belong to management '{management.Id}'.", nameof(device));
        }

        return new DeviceSpecificScopePath(
            GlobalLevel(),
            DeviceTypeLevel(management.DeviceType),
            ManagementLevel(management),
            new Level(DeviceSpecificScopeType.Gateway, Key(device.Id), device.Name ?? Key(device.Id)));
    }

    private static Level GlobalLevel() => new(DeviceSpecificScopeType.Global, GlobalObjectKey, GlobalDisplayName);

    private static Level DeviceTypeLevel(DeviceType deviceType)
    {
        ArgumentNullException.ThrowIfNull(deviceType);
        return new Level(DeviceSpecificScopeType.DeviceType, Key(deviceType.Id), deviceType.NameVersion());
    }

    private static Level ManagementLevel(Management management) =>
        new(DeviceSpecificScopeType.Management, Key(management.Id), management.Name);

    private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);

    private sealed record Level(DeviceSpecificScopeType ScopeType, string ObjectKey, string DisplayName);
}
