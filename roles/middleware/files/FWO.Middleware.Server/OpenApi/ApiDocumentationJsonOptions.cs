using Microsoft.AspNetCore.Mvc;
using FWO.Middleware.Server.Responses;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Centralizes JSON options used by middleware REST controllers and API documentation examples.
/// </summary>
public static class ApiDocumentationJsonOptions
{
    /// <summary>
    /// Applies the production JSON options used by middleware controllers.
    /// </summary>
    public static void Configure(JsonOptions jsonOptions)
    {
        jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
        jsonOptions.JsonSerializerOptions.TypeInfoResolver = CreateTypeInfoResolver(false);
    }

    /// <summary>
    /// Creates serializer options equivalent to the middleware controller JSON options.
    /// </summary>
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = null,
            TypeInfoResolver = CreateTypeInfoResolver(false)
        };
        return options;
    }

    /// <summary>
    /// Creates serializer options for published API examples. Empty string properties are omitted so that
    /// an example only documents the fields it actually demonstrates, which matters for mutually exclusive
    /// mutually exclusive request alternatives such as ipHost, ipNetwork, and ipRange.
    /// </summary>
    /// <param name="baseOptions">Controller options the examples should follow, or null for the defaults.</param>
    public static JsonSerializerOptions CreateExampleSerializerOptions(JsonSerializerOptions? baseOptions = null)
    {
        JsonSerializerOptions options = baseOptions == null ? CreateSerializerOptions() : new(baseOptions);
        options.TypeInfoResolver = CreateTypeInfoResolver(true);
        return options;
    }

    /// <summary>
    /// Creates the JSON contract resolver shared by runtime responses and API examples.
    /// </summary>
    private static DefaultJsonTypeInfoResolver CreateTypeInfoResolver(bool omitEmptyExampleValues)
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(ConfigureOptionalTechnicalDetails);
        if (omitEmptyExampleValues)
        {
            resolver.Modifiers.Add(OmitEmptyExampleValues);
        }
        return resolver;
    }

    /// <summary>
    /// Makes technical member fields conditional on the response instance's detail flag.
    /// </summary>
    private static void ConfigureOptionalTechnicalDetails(JsonTypeInfo typeInfo)
    {
        if (!typeof(IOptionalTechnicalDetailsResponse).IsAssignableFrom(typeInfo.Type))
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties.Where(property => property.Name is not "id" and not "name"))
        {
            property.ShouldSerialize = (response, _) =>
                response is IOptionalTechnicalDetailsResponse { IncludeTechnicalDetails: true };
        }
    }

    /// <summary>
    /// Suppresses string properties without content when an example is serialized.
    /// </summary>
    /// <param name="typeInfo">Type contract to adjust.</param>
    private static void OmitEmptyExampleValues(JsonTypeInfo typeInfo)
    {
        foreach (JsonPropertyInfo property in typeInfo.Properties.Where(property => property.PropertyType == typeof(string)))
        {
            Func<object, object?, bool>? existingPredicate = property.ShouldSerialize;
            property.ShouldSerialize = (response, value) =>
                (existingPredicate?.Invoke(response, value) ?? true)
                && value is string text
                && text.Length > 0;
        }

        if (typeof(Requests.IAddressInput).IsAssignableFrom(typeInfo.Type))
        {
            JsonPropertyInfo? rangeProperty = typeInfo.Properties.FirstOrDefault(property => property.Name == "ipRange");
            if (rangeProperty != null)
            {
                rangeProperty.ShouldSerialize = (_, value) => value is List<string> { Count: > 0 };
            }
        }
    }
}
