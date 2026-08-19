using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Base type for middleware POST request bodies. Every derived request supports an optional JSON options object.
/// </summary>
public abstract class RequestDto<TOptions> : IRequestWithOptions<TOptions>
    where TOptions : RequestOptionsDto, new()
{
    /// <summary>
    /// Gets or sets the optional request options. When omitted from JSON, this defaults to an empty options object.
    /// </summary>
    [JsonPropertyName("options")]
    public TOptions? Options { get; set; } = new();

    /// <summary>
    /// Gets or sets unknown root-level JSON members captured for explicit request validation.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
