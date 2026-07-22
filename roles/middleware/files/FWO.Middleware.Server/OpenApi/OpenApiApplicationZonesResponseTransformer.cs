using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Documents the response variants of the application-zone endpoint.
/// </summary>
public sealed class OpenApiApplicationZonesResponseTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!Matches(context.Description)
            || operation.Responses?.TryGetValue(StatusCodes.Status200OK.ToString(), out IOpenApiResponse? response) != true
            || response is null
            || response.Content is null)
        {
            return;
        }

        OpenApiSchema ipOnlySchema = await context.GetOrCreateSchemaAsync(
            typeof(List<ApplicationZoneIpOnlyResponse>), null, cancellationToken);
        ApplyIpOnlyResponseSchema(response, ipOnlySchema);
    }

    /// <summary>
    /// Adds the compact response shape as an alternative to every successful response media type.
    /// </summary>
    public static void ApplyIpOnlyResponseSchema(IOpenApiResponse response, IOpenApiSchema ipOnlySchema)
    {
        if (response.Content is null)
        {
            return;
        }

        foreach (OpenApiMediaType mediaType in response.Content.Values)
        {
            if (mediaType.Schema is not IOpenApiSchema fullSchema)
            {
                continue;
            }

            mediaType.Schema = new OpenApiSchema
            {
                OneOf = new List<IOpenApiSchema> { fullSchema, ipOnlySchema }
            };
        }
    }

    private static bool Matches(ApiDescription description)
    {
        return description.ActionDescriptor is ControllerActionDescriptor controllerAction
            && controllerAction.ControllerTypeInfo?.AsType() == typeof(ApplicationZonesController)
            && string.Equals(controllerAction.ActionName, nameof(ApplicationZonesController.Get), StringComparison.Ordinal);
    }
}
