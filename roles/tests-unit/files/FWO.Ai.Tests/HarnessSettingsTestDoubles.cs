using FWO.Ai.Harness.Settings;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Middleware;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Ai.Tests;

/// <summary>
/// Reader of the AI settings returning fixed values, counting its calls.
/// </summary>
internal sealed class FakeAiConfigReader : IAiConfigReader
{
    internal Dictionary<string, string?> ConfigValues { get; } = [];

    internal AiCredentialValue? Credential { get; set; }

    internal Exception? Failure { get; set; }

    internal int SettingsReads { get; private set; }

    internal int CredentialReads { get; private set; }

    public Task<IReadOnlyDictionary<string, string?>> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        SettingsReads++;
        return Failure != null ? Task.FromException<IReadOnlyDictionary<string, string?>>(Failure)
            : Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>(ConfigValues));
    }

    public Task<AiCredentialValue?> ReadCredentialAsync(CancellationToken cancellationToken)
    {
        CredentialReads++;
        return Task.FromResult(Credential);
    }
}

/// <summary>
/// Source of the AI settings returning the given settings, or throwing the given failure, counting its calls.
/// </summary>
internal sealed class FakeAiSettingsSource : IAiSettingsSource
{
    internal AiRuntimeSettings? Settings { get; set; }

    internal Exception? Failure { get; set; }

    internal int Loads => Volatile.Read(ref loads);

    private int loads;

    public Task<AiRuntimeSettings> LoadAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref loads);
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure != null)
        {
            return Task.FromException<AiRuntimeSettings>(Failure);
        }
        return Task.FromResult(Settings ?? throw new InvalidOperationException("No settings scripted."));
    }
}

/// <summary>
/// Login of the technical user handing out tokens that expire after the given lifetime, counting logins and refreshes.
/// </summary>
internal sealed class FakeTechnicalUserLogin(TimeProvider timeProvider, TimeSpan tokenLifetime) : ITechnicalUserLogin
{
    internal int Logins { get; private set; }

    internal List<string> UsedRefreshTokens { get; } = [];

    internal bool RefuseRefresh { get; set; }

    internal List<string> IssuedAccessTokens { get; } = [];

    public Task<TokenPair> LoginAsync(CancellationToken cancellationToken)
    {
        Logins++;
        return Task.FromResult(IssueTokens());
    }

    public Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        UsedRefreshTokens.Add(refreshToken);
        return Task.FromResult(RefuseRefresh ? null : IssueTokens());
    }

    private TokenPair IssueTokens()
    {
        string accessToken = AiTestTokens.Create(timeProvider.GetUtcNow().UtcDateTime.Add(tokenLifetime), IssuedAccessTokens.Count);
        IssuedAccessTokens.Add(accessToken);
        return new TokenPair { AccessToken = accessToken, RefreshToken = $"refresh-{IssuedAccessTokens.Count}" };
    }
}

/// <summary>
/// API connection answering the queries of the AI harness, recording the tokens it is given.
/// </summary>
internal sealed class RecordingAiApiConnection : ApiConnection
{
    internal List<string> AuthHeaders { get; } = [];

    internal List<string> Queries { get; } = [];

    internal List<object?> Variables { get; } = [];

    internal List<AiConfigValue> ConfigValues { get; set; } = [];

    internal List<AiCredentialValue> Credentials { get; set; } = [];

    internal bool Disposed { get; private set; }

    public override void SetAuthHeader(string jwt)
    {
        AuthHeaders.Add(jwt);
    }

    public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
        QueryChunkingOptions? chunkingOptions = null)
    {
        Queries.Add(query);
        Variables.Add(variables);
        if (query == AiQueries.getAiSettings)
        {
            return Task.FromResult((QueryResponseType)(object)ConfigValues);
        }
        if (query == AiQueries.getAiProviderCredential)
        {
            return Task.FromResult((QueryResponseType)(object)Credentials);
        }
        throw new NotSupportedException($"Unexpected query {query}");
    }

    public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => throw new NotSupportedException();

    public override void SetRole(string role) => throw new NotSupportedException();

    public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) => throw new NotSupportedException();

    public override void SwitchBack() => throw new NotSupportedException();

    public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null,
        string? operationName = null) => throw new NotSupportedException();

    public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
        GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null,
        string? operationName = null) => throw new NotSupportedException();

    public override void DisposeSubscriptions<T>() => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
    }
}

/// <summary>
/// Clock that only moves when a test moves it.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Unsigned JWTs with a given expiry, standing in for the tokens of the technical user.
/// </summary>
internal static class AiTestTokens
{
    private const string kIssuer = "ai-harness-test";

    /// <summary>
    /// Creates an unsigned JWT expiring at the given time; the serial number makes each token unique.
    /// </summary>
    internal static string Create(DateTime expiresUtc, int serialNumber)
    {
        JwtSecurityToken token = new(issuer: kIssuer, audience: serialNumber.ToString(), notBefore: expiresUtc.AddDays(-1), expires: expiresUtc);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Points the GraphQL query files at the repository when the tests do not run on an installed system, the way
    /// FWO.Test does it.
    /// </summary>
    internal static void UseRepositoryQueries()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FWO_BASE_DIR")))
        {
            return;
        }
        string baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "common", "files"));
        if (Directory.Exists(Path.Combine(baseDir, "fwo-api-calls")))
        {
            Environment.SetEnvironmentVariable("FWO_BASE_DIR", baseDir);
        }
    }
}

/// <summary>
/// Logger recording the level and text of each entry.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly List<(LogLevel Level, string Message)> entries = [];
    private readonly Lock entriesLock = new();

    internal List<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (entriesLock)
            {
                return [.. entries];
            }
        }
    }

    internal int Count(LogLevel level) => Entries.Count(entry => entry.Level == level);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (entriesLock)
        {
            entries.Add((logLevel, formatter(state, exception)));
        }
    }
}

/// <summary>
/// Logger factory handing out one <see cref="RecordingLogger"/> per category, so that a test can read the entries of
/// one class.
/// </summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentDictionary<string, RecordingLogger> loggers = new();

    /// <summary>
    /// Returns the logger of the category of the given class.
    /// </summary>
    internal RecordingLogger GetLogger<T>() => GetLogger(typeof(T).FullName!);

    public ILogger CreateLogger(string categoryName) => GetLogger(categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotSupportedException("The recording logger factory takes no providers.");
    }

    public void Dispose()
    {
        // nothing to release, the loggers only keep their entries in memory
    }

    private RecordingLogger GetLogger(string categoryName) => loggers.GetOrAdd(categoryName, _ => new RecordingLogger());
}
