using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Ai;
using FWO.Data.Middleware;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Reads the AI settings and the credential of the AI provider from the API with the JWT of the technical user of the
/// harness (role ai-harness), which may read exactly these. The token is kept until shortly before it expires and then
/// renewed with its refresh token, or by a new login if that fails. Not thread-safe: the settings are read by one
/// background service only.
/// </summary>
public sealed class FwoAiConfigReader : IAiConfigReader, IDisposable
{
    /// <summary>A token is renewed when it expires within this time.</summary>
    internal static readonly TimeSpan kTokenRenewalLeadTime = TimeSpan.FromMinutes(2);

    private readonly ITechnicalUserLogin login;
    private readonly Func<string, ApiConnection> connect;
    private readonly TimeProvider timeProvider;
    private ApiConnection? apiConnection;
    private string? refreshToken;
    private DateTime accessTokenExpiresUtc = DateTime.MinValue;

    /// <summary>
    /// Creates the reader.
    /// </summary>
    /// <param name="login">Gets the tokens of the technical user</param>
    /// <param name="connect">Creates the API connection with a JWT, e.g. a GraphQL connection to the API server</param>
    /// <param name="timeProvider">Clock deciding when the token is renewed; the system clock if null</param>
    public FwoAiConfigReader(ITechnicalUserLogin login, Func<string, ApiConnection> connect, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(connect);
        this.login = login;
        this.connect = connect;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Reads the global config items of the AI settings.
    /// </summary>
    /// <exception cref="Exception">The technical user could not log in or the API could not be read.</exception>
    public async Task<IReadOnlyDictionary<string, string?>> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        ApiConnection connection = await GetConnectionAsync(cancellationToken);
        List<AiConfigValue> configValues = await connection.SendQueryAsync<List<AiConfigValue>>(AiQueries.getAiSettings, new { keys = AiSettings.kKeys });
        Dictionary<string, string?> valuesByKey = [];
        foreach (AiConfigValue configValue in configValues)
        {
            valuesByKey[configValue.Key] = configValue.Value;
        }
        return valuesByKey;
    }

    /// <summary>
    /// Reads the credential the AI settings reference; null if there is none.
    /// </summary>
    /// <exception cref="Exception">The technical user could not log in or the API could not be read.</exception>
    public async Task<AiCredentialValue?> ReadCredentialAsync(CancellationToken cancellationToken)
    {
        ApiConnection connection = await GetConnectionAsync(cancellationToken);
        List<AiCredentialValue> credentials = await connection.SendQueryAsync<List<AiCredentialValue>>(AiQueries.getAiProviderCredential);
        return credentials.Count == 1 ? credentials[0] : null;
    }

    /// <summary>
    /// Disposes the API connection.
    /// </summary>
    public void Dispose()
    {
        apiConnection?.Dispose();
        apiConnection = null;
    }

    /// <summary>
    /// Returns the API connection with a token that is valid for at least <see cref="kTokenRenewalLeadTime"/>.
    /// </summary>
    private async Task<ApiConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        DateTime latestUseUtc = timeProvider.GetUtcNow().UtcDateTime + kTokenRenewalLeadTime;
        if (apiConnection != null && latestUseUtc < accessTokenExpiresUtc)
        {
            return apiConnection;
        }
        TokenPair tokens = await RenewTokensAsync(cancellationToken);
        refreshToken = tokens.RefreshToken;
        accessTokenExpiresUtc = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken).ValidTo;
        if (apiConnection == null)
        {
            apiConnection = connect(tokens.AccessToken);
        }
        else
        {
            apiConnection.SetAuthHeader(tokens.AccessToken);
        }
        return apiConnection;
    }

    /// <summary>
    /// Gets a new token pair with the refresh token, or by a new login if there is none or it is refused.
    /// </summary>
    private async Task<TokenPair> RenewTokensAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(refreshToken))
        {
            TokenPair? refreshed = await login.RefreshAsync(refreshToken, cancellationToken);
            refreshToken = null;
            if (refreshed != null)
            {
                return refreshed;
            }
        }
        return await login.LoginAsync(cancellationToken);
    }
}
