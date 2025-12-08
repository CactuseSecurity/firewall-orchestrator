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
    public class TokenService(MiddlewareClient middlewareClient, ProtectedSessionStorage sessionStorage)
    {
        private TokenPair? currentTokenPair;
        private readonly JwtSecurityTokenHandler jwtHandler = new();
        private readonly SemaphoreSlim refreshSemaphore = new(1, 1);
        private const string TOKEN_PAIR_KEY = "token_pair";

        /// <summary>
        /// Initializes the TokenService by trying to load any existing token pair from session storage.
        /// </summary>
        /// <returns></returns>
        private async Task Initialize()
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
        }
    }
}
