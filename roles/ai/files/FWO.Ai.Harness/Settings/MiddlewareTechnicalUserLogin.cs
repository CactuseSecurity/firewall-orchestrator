using FWO.Ai.Harness.Hosting;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using RestSharp;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Logs the technical user of the harness in at the middleware server on the same host, with the password from the
/// file the installer created.
/// </summary>
public sealed class MiddlewareTechnicalUserLogin : ITechnicalUserLogin, IDisposable
{
    private const string kLogCategory = "AI harness login";

    private readonly MiddlewareClient middlewareClient;
    private readonly string technicalUser;
    private readonly string passwordFile;

    /// <summary>
    /// Creates the login of the technical user.
    /// </summary>
    /// <param name="settings">Settings naming the technical user and its password file</param>
    /// <param name="middlewareServerUri">Base URI of the middleware server, e.g. its loopback address on the same host</param>
    public MiddlewareTechnicalUserLogin(HarnessSettings settings, string middlewareServerUri)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(middlewareServerUri);
        technicalUser = settings.TechnicalUser;
        passwordFile = settings.TechnicalUserPasswordFile;
        middlewareClient = new MiddlewareClient(middlewareServerUri);
    }

    /// <summary>
    /// Logs the technical user in with the password from its file, which is read at every login.
    /// </summary>
    /// <exception cref="IOException">The password file cannot be read.</exception>
    /// <exception cref="InvalidOperationException">The middleware server refused the login.</exception>
    public async Task<TokenPair> LoginAsync(CancellationToken cancellationToken)
    {
        string password = (await File.ReadAllTextAsync(passwordFile, cancellationToken)).Trim();
        AuthenticationTokenGetParameters parameters = new() { Username = technicalUser, Password = password };
        RestResponse<TokenPair> response = await middlewareClient.AuthenticateUser(parameters);
        TokenPair? tokens = TokenPairResponseParser.Parse(response, kLogCategory);
        if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            throw new InvalidOperationException($"The technical user {technicalUser} could not log in to the middleware server (HTTP {(int)response.StatusCode}).");
        }
        return tokens;
    }

    /// <summary>
    /// Gets a new token pair with the refresh token; null if the middleware server refuses it.
    /// </summary>
    public async Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        RestResponse<TokenPair> response = await middlewareClient.RefreshToken(new RefreshTokenRequest { RefreshToken = refreshToken });
        TokenPair? tokens = TokenPairResponseParser.Parse(response, kLogCategory);
        return tokens != null && !string.IsNullOrWhiteSpace(tokens.AccessToken) ? tokens : null;
    }

    /// <summary>
    /// Disposes the client of the middleware server.
    /// </summary>
    public void Dispose()
    {
        middlewareClient.Dispose();
    }
}
