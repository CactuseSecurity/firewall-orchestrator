using System.Net.Http.Headers;

namespace FWO.Ai.Harness.Mcp;

/// <summary>
/// Adds the calling user's JWT as bearer token to every request sent to the MCP server.
/// The token is asked for on each request, so a renewed token is picked up without reconnecting.
/// </summary>
internal sealed class BearerTokenHandler : DelegatingHandler
{
    private const string kBearerScheme = "Bearer";

    private readonly Func<CancellationToken, ValueTask<string>> accessTokenProvider;

    /// <summary>
    /// Creates the handler; set <see cref="DelegatingHandler.InnerHandler"/> before use.
    /// </summary>
    /// <param name="accessTokenProvider">Returns the JWT of the calling user</param>
    internal BearerTokenHandler(Func<CancellationToken, ValueTask<string>> accessTokenProvider)
    {
        this.accessTokenProvider = accessTokenProvider;
    }

    /// <summary>
    /// Sets the authorization header and sends the request.
    /// </summary>
    /// <exception cref="InvalidOperationException">The token provider returned no token.</exception>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string accessToken = await accessTokenProvider(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("The access token provider returned no token for the MCP server.");
        }
        request.Headers.Authorization = new AuthenticationHeaderValue(kBearerScheme, accessToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
