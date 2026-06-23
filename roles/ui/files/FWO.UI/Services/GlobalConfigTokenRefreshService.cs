using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Services;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Refreshes the anonymous token used by the singleton global configuration subscription.
    /// </summary>
    public class GlobalConfigTokenRefreshService : TokenRefreshBackgroundService<TokenPair>
    {
        private const string LogCategory = "Global Config Token Refresh";

        private readonly GlobalConfigApiConnection globalConfigApiConnection;
        private readonly IAnonymousGlobalConfigTokenProvider tokenProvider;
        private readonly GlobalConfigTokenState tokenState;

        /// <summary>
        /// Creates a refresh service for the singleton global configuration subscription token.
        /// </summary>
        /// <param name="globalConfigApiConnection">Connection used by the singleton global configuration.</param>
        /// <param name="tokenProvider">Provider for fresh anonymous token pairs.</param>
        /// <param name="tokenState">State containing the current anonymous token pair.</param>
        /// <param name="options">Refresh timing options.</param>
        public GlobalConfigTokenRefreshService(
            GlobalConfigApiConnection globalConfigApiConnection,
            IAnonymousGlobalConfigTokenProvider tokenProvider,
            GlobalConfigTokenState tokenState,
            TokenRefreshOptions? options = null)
            : base(options, LogCategory)
        {
            this.globalConfigApiConnection = globalConfigApiConnection;
            this.tokenProvider = tokenProvider;
            this.tokenState = tokenState;
        }

        /// <inheritdoc />
        protected override TokenPair CurrentToken => tokenState.CurrentTokenPair;

        /// <inheritdoc />
        protected override DateTime? CurrentTokenExpiresUtc =>
            string.IsNullOrWhiteSpace(tokenState.CurrentTokenPair.AccessToken)
                ? null
                : NormalizeTokenExpiration(tokenState.CurrentTokenPair.AccessTokenExpires);

        /// <inheritdoc />
        protected override async Task<TokenPair> AcquireFreshTokenAsync(CancellationToken cancellationToken)
        {
            TokenPair refreshedTokenPair = await tokenProvider.CreateTokenPairAsync(cancellationToken);
            await globalConfigApiConnection.ApiConnection.ReconnectSubscriptionsAsync(refreshedTokenPair.AccessToken, cancellationToken);
            tokenState.CurrentTokenPair = refreshedTokenPair;

            Log.WriteInfo(LogCategory, $"Refreshed anonymous global config token. Access token expires at {NormalizeTokenExpiration(refreshedTokenPair.AccessTokenExpires):yyyy-MM-dd'T'HH:mm:ss'Z'}.");

            return refreshedTokenPair;
        }

        private static DateTime NormalizeTokenExpiration(DateTime expiration)
        {
            if (expiration == default)
            {
                return DateTime.MinValue;
            }

            return expiration.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(expiration, DateTimeKind.Utc),
                _ => expiration.ToUniversalTime()
            };
        }
    }
}
