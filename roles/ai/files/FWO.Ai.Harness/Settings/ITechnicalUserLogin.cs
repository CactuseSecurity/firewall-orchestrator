using FWO.Data.Middleware;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Gets the tokens of the technical user of the harness from the middleware server.
/// </summary>
public interface ITechnicalUserLogin
{
    /// <summary>
    /// Logs the technical user in with its password.
    /// </summary>
    /// <param name="cancellationToken">Cancels the login</param>
    /// <returns>The token pair of the technical user.</returns>
    /// <exception cref="Exception">The login failed, e.g. the password file is missing or the password is wrong.</exception>
    Task<TokenPair> LoginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a new token pair with the refresh token of the current one, which is used up by it.
    /// </summary>
    /// <param name="refreshToken">Refresh token of the current token pair</param>
    /// <param name="cancellationToken">Cancels the refresh</param>
    /// <returns>The new token pair; null if the refresh token is no longer valid.</returns>
    Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
