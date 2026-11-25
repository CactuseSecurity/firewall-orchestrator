namespace FWO.Basics.OAuth
{
    /// <summary>
    /// Provides the access token of one OAuth 2.0 client (client credentials grant) and keeps it until shortly before it
    /// expires, so that not every request goes to the token endpoint. Only one renewal runs at a time; callers that need
    /// a token meanwhile wait for it. A token the resource server refuses is dropped with <see cref="Invalidate"/>.
    /// Thread-safe. Owns the token client and disposes it.
    /// </summary>
    public sealed class CachedOAuthTokenProvider : IDisposable
    {
        /// <summary>A token is renewed this long before it expires.</summary>
        public static readonly TimeSpan kRenewalLeadTime = TimeSpan.FromSeconds(30);

        /// <summary>A token whose expiry the token endpoint did not tell is kept this long.</summary>
        public static readonly TimeSpan kLifetimeWithoutExpiry = TimeSpan.FromMinutes(1);

        /// <summary>A token that lives shorter than twice the lead time is renewed after half its lifetime.</summary>
        private const int kShortLifetimeDivisor = 2;

        private readonly OAuthTokenClient tokenClient;
        private readonly OAuthClientCredentials credentials;
        private readonly TimeProvider timeProvider;
        private readonly SemaphoreSlim renewalLock = new(1, 1);
        private CachedToken? cachedToken;

        /// <summary>
        /// Creates the provider.
        /// </summary>
        /// <param name="tokenClient">Requests the tokens; disposed with the provider</param>
        /// <param name="credentials">Token endpoint, client id and secret, and the scope if any</param>
        /// <param name="timeProvider">Clock deciding when a token is renewed; the system clock if null</param>
        public CachedOAuthTokenProvider(OAuthTokenClient tokenClient, OAuthClientCredentials credentials, TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(tokenClient);
            ArgumentNullException.ThrowIfNull(credentials);
            this.tokenClient = tokenClient;
            this.credentials = credentials;
            this.timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Returns a valid access token: the kept one, or a new one if it is about to expire or was invalidated.
        /// </summary>
        /// <param name="cancellationToken">Cancels waiting for and requesting a token</param>
        /// <returns>The access token. Never logged or put into error messages.</returns>
        /// <exception cref="Exceptions.OAuthTokenException">A new token was needed, but the token endpoint gave none.</exception>
        /// <exception cref="OperationCanceledException">The request was cancelled.</exception>
        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            string? keptToken = GetKeptToken();
            if (keptToken != null)
            {
                return keptToken;
            }
            await renewalLock.WaitAsync(cancellationToken);
            try
            {
                // another caller may have renewed the token while this one waited
                keptToken = GetKeptToken();
                if (keptToken != null)
                {
                    return keptToken;
                }
                OAuthAccessToken token = await tokenClient.RequestClientCredentialsTokenAsync(credentials, cancellationToken);
                Volatile.Write(ref cachedToken, new CachedToken(token.Value, GetRenewalTime(token)));
                return token.Value;
            }
            finally
            {
                renewalLock.Release();
            }
        }

        /// <summary>
        /// Drops the kept token if it is the given one, e.g. because the resource server refused it, so that the next
        /// call gets a new token. A token renewed meanwhile by another caller is kept.
        /// </summary>
        /// <param name="refusedToken">The token that was refused</param>
        public void Invalidate(string refusedToken)
        {
            CachedToken? current = Volatile.Read(ref cachedToken);
            if (current != null && current.Value == refusedToken)
            {
                Interlocked.CompareExchange(ref cachedToken, null, current);
            }
        }

        /// <summary>
        /// Disposes the token client.
        /// </summary>
        public void Dispose()
        {
            tokenClient.Dispose();
            renewalLock.Dispose();
        }

        /// <summary>
        /// Returns the kept token if it need not be renewed yet; null otherwise.
        /// </summary>
        private string? GetKeptToken()
        {
            CachedToken? current = Volatile.Read(ref cachedToken);
            return current != null && timeProvider.GetUtcNow() < current.RenewAt ? current.Value : null;
        }

        /// <summary>
        /// Returns when a new token is renewed: <see cref="kRenewalLeadTime"/> before it expires, but for a token that
        /// lives shorter than twice that, after half its lifetime.
        /// </summary>
        private DateTimeOffset GetRenewalTime(OAuthAccessToken token)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            DateTimeOffset expiresAt = token.ExpiresAt ?? now + kLifetimeWithoutExpiry;
            TimeSpan lifetime = expiresAt - now;
            if (lifetime <= TimeSpan.Zero)
            {
                return now;
            }
            TimeSpan leadTime = lifetime < kRenewalLeadTime * kShortLifetimeDivisor
                ? lifetime / kShortLifetimeDivisor
                : kRenewalLeadTime;
            return expiresAt - leadTime;
        }

        /// <summary>
        /// A kept token and when it is renewed.
        /// A class rather than a record on purpose: the generated ToString of a record would print the token.
        /// </summary>
        private sealed class CachedToken(string value, DateTimeOffset renewAt)
        {
            public string Value { get; } = value;

            public DateTimeOffset RenewAt { get; } = renewAt;
        }
    }
}
