using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the result of the path device resolution: the devices found and the algorithm and zone matrix
/// the analysis actually used.
/// </summary>
public sealed class ResolvePathDevicesResponse
{
    /// <summary>
    /// Gets or sets the firewall devices on the paths between every source and every destination, each listed
    /// once and in no particular order. Empty when no device lies on any path or the filter excludes all of them.
    /// </summary>
    [JsonPropertyName("devices")]
    public List<PathDeviceResponse> Devices { get; set; } = [];

    /// <summary>
    /// Gets or sets the API name of the path analysis algorithm that ran, either the requested or the configured one.
    /// </summary>
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the zone matrix the analysis ran on, either the requested or the designated one.
    /// Null for algorithms that do not use a zone matrix.
    /// </summary>
    [JsonPropertyName("matrixId")]
    public int? MatrixId { get; set; }
}

/// <summary>
/// Represents one firewall device on an analysed path.
/// </summary>
public sealed class PathDeviceResponse
{
    /// <summary>
    /// Gets or sets the database id of the device.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the device.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
