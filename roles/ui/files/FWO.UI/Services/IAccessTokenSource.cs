namespace FWO.Ui.Services
{
    /// <summary>
    /// Provides the access token (JWT) of the user of the current UI session, e.g. to send it to the AI assistant.
    /// </summary>
    public interface IAccessTokenSource
    {
        /// <summary>
        /// Returns the access token of the user, refreshing the session first if the token is about to expire.
        /// </summary>
        /// <returns>The access token, or null if the session has none.</returns>
        Task<string?> GetAccessToken();
    }
}
