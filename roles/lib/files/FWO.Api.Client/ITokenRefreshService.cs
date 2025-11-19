namespace FWO.Api.Client
{
    /// <summary>
    /// Interface for token refresh operations.
    /// Allows GraphQlApiConnection to refresh tokens without depending on UI layer.
    /// </summary>
    public interface ITokenRefreshService
    {
        /// <summary>
        /// Checks if the current access token is expired or expiring soon.
        /// </summary>
        bool IsAccessTokenExpired();

        /// <summary>
        /// Refreshes the access token using the refresh token.
        /// </summary>
        /// <returns>True if refresh was successful, false otherwise.</returns>
        Task<bool> RefreshAccessTokenAsync();
    }
}
