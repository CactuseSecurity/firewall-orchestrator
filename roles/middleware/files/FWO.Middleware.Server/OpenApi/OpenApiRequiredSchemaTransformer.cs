using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Adds every property marked with <see cref="OpenApiRequiredAttribute"/> to the required list of
/// the generated OpenAPI schema of its type.
/// </summary>
/// <remarks>
/// The property names are taken from the JSON contract rather than from the CLR properties, so a
/// property renamed with <c>[JsonPropertyName]</c> is listed under the name callers actually send.
/// </remarks>
public sealed class OpenApiRequiredSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Marks the annotated properties of the transformed schema as required.
    /// </summary>
    /// <param name="schema">Schema generated for the type.</param>
    /// <param name="context">Context carrying the JSON contract of the type.</param>
    /// <param name="cancellationToken">Token used to cancel the transformation.</param>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (JsonPropertyInfo property in context.JsonTypeInfo.Properties)
        {
            if (IsMarkedRequired(property))
            {
                schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                schema.Required.Add(property.Name);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reports whether the property carries <see cref="OpenApiRequiredAttribute"/>.
    /// </summary>
    /// <param name="property">JSON contract entry of the property.</param>
    /// <returns>True when the property is annotated.</returns>
    private static bool IsMarkedRequired(JsonPropertyInfo property)
    {
        return property.AttributeProvider is ICustomAttributeProvider attributeProvider
            && attributeProvider.GetCustomAttributes(typeof(OpenApiRequiredAttribute), true).Length > 0;
    }
}
