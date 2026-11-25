using FWO.Basics.OAuth;
using System.ClientModel.Primitives;

namespace FWO.Ai.Harness.Providers;

/// <summary>
/// Authentication policy of the OpenAI client for OAuth 2.0: sets the access token of the token provider as bearer
/// token on every request. If the server refuses the token (HTTP 401), e.g. because it was revoked before it expired,
/// the token is dropped and the request is sent once more with a new one. Like an API key, a token is only sent over
/// https, unless to a loopback address.
/// Written instead of using BearerTokenPolicy of System.ClientModel, which asks its token provider on every request,
/// refuses loopback http and does not renew a refused token.
/// </summary>
internal sealed class OAuthBearerPolicy : AuthenticationPolicy
{
    private const string kAuthorizationHeader = "Authorization";
    private const string kBearerPrefix = "Bearer ";
    private const int kUnauthorizedStatus = 401;

    private readonly CachedOAuthTokenProvider tokenProvider;

    /// <summary>
    /// Creates the policy.
    /// </summary>
    /// <param name="tokenProvider">Provides the access token; not disposed by the policy</param>
    internal OAuthBearerPolicy(CachedOAuthTokenProvider tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        this.tokenProvider = tokenProvider;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The request goes over plain http to a host that is not a loopback address.</exception>
    /// <exception cref="FWO.Basics.Exceptions.OAuthTokenException">No access token could be obtained.</exception>
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        // the synchronous path of the SDK; it completes synchronously, as the token is waited for and the rest is sent synchronously
        ProcessCoreAsync(message, pipeline, currentIndex, isAsync: false).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The request goes over plain http to a host that is not a loopback address.</exception>
    /// <exception cref="FWO.Basics.Exceptions.OAuthTokenException">No access token could be obtained.</exception>
    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        return ProcessCoreAsync(message, pipeline, currentIndex, isAsync: true);
    }

    /// <summary>
    /// Sends the request with the current token, and once more with a new token if the server refuses it.
    /// </summary>
    private async ValueTask ProcessCoreAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex, bool isAsync)
    {
        RequireSecureUri(message.Request.Uri);
        string token = await GetTokenAsync(message, isAsync);
        await SendWithTokenAsync(message, pipeline, currentIndex, token, isAsync);
        if (message.Response is not { Status: kUnauthorizedStatus } refusedResponse)
        {
            return;
        }
        tokenProvider.Invalidate(token);
        // frees the connection of the refused answer before the request is sent again, like the retry policy of the SDK
        refusedResponse.ContentStream?.Dispose();
        string renewedToken = await GetTokenAsync(message, isAsync);
        await SendWithTokenAsync(message, pipeline, currentIndex, renewedToken, isAsync);
    }

    /// <summary>
    /// Returns the access token of the token provider, waiting for it on the synchronous path.
    /// </summary>
    private async ValueTask<string> GetTokenAsync(PipelineMessage message, bool isAsync)
    {
        Task<string> tokenTask = tokenProvider.GetAccessTokenAsync(message.CancellationToken);
        return isAsync ? await tokenTask : tokenTask.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Sets the token as bearer token and passes the request on to the next policy.
    /// </summary>
    private static async ValueTask SendWithTokenAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex,
        string token, bool isAsync)
    {
        message.Request.Headers.Set(kAuthorizationHeader, kBearerPrefix + token);
        if (isAsync)
        {
            await ProcessNextAsync(message, pipeline, currentIndex);
        }
        else
        {
            ProcessNext(message, pipeline, currentIndex);
        }
    }

    /// <summary>
    /// Requires https, or plain http to a loopback address, as the token is a secret.
    /// </summary>
    /// <exception cref="InvalidOperationException">The request goes elsewhere; the URI is not part of the message.</exception>
    private static void RequireSecureUri(Uri? uri)
    {
        bool isHttps = uri?.Scheme == Uri.UriSchemeHttps;
        bool isLoopbackHttp = uri?.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            throw new InvalidOperationException("The access token is only sent over https, unless to a loopback address.");
        }
    }
}
