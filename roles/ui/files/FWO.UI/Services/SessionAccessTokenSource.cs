namespace FWO.Ui.Services
{
    /// <summary>
    /// Provides the access token of the current UI session from the <see cref="TokenService"/>. An access token about to
    /// expire is refreshed by restoring the session (<see cref="UiSessionRestorer.RestoreAsync"/>), which also hands the
    /// new token to the API connection and the middleware client of the session, as the periodic token refresh does.
    /// </summary>
    public sealed class SessionAccessTokenSource : IAccessTokenSource
    {
        private readonly TokenService tokenService;
        private readonly Func<Task> restoreSession;

        /// <summary>
        /// Creates the token source of a UI session.
        /// </summary>
        /// <param name="tokenService">Token service of the session</param>
        /// <param name="restoreSession">Restores the session from its refresh token, e.g. <see cref="UiSessionRestorer.RestoreAsync"/></param>
        public SessionAccessTokenSource(TokenService tokenService, Func<Task> restoreSession)
        {
            this.tokenService = tokenService;
            this.restoreSession = restoreSession;
        }

        /// <inheritdoc/>
        public async Task<string?> GetAccessToken()
        {
            if (await tokenService.HasRefreshToken() && await tokenService.IsAccessTokenExpired())
            {
                await restoreSession();
            }
            return await tokenService.GetAccessToken();
        }
    }
}
