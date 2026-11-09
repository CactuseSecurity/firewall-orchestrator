using System.Text.Json;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the IRequestWithAdditionalData contract.
/// </summary>
public interface IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the additional request data.
    /// </summary>
    Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
