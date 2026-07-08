using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FWO.Middleware.Server;

/// <summary>
/// Adds stable operation names to generated OpenAPI operations.
/// </summary>
public sealed class OpenApiOperationNameTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Adds an operation ID when the generated operation does not already define one.
    /// </summary>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operation.OperationId))
        {
            operation.OperationId = CreateOperationId(context.Description);
        }

        PromoteEndpointPathToSummary(operation, context.Description);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a stable operation ID from controller metadata or route information.
    /// </summary>
    public static string CreateOperationId(ApiDescription description)
    {
        if (description.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            return SanitizeOperationId($"{controllerAction.ControllerName}_{controllerAction.ActionName}");
        }

        string httpMethod = description.HttpMethod ?? "Endpoint";
        string relativePath = description.RelativePath ?? string.Empty;

        return SanitizeOperationId($"{httpMethod}_{relativePath}");
    }

    /// <summary>
    /// Creates a display name from the real endpoint path.
    /// </summary>
    public static string CreateEndpointPath(ApiDescription description)
    {
        string relativePath = description.RelativePath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "/";
        }

        return relativePath.StartsWith('/') ? relativePath : $"/{relativePath}";
    }

    private static string SanitizeOperationId(string operationId)
    {
        char[] chars = operationId.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
        string sanitized = new(chars);

        return string.Join("_", sanitized.Split('_', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void PromoteEndpointPathToSummary(OpenApiOperation operation, ApiDescription description)
    {
        string endpointPath = CreateEndpointPath(description);

        if (string.Equals(operation.Summary, endpointPath, StringComparison.Ordinal))
        {
            return;
        }

        string previousSummary = operation.Summary ?? string.Empty;
        operation.Summary = endpointPath;

        if (string.IsNullOrWhiteSpace(previousSummary))
        {
            return;
        }

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? previousSummary
            : $"{previousSummary}\n\n{operation.Description}";
    }
}
