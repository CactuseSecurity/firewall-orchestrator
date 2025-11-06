using FWO.Api.Client;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Manages token pairs (access + refresh tokens) for the current user session.
    /// </summary>
    public class TokenService : ITokenRefreshService // ✅ Implements interface
    {
        private readonly MiddlewareClient middlewareClient;
        private readonly ApiConnection apiConnection;
        private TokenPair? currentTokenPair;
        private readonly JwtSecurityTokenHandler jwtHandler = new();
        private readonly SemaphoreSlim refreshSemaphore = new(1, 1);

        public TokenService(MiddlewareClient middlewareClient, ApiConnection apiConnection)
        {
            this.middlewareClient = middlewareClient;
            this.apiConnection = apiConnection;
        }

        public void SetTokenPair(TokenPair tokenPair)
        {
            currentTokenPair = tokenPair;
        }

        public async Task<bool> RefreshAccessTokenAsync()
        {
            await refreshSemaphore.WaitAsync();

            try
            {
                // Double-check if still expired
                if(!IsAccessTokenExpired())
                {
                    return true;
                }

                if(currentTokenPair?.RefreshToken == null)
                {
                    Log.WriteWarning("Token Refresh", "No refresh token available");
                    return false;
                }

                Log.WriteDebug("Token Refresh", "Attempting to refresh access token");

                var refreshRequest = new RefreshTokenRequest
                {
                    RefreshToken = currentTokenPair.RefreshToken
                };

                var response = await middlewareClient.RefreshToken(refreshRequest);

                if(response.IsSuccessful && response.Data != null)
                {
                    currentTokenPair = response.Data;

                    // Update auth headers
                    apiConnection.SetAuthHeader(currentTokenPair.AccessToken);
                    middlewareClient.SetAuthenticationToken(currentTokenPair.AccessToken);

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
                return true;

            try
            {
                var token = jwtHandler.ReadJwtToken(currentTokenPair.AccessToken);
                // Refresh 2 minutes before actual expiry
                return token.ValidTo <= DateTime.UtcNow.AddMinutes(2);
            }
            catch(Exception ex)
            {
                Log.WriteWarning("Token Check", $"Failed to read JWT: {ex.Message}");
                return true;
            }
        }

        public void ClearTokens()
        {
            currentTokenPair = null;
        }

        public TokenPair? GetCurrentTokenPair()
        {
            return currentTokenPair;
        }
    }
}
