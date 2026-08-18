using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the VisibleInRequestFilter type.
/// </summary>
public sealed class VisibleInRequestFilter : RequestFilterDto
{
    /// <summary>
    /// Gets the VisibleInRequest value.
    /// </summary>
    [JsonPropertyName("visibleInRequest")]
    public bool? VisibleInRequest { get; set; }
}
