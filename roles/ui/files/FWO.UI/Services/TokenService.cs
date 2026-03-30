using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using RestSharp;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

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
        private readonly Lazy<Task> initializationTask;

        /// <summary>
        /// Initializes a new instance of the TokenService class.
        /// </summary>
        /// <param name="middlewareClient">The middleware client for token operations.</param>
        /// <param name="sessionStorage">The session storage for persisting tokens.</param>
        public TokenService(MiddlewareClient middlewareClient, ISessionStorage sessionStorage)
        {
            this.middlewareClient = middlewareClient;
            this.sessionStorage = sessionStorage;
            this.initializationTask = new Lazy<Task>(() => InitializeAsync());
        }

        /// <summary>
        /// Initializes the TokenService by trying to load any existing token pair from session storage.
        /// </summary>
        /// <returns></returns>
        private async Task InitializeAsync()
        {
            try
            {
                ProtectedBrowserStorageResult<TokenPair> result = await sessionStorage.GetAsync<TokenPair>(TOKEN_PAIR_KEY);

                if (result.Success && result.Value != null)
                {
                    currentTokenPair = result.Value;
                }
                else
                {
                    currentTokenPair = null;
                }
            }
            catch (CryptographicException ex)
            {
                Log.WriteWarning("Token", $"Unreadable protected session token pair detected, clearing stored data: {ex.Message}");

                await ClearStoredTokenPair();
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Token", $"Failed to restore token pair from session storage, clearing stored data: {ex.Message}");

                await ClearStoredTokenPair();
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
        /// Gets the current access token.
        /// </summary>
        /// <returns>The access token or null if not available.</returns>
        public async Task<string?> GetAccessToken()
        {
            await initializationTask.Value;

            return currentTokenPair?.AccessToken;
        }

        /// <summary>
        /// Checks if the current access token is expired or about to expire within the next minute.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsAccessTokenExpired()
        {
            await initializationTask.Value;

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
        /// Refreshes the token pair using the current refresh token.
        /// </summary>
        /// <returns>The refreshed token pair, or the current pair if no refresh is required. Returns null on failure.</returns>
        public async Task<TokenPair?> RefreshTokenPair()
        {
            await initializationTask.Value;

            if (currentTokenPair is null || string.IsNullOrEmpty(currentTokenPair.RefreshToken))
            {
                Log.WriteWarning("Token Refresh", "No refresh token available");

                return null;
            }

            await refreshSemaphore.WaitAsync();

            try
            {
                if (!await IsAccessTokenExpired())
                {
                    return currentTokenPair;
                }

                RefreshTokenRequest refreshRequest = new()
                {
                    RefreshToken = currentTokenPair.RefreshToken
                };

                RestResponse<TokenPair> response = await middlewareClient.RefreshToken(refreshRequest);
                TokenPair? refreshedTokenPair = ParseTokenPairResponse(response);

                if (response.IsSuccessful && refreshedTokenPair != null)
                {
                    await SetTokenPair(refreshedTokenPair);

                    Log.WriteInfo("Token Refresh", "Successfully refreshed access token");

                    return refreshedTokenPair;
                }
                else
                {
                    Log.WriteWarning("Token Refresh", $"Failed to refresh token: {response.ErrorMessage ?? response.Content}");

                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.WriteError("Token Refresh", "Error refreshing access token", ex);

                return null;
            }
            finally
            {
                refreshSemaphore.Release();
            }
        }

        private static TokenPair? ParseTokenPairResponse(RestResponse<TokenPair> response)
        {
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<TokenPair>(response.Content);
            }
            catch (JsonException ex)
            {
                Log.WriteWarning("Token Refresh", $"Failed to deserialize refreshed token pair: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Revokes the current refresh token and clears the stored token pair.
        /// </summary>
        /// <returns></returns>
        public async Task RevokeTokens()
        {
            await initializationTask.Value;

            string? refreshToken = currentTokenPair?.RefreshToken;

            try
            {
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    RefreshTokenRequest revokeTokenRequest = new()
                    {
                        RefreshToken = refreshToken
                    };

                    RestResponse response = await middlewareClient.RevokeRefreshToken(revokeTokenRequest);

                    if (!response.IsSuccessful)
                    {
                        Log.WriteWarning("Token Revoke", $"Server-side revoke failed during logout: {response.StatusCode} {response.ErrorMessage ?? response.Content}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Token Revoke", $"Server-side revoke failed during logout: {ex.Message}");
            }
            finally
            {
                await ClearStoredTokenPair();
            }
        }

        /// <summary>
        ///  Clears the stored token pair from memory and session storage
        /// </summary>
        /// <returns></returns>
        private async Task ClearStoredTokenPair()
        {
            currentTokenPair = null;

            try
            {
                await sessionStorage.DeleteAsync(TOKEN_PAIR_KEY);
            }
            catch (Exception ex)
            {
                Log.WriteWarning("Token", $"Failed to clear stored token pair: {ex.Message}");
            }
        }
    }
}
