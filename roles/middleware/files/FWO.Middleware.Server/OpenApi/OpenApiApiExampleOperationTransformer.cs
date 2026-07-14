using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Adapts FWO-owned API examples to the built-in OpenAPI document model.
/// </summary>
public sealed class OpenApiApiExampleOperationTransformer : IOpenApiOperationTransformer
{
    private readonly ApiExampleCatalog catalog;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    private readonly List<IOpenApiEndpointDocumentationProvider> documentationProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiApiExampleOperationTransformer"/> class.
    /// </summary>
    public OpenApiApiExampleOperationTransformer(
        ApiExampleCatalog catalog,
        IOptions<JsonOptions> jsonOptions,
        IEnumerable<IOpenApiEndpointDocumentationProvider> documentationProviders)
    {
        this.catalog = catalog;
        jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
        this.documentationProviders = documentationProviders.ToList();
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ApplyEndpointDocumentation(operation, context.Description);
        ApplyRequestExample(operation, context.Description);
        ApplyResponseExamples(operation, context.Description);
        return Task.CompletedTask;
    }

    private void ApplyEndpointDocumentation(OpenApiOperation operation, ApiDescription apiDescription)
    {
        foreach (IOpenApiEndpointDocumentationProvider provider in documentationProviders)
        {
            if (provider.Matches(apiDescription))
            {
                provider.Apply(operation);
            }
        }
    }

    private void ApplyRequestExample(OpenApiOperation operation, ApiDescription apiDescription)
    {
        Type? requestType = apiDescription.ParameterDescriptions
            .FirstOrDefault(parameter => parameter.Source == BindingSource.Body)
            ?.Type;

        if (requestType == null || operation.RequestBody == null || !catalog.TryGetExample(requestType, out object? example))
        {
            return;
        }

        ApplyExample(operation.RequestBody.Content, example, requestType);
    }

    private void ApplyResponseExamples(OpenApiOperation operation, ApiDescription apiDescription)
    {
        if (operation.Responses == null)
        {
            return;
        }

        foreach (ApiResponseType responseType in apiDescription.SupportedResponseTypes)
        {
            Type? modelType = responseType.Type;
            string statusCode = responseType.StatusCode.ToString();
            if (modelType == null || modelType == typeof(void) || !operation.Responses.TryGetValue(statusCode, out IOpenApiResponse? response))
            {
                continue;
            }

            if (catalog.TryGetExample(modelType, out object? example))
            {
                ApplyExample(response.Content, example, modelType);
            }
        }
    }

    private void ApplyExample(IDictionary<string, OpenApiMediaType>? content, object? example, Type declaredType)
    {
        if (content == null || example == null)
        {
            return;
        }

        Type exampleType = declaredType.IsAssignableFrom(example.GetType()) ? declaredType : example.GetType();
        JsonNode? exampleNode = JsonSerializer.SerializeToNode(example, exampleType, jsonSerializerOptions);
        foreach (OpenApiMediaType mediaType in content.Values)
        {
            mediaType.Example = exampleNode?.DeepClone();
        }
    }
}
