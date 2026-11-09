using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FWO.Middleware.Server;

/// <summary>
/// Redirects legacy Swagger UI routes to the generated OpenAPI document route.
/// </summary>
public static class SwaggerRedirectMiddleware
{
    private const string kSwaggerPath = "/swagger";
    private const string kSwaggerPathPrefix = "/swagger/";

    /// <summary>
    /// Adds a redirect for legacy Swagger routes without registering overlapping endpoint routes.
    /// </summary>
    public static IApplicationBuilder UseSwaggerRedirect(this IApplicationBuilder app, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException("Swagger redirect target path must not be empty.", nameof(targetPath));
        }

        app.Use(async (context, next) =>
        {
            if (IsSwaggerPath(context.Request.Path))
            {
                context.Response.Redirect(targetPath);
                return;
            }

            await next(context);
        });

        return app;
    }

    /// <summary>
    /// Determines whether a request path targets the legacy Swagger route.
    /// </summary>
    public static bool IsSwaggerPath(PathString requestPath)
    {
        string path = requestPath.Value ?? string.Empty;

        return string.Equals(path, kSwaggerPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(kSwaggerPathPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
