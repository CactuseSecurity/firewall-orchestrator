using FWO.Api.Client;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Manages token pairs (access + refresh tokens) for the current user session.
    /// </summary>
    public class TokenService(MiddlewareClient middlewareClient, ApiConnection apiConnection, ProtectedSessionStorage sessionStorage)
    {
        private TokenPair? currentTokenPair;
        private readonly JwtSecurityTokenHandler jwtHandler = new();
        private readonly SemaphoreSlim refreshSemaphore = new(1, 1);
        private const string TOKEN_PAIR_KEY = "token_pair";

        /// <summary>
        /// Initializes the token service and tries loading any existing token pair from session storage.
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            ProtectedBrowserStorageResult<TokenPair> result = await sessionStorage.GetAsync<TokenPair>(TOKEN_PAIR_KEY);

            if (result.Success && result.Value != null)
            {
                currentTokenPair = result.Value;
            }
        }

        /// <summary>
        /// Sets the current token pair and stores it in session storage.
        /// </summary>
        /// <param name="tokenPair">The <see cref="TokenPair"/> object to set and persist.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task SetTokenPair(TokenPair tokenPair)
        {
            currentTokenPair = tokenPair;
            await sessionStorage.SetAsync(TOKEN_PAIR_KEY, tokenPair);
        }

        /// <summary>
        /// Refreshes the access token using the refresh token
        /// </summary>
        /// <returns></returns>
        public async Task<bool> RefreshAccessToken()
        {
            await refreshSemaphore.WaitAsync();

            try
            {
                if (!IsAccessTokenExpired())
                {
                    return true;
                }

                if (currentTokenPair?.RefreshToken == null)
                {
                    await Initialize();

                    if (currentTokenPair?.RefreshToken == null)
                    {
                        return false;
                    }
                }

                Log.WriteDebug("Token Refresh", "Attempting to refresh access token");

                RefreshTokenRequest refreshRequest = new()
                {
                    RefreshToken = currentTokenPair.RefreshToken
                };

                RestSharp.RestResponse<TokenPair> response = await middlewareClient.RefreshToken(refreshRequest);

                if (response.IsSuccessful && response.Data != null)
                {
                    await SetTokenPair(response.Data);

                    // Tell api connection to use new jwt as authentication
                    apiConnection.SetAuthHeader(response.Data.AccessToken);

                    // Tell middleware connection to use new jwt as authentication
                    middlewareClient.SetAuthenticationToken(response.Data.AccessToken);

                    Log.WriteInfo("Token Refresh", "Access token refreshed successfully");

                    return true;
                }
                else
                {
                    Log.WriteError("Token Refresh", $"Failed to refresh token: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Refresh", "Exception during token refresh", ex);

                return false;
            }
            finally
            {
                refreshSemaphore.Release();
            }
        }

        /// <summary>
        /// Checks if the current access token is expired or about to expire within the next minute.
        /// </summary>
        /// <returns></returns>
        public bool IsAccessTokenExpired()
        {
            if (string.IsNullOrEmpty(currentTokenPair?.AccessToken))
            {
                return true;
            }

            try
            {
                JwtSecurityToken token = jwtHandler.ReadJwtToken(currentTokenPair.AccessToken);

                return token.ValidTo <= DateTime.UtcNow.AddMinutes(1);
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Token Check", $"Failed to read JWT: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Revokes the current refresh token and clears the stored token pair.
        /// </summary>
        /// <returns></returns>
        public async Task RevokeTokens()
        {
            if (currentTokenPair is null)
            {
                return;
            }

            RefreshTokenRequest revokeTokenRequest = new()
            {
                RefreshToken = currentTokenPair.RefreshToken
            };

            await middlewareClient.RevokeRefreshToken(revokeTokenRequest);
            await sessionStorage.DeleteAsync(TOKEN_PAIR_KEY);

            currentTokenPair = null;
        }
    }
}
