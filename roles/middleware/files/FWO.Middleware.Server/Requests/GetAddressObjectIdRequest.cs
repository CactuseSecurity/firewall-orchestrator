using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAddressObjectIdRequest type.
/// </summary>
public sealed class GetAddressObjectIdRequest : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
    /// <summary>
    /// Gets the IpStart value.
    /// </summary>
    [JsonPropertyName("ipStart")]
    public string? IpStart { get; set; }

    /// <summary>
    /// Gets the IpEnd value.
    /// </summary>
    [JsonPropertyName("ipEnd")]
    public string? IpEnd { get; set; }

}
