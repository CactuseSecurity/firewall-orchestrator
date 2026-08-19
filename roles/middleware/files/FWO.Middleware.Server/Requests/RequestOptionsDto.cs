using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Base type for endpoint-specific request options.
/// </summary>
public class RequestOptionsDto : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets unknown option JSON members captured for explicit request validation.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Base type for endpoint options that expose a filter object.
/// </summary>
/// <typeparam name="TFilter">The endpoint-specific filter type.</typeparam>
public class RequestOptionsDto<TFilter> : RequestOptionsDto
    where TFilter : RequestFilterDto
{
    /// <summary>
    /// Gets or sets the optional endpoint filter.
    /// </summary>
    [JsonPropertyName("filter")]
    public TFilter? Filter { get; set; }
}
