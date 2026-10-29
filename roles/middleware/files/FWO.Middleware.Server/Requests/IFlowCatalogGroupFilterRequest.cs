namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the filter contract shared by Flow Catalog group requests.
/// </summary>
public interface IFlowCatalogGroupFilterRequest : IRequestWithRootAdditionalData
{
    /// <summary>
    /// Gets or sets the group filter.
    /// </summary>
    FlowCatalogGroupFilter? Filter { get; set; }
}
