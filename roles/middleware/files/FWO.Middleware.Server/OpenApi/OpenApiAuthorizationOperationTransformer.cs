using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Adds the bearer security requirement only to operations that actually require authorization.
/// Anonymous endpoints such as login and token issuance therefore no longer advertise an unneeded
/// bearer header in the generated documentation and Scalar request examples.
/// </summary>
public sealed class OpenApiAuthorizationOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Identifier of the bearer security scheme registered with the OpenAPI document.
    /// </summary>
    public const string BearerSchemeId = "bearer";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ApplyAuthorizationRequirement(operation, context.Description.ActionDescriptor.EndpointMetadata);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies the bearer security requirement when endpoint metadata requires authorization.
    /// </summary>
    public static void ApplyAuthorizationRequirement(OpenApiOperation operation, IList<object> metadata)
    {
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeId, null)] = []
            }
        ];
    }
}
