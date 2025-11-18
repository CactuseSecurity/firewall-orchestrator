using FWO.Api.Client;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Logging;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Manages token pairs (access + refresh tokens) for the current user session.
    /// </summary>
    public class TokenService : ITokenRefreshService
    {
        private TokenPair? currentTokenPair;
        private readonly ProtectedSessionStorage sessionStorage;
        private readonly JwtSecurityTokenHandler jwtHandler = new();
        private readonly SemaphoreSlim refreshSemaphore = new(1, 1);
        private readonly MiddlewareClient middlewareClient;
        private readonly ApiConnection apiConnection;
        private const string TOKEN_PAIR_KEY = "token_pair";

        public TokenService(MiddlewareClient middlewareClient, ApiConnection apiConnection, ProtectedSessionStorage sessionStorage)
        {
            this.middlewareClient = middlewareClient;
            this.apiConnection = apiConnection;
            this.sessionStorage = sessionStorage;
        }

        public async Task InitializeAsync()
        {
            ProtectedBrowserStorageResult<TokenPair> result = await sessionStorage.GetAsync<TokenPair>(TOKEN_PAIR_KEY);

            if(result.Success && result.Value != null)
            {
                currentTokenPair = result.Value;
            }
        }

        public async Task SetTokenPair(TokenPair tokenPair)
        {
            currentTokenPair = tokenPair;
            await sessionStorage.SetAsync(TOKEN_PAIR_KEY, tokenPair);
        }

        public async Task<bool> RefreshAccessTokenAsync()
        {
            await refreshSemaphore.WaitAsync();

            try
            {
                if(!IsAccessTokenExpired())
                {
                    return true;
                }

                if(currentTokenPair?.RefreshToken == null)
                {
                    await InitializeAsync();

                    if(currentTokenPair?.RefreshToken == null)
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

                if(response.IsSuccessful && response.Data != null)
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
            catch(Exception ex)
            {
                Log.WriteError("Token Refresh", "Exception during token refresh", ex);

                return false;
            }
            finally
            {
                refreshSemaphore.Release();
            }
        }

        public bool IsAccessTokenExpired()
        {
            if(string.IsNullOrEmpty(currentTokenPair?.AccessToken))
            {
                return true;
            }

            try
            {
                JwtSecurityToken token = jwtHandler.ReadJwtToken(currentTokenPair.AccessToken);

                return token.ValidTo <= DateTime.UtcNow.AddMinutes(1);
            }
            catch(Exception ex)
            {
                Log.WriteWarning("Token Check", $"Failed to read JWT: {ex.Message}");
                return true;
            }
        }

        public async Task ClearTokenPair()
        {
            currentTokenPair = null;
            await sessionStorage.DeleteAsync(TOKEN_PAIR_KEY);
        }
    }
}
