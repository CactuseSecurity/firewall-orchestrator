using System.Globalization;
using System.Text.Json;
using FWO.Basics.Exceptions;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>The exported package and the stored data that could not be exported.</summary>
/// <param name="Package">The package.</param>
/// <param name="Warnings">Levels and values left out, e.g. a level of a management deleted since its settings were stored.</param>
public sealed record DeviceSpecificSettingsExportResult(DeviceSpecificSettingsPackage Package, List<string> Warnings);

/// <summary>
/// Builds a <see cref="DeviceSpecificSettingsPackage"/> from the stored settings of an installation, addressing every
/// level by its natural key and every referenced object by its name.
/// </summary>
public static class DeviceSpecificSettingsExporter
{
    private sealed record ExportedNode(int LevelOrder, string SortKey, DeviceSpecificSettingsPackageNode Node);

    /// <summary>
    /// Exports every stored level with at least one override. Levels whose object no longer exists and values whose
    /// referenced object no longer exists are left out and reported as warnings.
    /// </summary>
    /// <exception cref="CollectionLimitExceededException">More than <see cref="DeviceSpecificSettingsPackage.kMaxNodes"/> levels would be exported.</exception>
    /// <exception cref="SizeLimitExceededException">
    /// The package written as file would be larger than <see cref="DeviceSpecificSettingsPackage.kMaxPackageBytes"/>, so
    /// it could not be imported again.
    /// </exception>
    public static DeviceSpecificSettingsExportResult Export(DeviceSpecificSettingsTransferContext context, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<string> warnings = [];
        List<ExportedNode> exportedNodes = [];
        foreach (DeviceSpecificStoredNode storedNode in context.StoredNodes.Where(node => node.Values.Count > 0))
        {
            DeviceSpecificSettingsPackageNode? node = CreateNode(storedNode.Scope, context);
            if (node is null)
            {
                warnings.Add($"Level '{storedNode.Scope.DisplayName}' ({storedNode.Scope.ScopeType} {storedNode.Scope.ObjectKey}) "
                    + "was left out: its object no longer exists.");
                continue;
            }

            node.Values = ExportValues(storedNode, context, warnings);
            if (node.Values.Count == 0)
            {
                continue;
            }
            exportedNodes.Add(new ExportedNode((int)storedNode.Scope.ScopeType, SortName(node), node));
        }

        if (exportedNodes.Count > DeviceSpecificSettingsPackage.kMaxNodes)
        {
            throw new CollectionLimitExceededException(DeviceSpecificSettingsPackage.kMaxNodes);
        }

        DeviceSpecificSettingsPackage package = new()
        {
            FwoVersion = context.FwoVersion,
            ExportedAt = exportedAt,
            Nodes = [.. exportedNodes
                .OrderBy(exported => exported.LevelOrder)
                .ThenBy(exported => exported.SortKey, StringComparer.Ordinal)
                .Select(exported => exported.Node)]
        };
        if (!DeviceSpecificSettingsPackageJson.FitsSizeLimit(package))
        {
            throw new SizeLimitExceededException(DeviceSpecificSettingsPackage.kMaxPackageBytes);
        }
        return new DeviceSpecificSettingsExportResult(package, warnings);
    }

    /// <summary>Creates the package entry of a level with its natural key, or null if the level's object no longer exists.</summary>
    private static DeviceSpecificSettingsPackageNode? CreateNode(DeviceSpecificSettingsScope scope, DeviceSpecificSettingsTransferContext context)
    {
        string level = DeviceSpecificSettingsScopeFactory.ToNodeType(scope.ScopeType);
        switch (scope.ScopeType)
        {
            case DeviceSpecificScopeType.Global:
                return new DeviceSpecificSettingsPackageNode { Level = level };
            case DeviceSpecificScopeType.DeviceType:
                DeviceType? deviceType = context.FindDeviceType(scope.ObjectKey);
                return deviceType is null ? null
                    : new DeviceSpecificSettingsPackageNode { Level = level, DeviceTypeName = deviceType.Name, DeviceTypeVersion = deviceType.Version };
            case DeviceSpecificScopeType.Management:
                Management? management = context.FindManagement(scope.ObjectKey);
                return management is null ? null : new DeviceSpecificSettingsPackageNode { Level = level, ManagementName = management.Name };
            case DeviceSpecificScopeType.Gateway:
                (Management Management, Device Device)? gateway = context.FindGateway(scope.ObjectKey);
                return gateway?.Device.Name is null ? null : new DeviceSpecificSettingsPackageNode
                {
                    Level = level,
                    ManagementName = gateway.Value.Management.Name,
                    GatewayName = gateway.Value.Device.Name
                };
            default:
                return null;
        }
    }

    /// <summary>The overrides of a level, ordered by key, with referenced objects replaced by their names.</summary>
    private static Dictionary<string, JsonElement> ExportValues(DeviceSpecificStoredNode storedNode,
        DeviceSpecificSettingsTransferContext context, List<string> warnings)
    {
        Dictionary<string, JsonElement> values = [];
        foreach ((DeviceSpecificSettingKey key, JToken value) in storedNode.Values.OrderBy(entry => entry.Key.DatabaseKey, StringComparer.Ordinal))
        {
            JToken exportedValue = value;
            if (key == DeviceSpecificSettingKeys.ScCredential)
            {
                Credential? credential = value.Type == JTokenType.Integer ? context.FindCredential(value.Value<int>()) : null;
                if (credential is null)
                {
                    warnings.Add($"Setting '{key.DatabaseKey}' of level '{storedNode.Scope.DisplayName}' was left out: "
                        + $"credential {value.ToString(Newtonsoft.Json.Formatting.None)} no longer exists.");
                    continue;
                }
                exportedValue = new JValue(credential.Name);
            }
            values[key.DatabaseKey] = DeviceSpecificSettingsPackageJson.ToJsonElement(exportedValue);
        }
        return values;
    }

    private static string SortName(DeviceSpecificSettingsPackageNode node) => string.Join("\n",
        node.DeviceTypeName ?? "", node.DeviceTypeVersion ?? "", node.ManagementName ?? "", node.GatewayName ?? "");

    /// <summary>File name of an export written at the given time, e.g. <c>device-specific-settings-20261121-1530.fwo-dss.json</c>.</summary>
    public static string FileName(DateTimeOffset exportedAt) =>
        $"device-specific-settings-{exportedAt.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}{DeviceSpecificSettingsPackageJson.kFileSuffix}";
}
