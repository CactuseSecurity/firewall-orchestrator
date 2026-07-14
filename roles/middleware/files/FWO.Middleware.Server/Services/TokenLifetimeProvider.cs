using FWO.Api.Client;
using FWO.Config.Api.Data;
using FWO.Data.Enums;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Central source for token lifetime defaults and persisted user token settings.
    /// </summary>
    public class TokenLifetimeProvider
    {
        private static readonly TimeSpan kAnonymousTokenLifetime = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan kInternalServiceTokenLifetime = TimeSpan.FromMinutes(60);

        /// <summary>
        /// Gets the configured access-token lifetime for interactive users.
        /// </summary>
        public virtual async Task<TimeSpan> GetUserAccessTokenLifetimeAsync(ApiConnection apiConnection)
        {
            int lifetimeValue = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.AccessTokenLifetime));
            TokenLifetimeUnit lifetimeUnit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.AccessTokenLifetimeUnit));
            return ConvertToTimeSpan(lifetimeValue, lifetimeUnit);
        }

        /// <summary>
        /// Gets the configured refresh-token lifetime for interactive users.
        /// </summary>
        public virtual async Task<TimeSpan> GetRefreshTokenLifetimeAsync(ApiConnection apiConnection)
        {
            int lifetimeValue = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.RefreshTokenLifetime));
            TokenLifetimeUnit lifetimeUnit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.RefreshTokenLifetimeUnit));
            return ConvertToTimeSpan(lifetimeValue, lifetimeUnit);
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

        private static TimeSpan ConvertToTimeSpan(int value, TokenLifetimeUnit unit)
        {
            int safeValue = Math.Max(1, value);

            return unit switch
            {
                TokenLifetimeUnit.Minutes => TimeSpan.FromMinutes(safeValue),
                TokenLifetimeUnit.Hours => TimeSpan.FromHours(safeValue),
                TokenLifetimeUnit.Days => TimeSpan.FromDays(safeValue),
                _ => TimeSpan.FromHours(safeValue)
            };
        }
    }
}
