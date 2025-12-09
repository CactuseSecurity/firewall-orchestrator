using FWO.Api.Client;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using RestSharp;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Manages token pairs (access + refresh tokens) for the current user session.
    /// </summary>
    public class TokenService
    {
        private readonly MiddlewareClient middlewareClient;
        private readonly ISessionStorage sessionStorage;
        private TokenPair? currentTokenPair;
        private readonly JwtSecurityTokenHandler jwtHandler = new();
        private readonly SemaphoreSlim refreshSemaphore = new(1, 1);
        private const string TOKEN_PAIR_KEY = "token_pair";
        private bool isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the TokenService class.
        /// </summary>
        /// <param name="middlewareClient">The middleware client for token operations.</param>
        /// <param name="sessionStorage">The session storage for persisting tokens.</param>
        public TokenService(MiddlewareClient middlewareClient, ISessionStorage sessionStorage)
        {
            this.middlewareClient = middlewareClient;
            this.sessionStorage = sessionStorage;
        }

        /// <summary>
        /// Initializes the TokenService by trying to load any existing token pair from session storage.
        /// </summary>
        /// <returns></returns>
        private async Task Initialize()
        {
            if (isInitialized) return;

            ProtectedBrowserStorageResult<TokenPair> result = await sessionStorage.GetAsync<TokenPair>(TOKEN_PAIR_KEY);

            if (result.Success && result.Value != null)
            {
                currentTokenPair = result.Value;
            }

            isInitialized = true;
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
            isInitialized = true;
        }

        /// <summary>
        /// Gets the current access token.
        /// </summary>
        /// <returns>The access token or null if not available.</returns>
        public async Task<string?> GetAccessTokenAsync()
        {
            await Initialize();
            return currentTokenPair?.AccessToken;
        }

        /// <summary>
        /// Checks if the current access token is expired or about to expire within the next minute.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsAccessTokenExpired()
        {
            await Initialize();

            if (currentTokenPair is null || string.IsNullOrEmpty(currentTokenPair.AccessToken))
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
        /// Refreshes the access token using the current refresh token.
        /// </summary>
        /// <returns>True if refresh was successful, false otherwise.</returns>
        public async Task<bool> RefreshAccessTokenAsync()
        {
            await Initialize();

            if (currentTokenPair is null || string.IsNullOrEmpty(currentTokenPair.RefreshToken))
            {
                Log.WriteWarning("Token Refresh", "No refresh token available");
                return false;
            }

            await refreshSemaphore.WaitAsync();

            try
            {
                if (!await IsAccessTokenExpired())
                {
                    return true;
                }

                RefreshTokenRequest refreshRequest = new()
                {
                    RefreshToken = currentTokenPair.RefreshToken
                };

                RestResponse<TokenPair> response = await middlewareClient.RefreshToken(refreshRequest);

                if (response.IsSuccessful && response.Data != null)
                {
                    await SetTokenPair(response.Data);

                    Log.WriteInfo("Token Refresh", "Successfully refreshed access token");

                    return true;
                }
                else
                {
                    Log.WriteWarning("Token Refresh", $"Failed to refresh token: {response.ErrorMessage ?? response.Content}");

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Refresh", "Error refreshing access token", ex);

                return false;
            }
            finally
            {
                refreshSemaphore.Release();
            }
        }

        /// <summary>
        /// Revokes the current refresh token and clears the stored token pair.
        /// </summary>
        /// <returns></returns>
        public async Task RevokeTokens()
        {
            await Initialize();

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
            isInitialized = false;
        }
    }
}
