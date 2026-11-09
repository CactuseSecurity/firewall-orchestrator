using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Provides endpoint-specific documentation for the generated Scalar/OpenAPI document.
/// </summary>
public interface IOpenApiEndpointDocumentationProvider
{
    /// <summary>
    /// Determines whether this provider documents the supplied endpoint.
    /// </summary>
    bool Matches(ApiDescription description);

    /// <summary>
    /// Applies endpoint-specific documentation to the OpenAPI operation.
    /// </summary>
    void Apply(OpenApiOperation operation);
}
