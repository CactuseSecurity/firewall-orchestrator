using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
}
