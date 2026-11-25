using FWO.Basics;

namespace FWO.Ai.Client.AgUi;

/// <summary>
/// Builds the AG-UI endpoint of the harness below the middleware server uri. Plain http is not refused here: the UI
/// sends the same JWT to the same uri with every other middleware call, and the middleware server refuses to start
/// on a plain http listener that is not loopback (see <see cref="EndpointGuard.RequireHttpsOrLoopbackListener"/>).
/// Refusing it here would only make every page of the UI fail, as the chat is part of the layout.
/// </summary>
internal static class AgUiEndpoint
{
    private const string kPathSeparator = "/";
    private const string kMiddlewareServerUriPurpose = "middleware server uri";

    /// <summary>
    /// Returns the AG-UI endpoint below the middleware server uri.
    /// </summary>
    /// <param name="middlewareServerUri">Uri of the middleware server, e.g. https://host:8888/</param>
    /// <exception cref="ArgumentNullException">The uri is null.</exception>
    /// <exception cref="ArgumentException">The uri is relative or uses another scheme than http or https.</exception>
    internal static Uri Create(Uri middlewareServerUri)
    {
        EndpointGuard.RequireHttpOrHttps(middlewareServerUri, kMiddlewareServerUriPurpose);
        return new Uri(WithTrailingSlash(middlewareServerUri), FwoAgentChatClientFactory.kAgUiPath);
    }

    /// <summary>
    /// Returns the uri with a path ending in a slash, so that the AG-UI path is added below it instead of replacing its
    /// last segment.
    /// </summary>
    private static Uri WithTrailingSlash(Uri uri)
    {
        if (uri.AbsolutePath.EndsWith(kPathSeparator, StringComparison.Ordinal))
        {
            return uri;
        }
        UriBuilder builder = new(uri);
        builder.Path += kPathSeparator;
        return builder.Uri;
    }
}
