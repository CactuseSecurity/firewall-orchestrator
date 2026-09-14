using Microsoft.AspNetCore.Mvc;
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
    }

    /// <summary>
    /// Creates serializer options equivalent to the middleware controller JSON options.
    /// </summary>
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = null
        };
        return options;
    }

    /// <summary>
    /// Creates serializer options for published API examples. Empty string properties are omitted so that
    /// an example only documents the fields it actually demonstrates, which matters for mutually exclusive
    /// fields such as ipStart/ipEnd versus ipNetwork.
    /// </summary>
    /// <param name="baseOptions">Controller options the examples should follow, or null for the defaults.</param>
    public static JsonSerializerOptions CreateExampleSerializerOptions(JsonSerializerOptions? baseOptions = null)
    {
        JsonSerializerOptions options = baseOptions == null ? CreateSerializerOptions() : new(baseOptions);
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { OmitEmptyStrings }
        };
        return options;
    }

    /// <summary>
    /// Suppresses string properties without content when an example is serialized.
    /// </summary>
    /// <param name="typeInfo">Type contract to adjust.</param>
    private static void OmitEmptyStrings(JsonTypeInfo typeInfo)
    {
        foreach (JsonPropertyInfo property in typeInfo.Properties.Where(property => property.PropertyType == typeof(string)))
        {
            property.ShouldSerialize = (_, value) => value is string text && text.Length > 0;
        }
    }
}
