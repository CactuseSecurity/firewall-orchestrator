using System.Text.Json;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the IVisibleInRequestFilterRequest contract.
/// </summary>
public interface IVisibleInRequestFilterRequest : IRequestWithRootAdditionalData
{
    /// <summary>
    /// Gets or sets the visible-in-request filter.
    /// </summary>
    VisibleInRequestFilter? Filter { get; set; }
}
