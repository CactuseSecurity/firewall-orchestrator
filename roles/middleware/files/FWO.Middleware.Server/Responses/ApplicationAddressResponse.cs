using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents every active app-server address owned by one application.
/// </summary>
public sealed class ApplicationAddressResponse
{
    /// <summary>
    /// Gets or sets the owning application id.
    /// </summary>
    [JsonPropertyName("applicationId")]
    public int ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the owning application name.
    /// </summary>
    [JsonPropertyName("applicationName")]
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional external identifier of the owning application.
    /// </summary>
    [JsonPropertyName("appIdExternal")]
    public string? AppIdExternal { get; set; }

    /// <summary>
    /// Gets or sets every undeleted compact IP address or range assigned to the application.
    /// </summary>
    [JsonPropertyName("addresses")]
    public List<string> Addresses { get; set; } = [];
}
