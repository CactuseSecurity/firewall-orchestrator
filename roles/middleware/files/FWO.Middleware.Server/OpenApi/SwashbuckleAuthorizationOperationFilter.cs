using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Adds the bearer security requirement only to operations that actually require authorization.
/// Anonymous endpoints such as login and token issuance therefore no longer advertise an unneeded
/// bearer header in the generated documentation and Scalar request examples.
/// </summary>
public sealed class SwashbuckleAuthorizationOperationFilter : IOperationFilter
{
    /// <summary>
    /// Identifier of the bearer security scheme registered with the OpenAPI document.
    /// </summary>
    public const string BearerSchemeId = "bearer";

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        IList<object> metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        // Endpoints opting out of auth (or simply not requiring it) must not advertise a bearer requirement.
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeId, context.Document)] = []
            }
        ];
    }
}
