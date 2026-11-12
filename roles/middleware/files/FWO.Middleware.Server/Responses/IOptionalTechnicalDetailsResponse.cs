namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Marks response objects whose technical fields are serialized only on request.
/// </summary>
public interface IOptionalTechnicalDetailsResponse
{
    /// <summary>
    /// Gets or sets whether technical fields are included during serialization.
    /// </summary>
    bool IncludeTechnicalDetails { get; set; }
}
