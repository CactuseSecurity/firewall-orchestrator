using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;

namespace FWO.Config.Api.DeviceSpecificSettings;

/// <summary>
/// A persisted device specific settings node together with the overrides stored directly on it, as JSON values.
/// </summary>
/// <param name="Scope">Scope of the persisted node.</param>
/// <param name="Values">Stored overrides by canonical key.</param>
public sealed record DeviceSpecificStoredNode(
    DeviceSpecificSettingsScope Scope,
    IReadOnlyDictionary<DeviceSpecificSettingKey, JToken> Values);
