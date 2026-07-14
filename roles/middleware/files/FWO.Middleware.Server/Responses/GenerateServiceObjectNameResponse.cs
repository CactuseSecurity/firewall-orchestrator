using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the GenerateServiceObjectNameResponse type.
/// </summary>
public sealed class GenerateServiceObjectNameResponse
{
    /// <summary>
    /// Gets the Name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
