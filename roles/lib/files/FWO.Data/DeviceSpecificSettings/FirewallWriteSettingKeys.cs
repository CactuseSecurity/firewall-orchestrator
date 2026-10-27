using System.Collections.ObjectModel;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// Device specific setting keys of the firewall write library: one endpoint and one body template key per
/// <see cref="FirewallWriteOperation"/>, plus the connection settings. An empty endpoint or template means that the
/// default shipped for the device type is used.
/// </summary>
public static class FirewallWriteSettingKeys
{
    private const string kEndpointKeyPrefix = "fwWriteEndpoint";
    private const string kTemplateKeyPrefix = "fwWriteTemplate";

    private static readonly List<DeviceSpecificScopeType> AllScopeTypes =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management, DeviceSpecificScopeType.Gateway];
    private static readonly List<DeviceSpecificScopeType> ManagerScopeTypes =
        [DeviceSpecificScopeType.Global, DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management];
    private static readonly List<DeviceSpecificScopeType> DeviceScopeTypes =
        [DeviceSpecificScopeType.DeviceType, DeviceSpecificScopeType.Management, DeviceSpecificScopeType.Gateway];

    /// <summary>Whether the server certificate of the firewall management is checked when writing to it.</summary>
    public static readonly DeviceSpecificSettingKey<bool> CheckCertificates =
        new("fwWriteCheckCertificates", ManagerScopeTypes);

    /// <summary>Whether the policy is installed on the gateway after the changes were written and published.</summary>
    public static readonly DeviceSpecificSettingKey<bool> InstallPolicy =
        new("fwWriteInstallPolicy", AllScopeTypes);

    /// <summary>Endpoint ("METHOD path") setting per operation.</summary>
    public static IReadOnlyDictionary<FirewallWriteOperation, DeviceSpecificSettingKey<string>> Endpoints { get; } =
        CreateKeys(kEndpointKeyPrefix, settings => settings.FwWriteEndpoints);

    /// <summary>Body template setting per operation.</summary>
    public static IReadOnlyDictionary<FirewallWriteOperation, DeviceSpecificSettingKey<string>> Templates { get; } =
        CreateKeys(kTemplateKeyPrefix, settings => settings.FwWriteTemplates);

    /// <summary>All firewall write keys: connection settings first, then endpoint and template per operation.</summary>
    public static IReadOnlyList<DeviceSpecificSettingKey> All { get; } = BuildAll();

    private static ReadOnlyDictionary<FirewallWriteOperation, DeviceSpecificSettingKey<string>> CreateKeys(
        string prefix, Func<GlobalDeviceSpecificSettings, Dictionary<FirewallWriteOperation, string>> store)
    {
        Dictionary<FirewallWriteOperation, DeviceSpecificSettingKey<string>> keys = [];
        foreach (FirewallWriteOperation operation in Enum.GetValues<FirewallWriteOperation>())
        {
            keys[operation] = new DeviceSpecificSettingKey<string>(
                prefix + operation,
                DeviceScopeTypes,
                settings => store(settings).GetValueOrDefault(operation, ""),
                (settings, value) => store(settings)[operation] = value);
        }
        return new ReadOnlyDictionary<FirewallWriteOperation, DeviceSpecificSettingKey<string>>(keys);
    }

    private static ReadOnlyCollection<DeviceSpecificSettingKey> BuildAll()
    {
        List<DeviceSpecificSettingKey> keys = [CheckCertificates, InstallPolicy];
        foreach (FirewallWriteOperation operation in Enum.GetValues<FirewallWriteOperation>())
        {
            keys.Add(Endpoints[operation]);
            keys.Add(Templates[operation]);
        }
        return keys.AsReadOnly();
    }
}
