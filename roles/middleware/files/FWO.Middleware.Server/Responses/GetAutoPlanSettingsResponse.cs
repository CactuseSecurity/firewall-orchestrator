using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GetAutoPlanSettingsResponse type.
/// </summary>
public sealed class GetAutoPlanSettingsResponse
{
    /// <summary>
    /// Gets or sets one page of the effective settings: the global settings first, then every requested management
    /// and then every requested gateway, each ordered by id. Unknown ids and ids not visible to the caller get the
    /// global settings.
    /// </summary>
    [JsonPropertyName("settings")]
    public List<AutoPlanSettingResponse> Settings { get; set; } = [];
}

/// <summary>
/// Represents the effective auto plan settings of one level of the device specific settings hierarchy.
/// </summary>
public sealed class AutoPlanSettingResponse
{
    /// <summary>
    /// Gets or sets the scope type of the level: global, management or gateway.
    /// </summary>
    [JsonPropertyName("scopeType")]
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database id of the management or gateway; null for the global level.
    /// </summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>
    /// Gets or sets whether implementation tasks are created automatically for every task type, inherited along
    /// the hierarchy.
    /// </summary>
    [JsonPropertyName("autoPlanImplementation")]
    public bool AutoPlanImplementation { get; set; }

    /// <summary>
    /// Gets or sets whether implementation tasks of rule tasks are created automatically, inherited along the
    /// hierarchy; only effective together with autoPlanImplementation.
    /// </summary>
    [JsonPropertyName("autoPlanImplementationForRules")]
    public bool AutoPlanImplementationForRules { get; set; }
}
