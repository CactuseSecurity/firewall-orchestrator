using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Base type for endpoint-specific request filters.
/// </summary>
public abstract class RequestFilterDto : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets unknown filter JSON members captured for explicit request validation.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
