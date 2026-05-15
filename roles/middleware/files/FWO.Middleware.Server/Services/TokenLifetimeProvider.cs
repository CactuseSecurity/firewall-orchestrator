using FWO.Api.Client;
using FWO.Config.Api.Data;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Central source for token lifetime defaults and persisted user token settings.
    /// </summary>
    public class TokenLifetimeProvider
    {
        private static readonly TimeSpan kAnonymousTokenLifetime = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan kInternalServiceTokenLifetime = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan kDelegatedUserTokenLifetime = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets the configured access-token lifetime for interactive users.
        /// </summary>
        public virtual async Task<TimeSpan> GetUserAccessTokenLifetimeAsync(ApiConnection apiConnection)
        {
            int lifetimeHours = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.AccessTokenLifetimeHours));
            return TimeSpan.FromHours(Math.Max(1, lifetimeHours));
        }

        /// <summary>
        /// Gets the configured refresh-token lifetime for interactive users.
        /// </summary>
        public virtual async Task<TimeSpan> GetRefreshTokenLifetimeAsync(ApiConnection apiConnection)
        {
            int lifetimeDays = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.RefreshTokenLifetimeDays));
            return TimeSpan.FromDays(Math.Max(1, lifetimeDays));
        }

        /// <summary>
        /// Gets the lifetime for bootstrap tokens used before a user logs in.
        /// </summary>
        public virtual TimeSpan GetAnonymousTokenLifetime()
        {
            return kAnonymousTokenLifetime;
        }

        /// <summary>
        /// Gets the lifetime for internal service tokens such as middleware-server or reporter-viewall.
        /// </summary>
        public virtual TimeSpan GetInternalServiceTokenLifetime()
        {
            return kInternalServiceTokenLifetime;
        }

        /// <summary>
        /// Gets the maximum lifetime for delegated user tokens.
        /// </summary>
        public virtual TimeSpan GetDelegatedUserTokenLifetime()
        {
            return kDelegatedUserTokenLifetime;
        }

        /// <summary>
        /// Caps a requested delegated user-token lifetime to the configured maximum.
        /// </summary>
        public virtual TimeSpan CapDelegatedUserTokenLifetime(TimeSpan? requestedLifetime)
        {
            TimeSpan maxLifetime = GetDelegatedUserTokenLifetime();
            if (requestedLifetime == null || requestedLifetime.Value <= TimeSpan.Zero)
            {
                return maxLifetime;
            }

            return requestedLifetime.Value <= maxLifetime ? requestedLifetime.Value : maxLifetime;
        }
    }
}
