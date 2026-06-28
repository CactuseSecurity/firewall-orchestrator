using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Adapts FWO-owned API examples to Swashbuckle's OpenAPI document model.
/// </summary>
public sealed class SwashbuckleApiExampleOperationFilter : IOperationFilter
{
    private readonly ApiExampleCatalog catalog;
    private readonly JsonSerializerOptions jsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwashbuckleApiExampleOperationFilter"/> class.
    /// </summary>
    public SwashbuckleApiExampleOperationFilter(ApiExampleCatalog catalog, IOptions<JsonOptions> jsonOptions)
    {
        this.catalog = catalog;
        jsonSerializerOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ApplyRequestExample(operation, context.ApiDescription);
        ApplyResponseExamples(operation, context.ApiDescription);
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
