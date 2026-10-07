using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Documents the mutually exclusive REST address representations.
/// </summary>
public sealed class OpenApiAddressInputSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly string[] AddressPropertyNames = ["ipHost", "ipNetwork", "ipRange"];

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!typeof(IAddressInput).IsAssignableFrom(context.JsonTypeInfo.Type))
        {
            return Task.CompletedTask;
        }

        schema.OneOf = AddressPropertyNames
            .Select(propertyName => (IOpenApiSchema)new OpenApiSchema
            {
                Required = new HashSet<string>(StringComparer.Ordinal) { propertyName }
            })
            .ToList();

        if (schema.Properties?.TryGetValue("ipRange", out IOpenApiSchema? rangeProperty) == true
            && rangeProperty is OpenApiSchema rangeSchema)
        {
            rangeSchema.MinItems = 2;
            rangeSchema.MaxItems = 2;
        }

        return Task.CompletedTask;
    }
}
