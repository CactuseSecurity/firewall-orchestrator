using System.Globalization;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>
/// The objects of one installation that device specific settings packages are mapped against: device types,
/// managements with their gateways, credentials (without secrets) and the stored settings nodes.
/// </summary>
public sealed class DeviceSpecificSettingsTransferContext
{
    /// <summary>Name of the global level in packages and previews.</summary>
    public const string kGlobalName = DeviceSpecificScopePath.GlobalDisplayName;

    /// <summary>Separator between management and gateway name in the name of a gateway level.</summary>
    public const string kGatewayNameSeparator = " > ";

    private readonly Dictionary<int, DeviceType> deviceTypesById;
    private readonly Dictionary<int, Management> managementsById;
    private readonly Dictionary<int, (Management Management, Device Device)> gatewaysById = [];
    private readonly Dictionary<int, Credential> credentialsById;
    private readonly Dictionary<(DeviceSpecificScopeType ScopeType, string ObjectKey), DeviceSpecificStoredNode> storedNodesByKey;

    /// <summary>All device types.</summary>
    public IReadOnlyList<DeviceType> DeviceTypes { get; }

    /// <summary>All managements with their gateways.</summary>
    public IReadOnlyList<Management> Managements { get; }

    /// <summary>All credentials, without secrets.</summary>
    public IReadOnlyList<Credential> Credentials { get; }

    /// <summary>The stored settings nodes with their overrides, Global first.</summary>
    public IReadOnlyList<DeviceSpecificStoredNode> StoredNodes { get; }

    /// <summary>FWO version of this installation.</summary>
    public string FwoVersion { get; }

    /// <summary>Creates a context from the objects of an installation.</summary>
    public DeviceSpecificSettingsTransferContext(IReadOnlyList<DeviceType> deviceTypes, IReadOnlyList<Management> managements,
        IReadOnlyList<Credential> credentials, IReadOnlyList<DeviceSpecificStoredNode> storedNodes, string fwoVersion)
    {
        DeviceTypes = deviceTypes ?? throw new ArgumentNullException(nameof(deviceTypes));
        Managements = managements ?? throw new ArgumentNullException(nameof(managements));
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        StoredNodes = storedNodes ?? throw new ArgumentNullException(nameof(storedNodes));
        FwoVersion = fwoVersion ?? "";

        deviceTypesById = deviceTypes.GroupBy(deviceType => deviceType.Id).ToDictionary(group => group.Key, group => group.First());
        managementsById = managements.GroupBy(management => management.Id).ToDictionary(group => group.Key, group => group.First());
        credentialsById = credentials.GroupBy(credential => credential.Id).ToDictionary(group => group.Key, group => group.First());
        storedNodesByKey = storedNodes.ToDictionary(node => (node.Scope.ScopeType, node.Scope.ObjectKey));
        foreach (Management management in managements)
        {
            foreach (Device device in management.Devices)
            {
                gatewaysById.TryAdd(device.Id, (management, device));
            }
        }
    }

    /// <summary>The stored node of a scope, or null if nothing is stored for it.</summary>
    public DeviceSpecificStoredNode? FindStoredNode(DeviceSpecificScopeType scopeType, string objectKey) =>
        storedNodesByKey.GetValueOrDefault((scopeType, objectKey));

    /// <summary>The device type with the given database ID, or null.</summary>
    public DeviceType? FindDeviceType(string objectKey) =>
        TryParseId(objectKey, out int id) ? deviceTypesById.GetValueOrDefault(id) : null;

    /// <summary>The management with the given database ID, or null.</summary>
    public Management? FindManagement(string objectKey) =>
        TryParseId(objectKey, out int id) ? managementsById.GetValueOrDefault(id) : null;

    /// <summary>The gateway with the given database ID and its management, or null.</summary>
    public (Management Management, Device Device)? FindGateway(string objectKey) =>
        TryParseId(objectKey, out int id) && gatewaysById.TryGetValue(id, out (Management, Device) gateway) ? gateway : null;

    /// <summary>The credential with the given database ID, or null.</summary>
    public Credential? FindCredential(int id) => credentialsById.GetValueOrDefault(id);

    /// <summary>Credentials with the given name that can log in with a password, i.e. no key pairs.</summary>
    public List<Credential> FindPasswordCredentials(string name) =>
        [.. Credentials.Where(credential => !credential.IsKeyPair && credential.Name == name)];

    /// <summary>Whether a credential with the given name exists, password or key pair.</summary>
    public bool HasCredentialNamed(string name) => Credentials.Any(credential => credential.Name == name);

    /// <summary>Name of a gateway level as shown in previews and messages.</summary>
    public static string GatewayName(string managementName, string gatewayName) => managementName + kGatewayNameSeparator + gatewayName;

    private static bool TryParseId(string objectKey, out int id) =>
        int.TryParse(objectKey, NumberStyles.None, CultureInfo.InvariantCulture, out id);
}
