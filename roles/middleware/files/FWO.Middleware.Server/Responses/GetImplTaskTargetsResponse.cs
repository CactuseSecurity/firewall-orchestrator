using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GetImplTaskTargetsResponse type.
/// </summary>
public sealed class GetImplTaskTargetsResponse
{
    /// <summary>
    /// Gets or sets the managements the object task gets one implementation task each on, ordered by name and
    /// without duplicates. Holds the requested management itself when it is unknown or has no sub-managements;
    /// empty only when the supplied filter excludes every target.
    /// </summary>
    [JsonPropertyName("targets")]
    public List<ImplTaskTargetResponse> Targets { get; set; } = [];
}

/// <summary>
/// Represents one target management of an object task.
/// </summary>
public sealed class ImplTaskTargetResponse
{
    /// <summary>
    /// Gets or sets the database id of the target management.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the target management, used as suffix of the implementation task title. Empty
    /// when the requested management is unknown.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
